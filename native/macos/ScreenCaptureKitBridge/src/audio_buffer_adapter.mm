#import "screen_capture_bridge.h"

#import <AudioToolbox/AudioToolbox.h>
#import <CoreMedia/CoreMedia.h>
#import <cmath>
#import <cstddef>
#import <cstdint>
#import <vector>

static float clamp_to_unit_float(double value) {
    if (value > 1.0) {
        return 1.0f;
    }

    if (value < -1.0) {
        return -1.0f;
    }

    return static_cast<float>(value);
}

bool sc_forward_audio_sample_buffer(
    CMSampleBufferRef sampleBuffer,
    audio_callback_t callback,
    void* context,
    const char** errorMessage
) {
    if (sampleBuffer == nil || callback == nullptr) {
        if (errorMessage != nullptr) {
            *errorMessage = "Invalid sample buffer or callback.";
        }
        return false;
    }

    CMFormatDescriptionRef formatDescription = CMSampleBufferGetFormatDescription(sampleBuffer);
    const AudioStreamBasicDescription* streamDescription = CMAudioFormatDescriptionGetStreamBasicDescription(formatDescription);
    if (streamDescription == nullptr) {
        if (errorMessage != nullptr) {
            *errorMessage = "Unable to read audio stream description.";
        }
        return false;
    }

    if (streamDescription->mFormatID != kAudioFormatLinearPCM) {
        if (errorMessage != nullptr) {
            *errorMessage = "Unsupported audio format: expected Linear PCM.";
        }
        return false;
    }

    const CMItemCount frameCount = CMSampleBufferGetNumSamples(sampleBuffer);
    const int channels = static_cast<int>(streamDescription->mChannelsPerFrame);
    const UInt32 bytesPerFrame = streamDescription->mBytesPerFrame;
    const UInt32 bytesPerPacket = streamDescription->mBytesPerPacket;
    const UInt32 totalByteCount = static_cast<UInt32>(frameCount) * bytesPerFrame * ((streamDescription->mFormatFlags & kAudioFormatFlagIsNonInterleaved) != 0 ? channels : 1);
    const double timestamp = CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sampleBuffer));

    if (frameCount <= 0 || channels <= 0 || bytesPerFrame == 0 || totalByteCount == 0) {
        if (errorMessage != nullptr) {
            *errorMessage = "Invalid PCM layout in sample buffer.";
        }
        return false;
    }

    std::vector<uint8_t> pcmData(totalByteCount);

    const bool isNonInterleaved = (streamDescription->mFormatFlags & kAudioFormatFlagIsNonInterleaved) != 0;
    const UInt32 bufferCount = isNonInterleaved ? static_cast<UInt32>(channels) : 1;
    const size_t audioBufferListSize = offsetof(AudioBufferList, mBuffers) + sizeof(AudioBuffer) * bufferCount;
    std::vector<uint8_t> audioBufferListStorage(audioBufferListSize);
    auto* audioBufferList = reinterpret_cast<AudioBufferList*>(audioBufferListStorage.data());
    audioBufferList->mNumberBuffers = bufferCount;

    if (isNonInterleaved) {
        const UInt32 bytesPerChannel = static_cast<UInt32>(frameCount) * bytesPerFrame;
        for (UInt32 channelIndex = 0; channelIndex < bufferCount; channelIndex += 1) {
            audioBufferList->mBuffers[channelIndex].mNumberChannels = 1;
            audioBufferList->mBuffers[channelIndex].mDataByteSize = bytesPerChannel;
            audioBufferList->mBuffers[channelIndex].mData = pcmData.data() + (channelIndex * bytesPerChannel);
        }
    } else {
        audioBufferList->mBuffers[0].mNumberChannels = static_cast<UInt32>(channels);
        audioBufferList->mBuffers[0].mDataByteSize = totalByteCount;
        audioBufferList->mBuffers[0].mData = pcmData.data();
    }

    OSStatus status = CMSampleBufferCopyPCMDataIntoAudioBufferList(
        sampleBuffer,
        0,
        static_cast<int32_t>(frameCount),
        audioBufferList
    );

    if (status != noErr) {
        if (errorMessage != nullptr) {
            *errorMessage = "Unable to copy PCM data from sample buffer.";
        }
        return false;
    }

    const bool isFloat = (streamDescription->mFormatFlags & kAudioFormatFlagIsFloat) != 0;
    const bool isSignedInteger = (streamDescription->mFormatFlags & kAudioFormatFlagIsSignedInteger) != 0;
    const bool isPacked = (streamDescription->mFormatFlags & kAudioFormatFlagIsPacked) != 0;
    const int bitsPerChannel = static_cast<int>(streamDescription->mBitsPerChannel);
    const int sampleCount = static_cast<int>(frameCount) * channels;

    if (!isPacked || sampleCount <= 0 || bytesPerPacket == 0) {
        if (errorMessage != nullptr) {
            *errorMessage = "Unsupported PCM buffer layout: expected packed audio.";
        }
        return false;
    }

    std::vector<float> normalizedSamples(sampleCount);

    auto writeFloatSample = [&](int frameIndex, int channelIndex, float value) {
        normalizedSamples[(frameIndex * channels) + channelIndex] = clamp_to_unit_float(value);
    };

    auto writeInt16Sample = [&](int frameIndex, int channelIndex, int16_t value) {
        normalizedSamples[(frameIndex * channels) + channelIndex] = static_cast<float>(value / 32768.0);
    };

    auto writeInt32Sample = [&](int frameIndex, int channelIndex, int32_t value) {
        normalizedSamples[(frameIndex * channels) + channelIndex] = static_cast<float>(value / 2147483648.0);
    };

    if (isFloat && bitsPerChannel == 32) {
        if (isNonInterleaved) {
            for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                const float* source = reinterpret_cast<const float*>(audioBufferList->mBuffers[channelIndex].mData);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                    writeFloatSample(frameIndex, channelIndex, source[frameIndex]);
                }
            }
        } else {
            const float* source = reinterpret_cast<const float*>(pcmData.data());
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                    writeFloatSample(frameIndex, channelIndex, source[(frameIndex * channels) + channelIndex]);
                }
            }
        }
    } else if (isSignedInteger && bitsPerChannel == 16) {
        if (isNonInterleaved) {
            for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                const int16_t* source = reinterpret_cast<const int16_t*>(audioBufferList->mBuffers[channelIndex].mData);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                    writeInt16Sample(frameIndex, channelIndex, source[frameIndex]);
                }
            }
        } else {
            const int16_t* source = reinterpret_cast<const int16_t*>(pcmData.data());
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                    writeInt16Sample(frameIndex, channelIndex, source[(frameIndex * channels) + channelIndex]);
                }
            }
        }
    } else if (isSignedInteger && bitsPerChannel == 32) {
        if (isNonInterleaved) {
            for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                const int32_t* source = reinterpret_cast<const int32_t*>(audioBufferList->mBuffers[channelIndex].mData);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                    writeInt32Sample(frameIndex, channelIndex, source[frameIndex]);
                }
            }
        } else {
            const int32_t* source = reinterpret_cast<const int32_t*>(pcmData.data());
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex += 1) {
                for (int channelIndex = 0; channelIndex < channels; channelIndex += 1) {
                    writeInt32Sample(frameIndex, channelIndex, source[(frameIndex * channels) + channelIndex]);
                }
            }
        }
    } else {
        if (errorMessage != nullptr) {
            *errorMessage = "Unsupported PCM sample format for ScreenCaptureKit audio.";
        }
        return false;
    }

    callback(
        normalizedSamples.data(),
        static_cast<int>(frameCount),
        channels,
        timestamp,
        context
    );

    return true;
}
