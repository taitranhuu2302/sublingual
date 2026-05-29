#pragma once

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*audio_callback_t)(const float* samples, int frame_count, int channels, double timestamp, void* context);

enum sc_status_code {
    SC_STATUS_OK = 0,
    SC_STATUS_INVALID_ARGUMENT = 1,
    SC_STATUS_UNSUPPORTED_PLATFORM = 2,
    SC_STATUS_NOT_INITIALIZED = 3,
    SC_STATUS_ALREADY_RUNNING = 4,
    SC_STATUS_INTERNAL_ERROR = 5,
    SC_STATUS_NO_DATA = 6,
};

// Original callback-based API
int sc_create_session(audio_callback_t callback, void* context);
int sc_start_capture(void);
int sc_stop_capture(void);
int sc_destroy_session(void);
const char* sc_get_last_error_message(void);

// Polling-based API for JavaScript/FFI integration
// Use this instead of callback-based API when callbacks are not supported

// Initialize session in polling mode (no callback needed)
int sc_create_session_polling(void);

// Read audio data from internal buffer
// Returns SC_STATUS_OK if data available, SC_STATUS_NO_DATA if buffer empty
// samples: output buffer (must be pre-allocated)
// max_frames: maximum frames to read
// out_frame_count: actual frames read
// out_channels: number of channels (typically 2)
// out_timestamp: presentation timestamp
int sc_read_audio(float* samples, int max_frames, int* out_frame_count, int* out_channels, double* out_timestamp);

// Get the sample rate (typically 48000)
int sc_get_sample_rate(void);

// Get buffer info
int sc_get_buffer_frames_available(void);

#ifdef __cplusplus
}
#endif
