#pragma once
#include <cstdint>

// Blittable C ABI shared with StreamlineDlssFrame.cs. No SDK types cross this boundary.
struct DlssFrame
{
    uint32_t viewport;
    uint32_t frameIndex;
    uint32_t mode;
    uint32_t inputWidth;
    uint32_t inputHeight;
    uint32_t outputWidth;
    uint32_t outputHeight;
    uint32_t reset;
    uint32_t depthInverted;
    float jitterX;
    float jitterY;
    float motionScaleX;
    float motionScaleY;
    float nearPlane;
    float farPlane;
    float verticalFov;
    float aspectRatio;
    float cameraViewToClip[16];
    float clipToCameraView[16];
    float clipToPreviousClip[16];
    float previousClipToClip[16];
    float cameraPosition[3];
    float cameraUp[3];
    float cameraRight[3];
    float cameraForward[3];
    void* color;
    void* depth;
    void* motion;
    void* output;
};
