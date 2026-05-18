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
};

int sc_create_session(audio_callback_t callback, void* context);
int sc_start_capture(void);
int sc_stop_capture(void);
int sc_destroy_session(void);
const char* sc_get_last_error_message(void);

#ifdef __cplusplus
}
#endif
