#import "screen_capture_bridge.h"

#import <TargetConditionals.h>

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

int sc_create_session(audio_callback_t callback, void* context) {
    if (callback == nullptr) {
        g_last_error_message = "Audio callback must not be null.";
        return SC_STATUS_INVALID_ARGUMENT;
    }

    g_audio_callback = callback;
    g_audio_context = context;
    g_session_initialized = true;
    g_capture_running = false;
    g_last_error_message = "No error";
    return SC_STATUS_OK;
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
    return status;
}

const char* sc_get_last_error_message(void) {
    return g_last_error_message;
}
