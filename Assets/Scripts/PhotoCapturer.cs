using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.WebCam;

public class PhotoCapturer : MonoBehaviour
{
    public event Action<byte[]> OnPhotoCaptured;

#if UNITY_WSA && !UNITY_EDITOR
    private PhotoCapture photoCapture;
    private Resolution resolution;
    private CameraParameters cameraParams;
#endif

    private void Awake()
    {
#if UNITY_WSA && !UNITY_EDITOR
        resolution = ChooseMaxResolution();

        cameraParams = new CameraParameters
        {
            hologramOpacity = 0.0f,
            cameraResolutionWidth = resolution.width,
            cameraResolutionHeight = resolution.height,
            pixelFormat = CapturePixelFormat.BGRA32
        };
#endif
    }

    public void CapturePhoto()
    {
#if UNITY_WSA && !UNITY_EDITOR
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
#else
        Debug.LogWarning("[PhotoCapturer] CapturePhoto nur auf HoloLens/UWP verfügbar.");
#endif
    }

#if UNITY_WSA && !UNITY_EDITOR
    private Resolution ChooseMaxResolution()
    {
        Resolution best = default;
        int bestPixels = 0;

        foreach (var r in PhotoCapture.SupportedResolutions)
        {
            int pixels = r.width * r.height;
            if (pixels > bestPixels)
            {
                best = r;
                bestPixels = pixels;
            }
        }

        if (best.width == 0 || best.height == 0)
        {
            best.width = 1280;
            best.height = 720;
        }
        return best;
    }

    private void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCapture = captureObject;
        photoCapture.StartPhotoModeAsync(cameraParams, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            photoCapture.TakePhotoAsync(OnCapturedPhotoToMemory);
        }
        else
        {
            Debug.LogError("[PhotoCapturer] Failed to start photo mode.");
        }
    }

    private void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            List<byte> imageBufferList = new List<byte>();
            photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);
            OnPhotoCaptured?.Invoke(imageBufferList.ToArray());
        }
        else
        {
            Debug.LogError("[PhotoCapturer] Failed to capture photo to memory.");
        }

        photoCapture.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    private void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCapture.Dispose();
        photoCapture = null;
    }
#endif
}
