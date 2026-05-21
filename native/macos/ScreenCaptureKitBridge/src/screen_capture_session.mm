#import "screen_capture_bridge.h"

#import <CoreAudio/CoreAudio.h>
#import <Foundation/Foundation.h>
#import <CoreMedia/CoreMedia.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>

static int g_audio_callback_count = 0;

static void sc_log_active_output_device(void) {
    AudioDeviceID deviceId = kAudioObjectUnknown;
    UInt32 dataSize = sizeof(deviceId);
    AudioObjectPropertyAddress address = {
        kAudioHardwarePropertyDefaultOutputDevice,
        kAudioObjectPropertyScopeGlobal,
        kAudioObjectPropertyElementMain,
    };

    OSStatus status = AudioObjectGetPropertyData(
        kAudioObjectSystemObject,
        &address,
        0,
        nullptr,
        &dataSize,
        &deviceId
    );

    if (status != noErr || deviceId == kAudioObjectUnknown) {
        NSLog(@"[ScreenCaptureKitBridge] Could not resolve the active default output device. status=%d", status);
        return;
    }

    CFStringRef deviceName = nullptr;
    dataSize = sizeof(deviceName);
    AudioObjectPropertyAddress nameAddress = {
        kAudioObjectPropertyName,
        kAudioObjectPropertyScopeGlobal,
        kAudioObjectPropertyElementMain,
    };

    status = AudioObjectGetPropertyData(
        deviceId,
        &nameAddress,
        0,
        nullptr,
        &dataSize,
        &deviceName
    );

    if (status != noErr || deviceName == nullptr) {
        NSLog(@"[ScreenCaptureKitBridge] Active output device id=%u (name unavailable, status=%d)", deviceId, status);
        return;
    }

    NSLog(@"[ScreenCaptureKitBridge] Active default output device: %@ (id=%u)", (__bridge NSString*)deviceName, deviceId);
    CFRelease(deviceName);
}

extern bool sc_forward_audio_sample_buffer(
    CMSampleBufferRef sampleBuffer,
    audio_callback_t callback,
    void* context,
    const char** errorMessage
);

static const char* g_session_error_message = "No error";

@interface SCBridgeStreamOutput : NSObject <SCStreamOutput>

- (instancetype)initWithCallback:(audio_callback_t)callback context:(void*)context;

@end

@implementation SCBridgeStreamOutput {
    audio_callback_t _callback;
    void* _context;
}

- (instancetype)initWithCallback:(audio_callback_t)callback context:(void*)context {
    self = [super init];
    if (self != nil) {
        _callback = callback;
        _context = context;
    }
    return self;
}

- (void)stream:(SCStream*)stream didOutputSampleBuffer:(CMSampleBufferRef)sampleBuffer ofType:(SCStreamOutputType)type {
    (void)stream;

    if (type != SCStreamOutputTypeAudio || sampleBuffer == nil || _callback == nullptr) {
        return;
    }

    g_audio_callback_count += 1;

    const char* error_message = nullptr;
    if (!sc_forward_audio_sample_buffer(sampleBuffer, _callback, _context, &error_message)) {
        g_session_error_message = error_message != nullptr ? error_message : "Failed to forward audio sample buffer.";
        NSLog(@"[ScreenCaptureKitBridge] Failed to forward audio sample buffer: %s", g_session_error_message);
    }
}

@end

@interface SCBridgeCaptureSession : NSObject

@property(nonatomic, strong) SCStream* stream;
@property(nonatomic, strong) SCBridgeStreamOutput* output;
@property(nonatomic, strong) dispatch_queue_t sampleQueue;

@end

@implementation SCBridgeCaptureSession
@end

static SCBridgeCaptureSession* g_capture_session = nil;

int sc_internal_start_session(audio_callback_t callback, void* context, const char** errorMessage) {
    if (callback == nullptr) {
        g_session_error_message = "Audio callback must not be null.";
        if (errorMessage != nullptr) {
            *errorMessage = g_session_error_message;
        }
        return SC_STATUS_INVALID_ARGUMENT;
    }

    if (@available(macOS 13.0, *)) {
        NSLog(@"[ScreenCaptureKitBridge] Starting ScreenCaptureKit session...");
        sc_log_active_output_device();

        if (g_capture_session != nil && g_capture_session.stream != nil) {
            g_session_error_message = "ScreenCaptureKit stream already exists.";
            if (errorMessage != nullptr) {
                *errorMessage = g_session_error_message;
            }
            return SC_STATUS_ALREADY_RUNNING;
        }

        dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
        __block int status = SC_STATUS_INTERNAL_ERROR;

        [SCShareableContent getShareableContentExcludingDesktopWindows:NO
                                                     onScreenWindowsOnly:NO
                                                     completionHandler:^(SCShareableContent* content, NSError* error) {
            if (error != nil) {
                g_session_error_message = [[error localizedDescription] UTF8String];
                NSLog(@"[ScreenCaptureKitBridge] Failed to get shareable content: %@", error.localizedDescription);
                status = SC_STATUS_INTERNAL_ERROR;
                dispatch_semaphore_signal(semaphore);
                return;
            }

            SCDisplay* display = content.displays.firstObject;
            if (display == nil) {
                g_session_error_message = "No display available for ScreenCaptureKit content filter.";
                NSLog(@"[ScreenCaptureKitBridge] No display available for ScreenCaptureKit content filter.");
                status = SC_STATUS_INTERNAL_ERROR;
                dispatch_semaphore_signal(semaphore);
                return;
            }

            NSLog(@"[ScreenCaptureKitBridge] Using display %u (%ldx%ld)",
                  display.displayID,
                  (long)display.width,
                  (long)display.height);

            SCContentFilter* filter = [[SCContentFilter alloc] initWithDisplay:display excludingWindows:@[]];

            SCStreamConfiguration* configuration = [SCStreamConfiguration new];
            configuration.capturesAudio = YES;
            configuration.captureMicrophone = NO;
            configuration.excludesCurrentProcessAudio = NO;
            configuration.sampleRate = 48000;
            configuration.channelCount = 2;
            configuration.queueDepth = 8;

            SCBridgeCaptureSession* captureSession = [SCBridgeCaptureSession new];
            captureSession.sampleQueue = dispatch_queue_create("ai.sublingual.screencapturekit.audio", DISPATCH_QUEUE_SERIAL);
            captureSession.output = [[SCBridgeStreamOutput alloc] initWithCallback:callback context:context];
            captureSession.stream = [[SCStream alloc] initWithFilter:filter configuration:configuration delegate:nil];

            NSError* addOutputError = nil;
            BOOL outputAdded = [captureSession.stream addStreamOutput:captureSession.output type:SCStreamOutputTypeAudio sampleHandlerQueue:captureSession.sampleQueue error:&addOutputError];
            if (!outputAdded || addOutputError != nil) {
                g_session_error_message = addOutputError != nil
                    ? [[addOutputError localizedDescription] UTF8String]
                    : "Failed to add ScreenCaptureKit audio output.";
                NSLog(@"[ScreenCaptureKitBridge] Failed to add audio output: %@", addOutputError.localizedDescription);
                status = SC_STATUS_INTERNAL_ERROR;
                dispatch_semaphore_signal(semaphore);
                return;
            }

            NSLog(@"[ScreenCaptureKitBridge] Audio output attached. Starting capture...");

            [captureSession.stream startCaptureWithCompletionHandler:^(NSError* startError) {
                if (startError != nil) {
                    g_session_error_message = [[startError localizedDescription] UTF8String];
                    NSLog(@"[ScreenCaptureKitBridge] startCapture failed: %@", startError.localizedDescription);
                    status = SC_STATUS_INTERNAL_ERROR;
                } else {
                    g_capture_session = captureSession;
                    g_session_error_message = "No error";
                    g_audio_callback_count = 0;
                    NSLog(@"[ScreenCaptureKitBridge] Capture started successfully.");
                    status = SC_STATUS_OK;
                }

                dispatch_semaphore_signal(semaphore);
            }];
        }];

        dispatch_semaphore_wait(semaphore, DISPATCH_TIME_FOREVER);

        if (errorMessage != nullptr) {
            *errorMessage = g_session_error_message;
        }

        return status;
    }

    g_session_error_message = "ScreenCaptureKit requires macOS 13.0 or later.";
    if (errorMessage != nullptr) {
        *errorMessage = g_session_error_message;
    }
    return SC_STATUS_UNSUPPORTED_PLATFORM;
}

int sc_internal_stop_session(const char** errorMessage) {
    if (@available(macOS 13.0, *)) {
        NSLog(@"[ScreenCaptureKitBridge] Stopping ScreenCaptureKit session...");

        if (g_capture_session == nil || g_capture_session.stream == nil) {
            g_session_error_message = "No active ScreenCaptureKit stream to stop.";
            if (errorMessage != nullptr) {
                *errorMessage = g_session_error_message;
            }
            return SC_STATUS_OK;
        }

        dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);
        __block int status = SC_STATUS_OK;

        [g_capture_session.stream stopCaptureWithCompletionHandler:^(NSError* error) {
            if (error != nil) {
                g_session_error_message = [[error localizedDescription] UTF8String];
                NSLog(@"[ScreenCaptureKitBridge] stopCapture failed: %@", error.localizedDescription);
                status = SC_STATUS_INTERNAL_ERROR;
            } else {
                g_session_error_message = "No error";
                NSLog(@"[ScreenCaptureKitBridge] Capture stopped successfully. Total audio callbacks: %d", g_audio_callback_count);
            }

            dispatch_semaphore_signal(semaphore);
        }];

        dispatch_semaphore_wait(semaphore, DISPATCH_TIME_FOREVER);

        if (errorMessage != nullptr) {
            *errorMessage = g_session_error_message;
        }
        return status;
    }

    g_session_error_message = "ScreenCaptureKit requires macOS 13.0 or later.";
    if (errorMessage != nullptr) {
        *errorMessage = g_session_error_message;
    }
    return SC_STATUS_UNSUPPORTED_PLATFORM;
}

int sc_internal_destroy_session(const char** errorMessage) {
    NSLog(@"[ScreenCaptureKitBridge] Destroying ScreenCaptureKit session.");
    g_capture_session = nil;
    g_session_error_message = "No error";
    if (errorMessage != nullptr) {
        *errorMessage = g_session_error_message;
    }
    return SC_STATUS_OK;
}
