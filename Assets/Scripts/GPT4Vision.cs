using System;
using UnityEngine;

public class GPT4Vision : MonoBehaviour
{
    [SerializeField] private OpenAIWrapper openAIWrapper;
    [SerializeField] private PhotoCapturer photoCapturer;

    private void OnEnable()
    {
        TryGetComponents();
        if (photoCapturer) photoCapturer.OnPhotoCaptured += OnPhotoCaptured;
    }

    private void OnDisable()
    {
        if (photoCapturer) photoCapturer.OnPhotoCaptured -= OnPhotoCaptured;
    }

    private void TryGetComponents()
    {
        if (!openAIWrapper) openAIWrapper = GetComponentInChildren<OpenAIWrapper>();
        if (!photoCapturer) photoCapturer = GetComponentInChildren<PhotoCapturer>();
    }

    public void CapturePhoto()
    {
        if (!photoCapturer) { Debug.LogError("[GPT4Vision] PhotoCapturer missing."); return; }
        Debug.Log("[GPT4Vision] Capturing photo…");
        photoCapturer.CapturePhoto();
    }

    private async void OnPhotoCaptured(byte[] imageData)
    {
        Debug.Log($"[GPT4Vision] Photo captured, bytes={imageData?.Length}");
        if (imageData == null || imageData.Length == 0)
        {
            Debug.LogError("[GPT4Vision] Empty image data.");
            return;
        }

        try
        {
            await openAIWrapper.AnalyzeImage(imageData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GPT4Vision] Error calling OpenAI: {ex.Message}");
        }
    }
}
