#import "screen_capture_bridge.h"

#import <TargetConditionals.h>
#import <cstring>
#import <mutex>
#import <vector>

#if TARGET_OS_OSX
extern int sc_internal_start_session(audio_callback_t callback, void* context, const char** errorMessage);
extern int sc_internal_stop_session(const char** errorMessage);
extern int sc_internal_destroy_session(const char** errorMessage);
#endif

static audio_callback_t g_audio_callback = nullptr;
static void* g_audio_context = nullptr;
static bool g_session_initialized = false;
static bool g_capture_running = false;
static const char* g_last_error_message = "No error";

// Ring buffer for polling mode
static const int RING_BUFFER_FRAMES = 48000 * 2; // 2 seconds at 48kHz
static std::vector<float> g_ring_buffer;
static int g_ring_buffer_write_pos = 0;
static int g_ring_buffer_read_pos = 0;
static int g_ring_buffer_frames_available = 0;
static int g_last_channels = 2;
static double g_last_timestamp = 0.0;
static std::mutex g_ring_buffer_mutex;
static bool g_polling_mode = false;

// Internal callback for polling mode - stores data in ring buffer
static void polling_audio_callback(const float* samples, int frame_count, int channels, double timestamp, void* context) {
    (void)context;
    
    std::lock_guard<std::mutex> lock(g_ring_buffer_mutex);
    
    g_last_channels = channels;
    g_last_timestamp = timestamp;
    
    const int total_samples = frame_count * channels;
    const int buffer_size = RING_BUFFER_FRAMES * channels;
    
    // Ensure buffer is sized correctly
    if (g_ring_buffer.size() != static_cast<size_t>(buffer_size)) {
        g_ring_buffer.resize(buffer_size);
        g_ring_buffer_write_pos = 0;
        g_ring_buffer_read_pos = 0;
        g_ring_buffer_frames_available = 0;
    }
    
    // Write samples to ring buffer
    for (int i = 0; i < total_samples; i++) {
        g_ring_buffer[g_ring_buffer_write_pos] = samples[i];
        g_ring_buffer_write_pos = (g_ring_buffer_write_pos + 1) % buffer_size;
    }
    
    g_ring_buffer_frames_available += frame_count;
    if (g_ring_buffer_frames_available > RING_BUFFER_FRAMES) {
        // Overflow - advance read position
        int overflow_frames = g_ring_buffer_frames_available - RING_BUFFER_FRAMES;
        g_ring_buffer_read_pos = (g_ring_buffer_read_pos + overflow_frames * channels) % buffer_size;
        g_ring_buffer_frames_available = RING_BUFFER_FRAMES;
    }
}

int sc_create_session(audio_callback_t callback, void* context) {
    if (callback == nullptr) {
        g_last_error_message = "Audio callback must not be null.";
        return SC_STATUS_INVALID_ARGUMENT;
    }

    g_audio_callback = callback;
    g_audio_context = context;
    g_session_initialized = true;
    g_capture_running = false;
    g_polling_mode = false;
    g_last_error_message = "No error";
    return SC_STATUS_OK;
}

int sc_create_session_polling(void) {
    g_audio_callback = polling_audio_callback;
    g_audio_context = nullptr;
    g_session_initialized = true;
    g_capture_running = false;
    g_polling_mode = true;
    
    // Initialize ring buffer
    {
        std::lock_guard<std::mutex> lock(g_ring_buffer_mutex);
        g_ring_buffer.clear();
        g_ring_buffer_write_pos = 0;
        g_ring_buffer_read_pos = 0;
        g_ring_buffer_frames_available = 0;
        g_last_channels = 2;
        g_last_timestamp = 0.0;
    }
    
    g_last_error_message = "No error";
    return SC_STATUS_OK;
}

int sc_read_audio(float* samples, int max_frames, int* out_frame_count, int* out_channels, double* out_timestamp) {
    if (!g_polling_mode) {
        g_last_error_message = "Not in polling mode. Use sc_create_session_polling() first.";
        return SC_STATUS_INVALID_ARGUMENT;
    }
    
    if (samples == nullptr || out_frame_count == nullptr) {
        g_last_error_message = "Invalid output parameters.";
        return SC_STATUS_INVALID_ARGUMENT;
    }
    
    std::lock_guard<std::mutex> lock(g_ring_buffer_mutex);
    
    if (g_ring_buffer_frames_available == 0) {
        *out_frame_count = 0;
        return SC_STATUS_NO_DATA;
    }
    
    const int channels = g_last_channels;
    const int buffer_size = static_cast<int>(g_ring_buffer.size());
    
    if (buffer_size == 0) {
        *out_frame_count = 0;
        return SC_STATUS_NO_DATA;
    }
    
    int frames_to_read = max_frames;
    if (frames_to_read > g_ring_buffer_frames_available) {
        frames_to_read = g_ring_buffer_frames_available;
    }
    
    const int samples_to_read = frames_to_read * channels;
    
    // Read samples from ring buffer
    for (int i = 0; i < samples_to_read; i++) {
        samples[i] = g_ring_buffer[g_ring_buffer_read_pos];
        g_ring_buffer_read_pos = (g_ring_buffer_read_pos + 1) % buffer_size;
    }
    
    g_ring_buffer_frames_available -= frames_to_read;
    
    *out_frame_count = frames_to_read;
    if (out_channels != nullptr) {
        *out_channels = channels;
    }
    if (out_timestamp != nullptr) {
        *out_timestamp = g_last_timestamp;
    }
    
    return SC_STATUS_OK;
}

int sc_get_sample_rate(void) {
    return 48000; // ScreenCaptureKit always outputs at 48kHz
}

int sc_get_buffer_frames_available(void) {
    std::lock_guard<std::mutex> lock(g_ring_buffer_mutex);
    return g_ring_buffer_frames_available;
}

int sc_start_capture(void) {
    if (!g_session_initialized) {
        g_last_error_message = "ScreenCaptureKit session is not initialized.";
        return SC_STATUS_NOT_INITIALIZED;
    }

#if !TARGET_OS_OSX
    g_last_error_message = "ScreenCaptureKit bridge is only supported on macOS.";
    return SC_STATUS_UNSUPPORTED_PLATFORM;
#else
    if (g_capture_running) {
        g_last_error_message = "Capture session is already running.";
        return SC_STATUS_ALREADY_RUNNING;
    }

    const char* error_message = nullptr;
    const int status = sc_internal_start_session(g_audio_callback, g_audio_context, &error_message);
    if (status != SC_STATUS_OK) {
        g_last_error_message = error_message != nullptr ? error_message : "Unknown ScreenCaptureKit start error.";
        g_capture_running = false;
        return status;
    }

    g_capture_running = true;
    g_last_error_message = "No error";
    return SC_STATUS_OK;
#endif
}

int sc_stop_capture(void) {
    if (!g_session_initialized) {
        g_last_error_message = "ScreenCaptureKit session is not initialized.";
        return SC_STATUS_NOT_INITIALIZED;
    }

#if !TARGET_OS_OSX
    g_capture_running = false;
    g_last_error_message = "ScreenCaptureKit bridge is only supported on macOS.";
    return SC_STATUS_UNSUPPORTED_PLATFORM;
#else
    const char* error_message = nullptr;
    const int status = sc_internal_stop_session(&error_message);
    g_capture_running = false;
    g_last_error_message = error_message != nullptr ? error_message : "No error";
    return status;
#endif
}

int sc_destroy_session(void) {
#if TARGET_OS_OSX
    const char* error_message = nullptr;
    const int status = sc_internal_destroy_session(&error_message);
    g_last_error_message = error_message != nullptr ? error_message : "No error";
#else
    const int status = SC_STATUS_OK;
#endif

    g_capture_running = false;
    g_session_initialized = false;
    g_audio_callback = nullptr;
    g_audio_context = nullptr;
    g_polling_mode = false;
    
    // Clear ring buffer
    {
        std::lock_guard<std::mutex> lock(g_ring_buffer_mutex);
        g_ring_buffer.clear();
        g_ring_buffer_write_pos = 0;
        g_ring_buffer_read_pos = 0;
        g_ring_buffer_frames_available = 0;
    }
    
    return status;
}

const char* sc_get_last_error_message(void) {
    return g_last_error_message;
}
