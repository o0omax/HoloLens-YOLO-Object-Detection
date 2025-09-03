using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts; // for WebCamTextureAccess

[RequireComponent(typeof(RawImage))]
public class CameraFeedUIBinder : MonoBehaviour
{
    RawImage img;

#if UNITY_EDITOR
    void OnEnable()
    {
        img = GetComponent<RawImage>();
        StartCoroutine(BindWhenReady());
    }

    System.Collections.IEnumerator BindWhenReady()
    {
        // Ensure WebCamTextureAccess was started by YoloModelExecutor; if not, start it.
        if (WebCamTextureAccess.Instance.WebCamTexture == null)
            WebCamTextureAccess.Instance.Play();

        // Wait until the WebCamTexture is available & has size
        var tex = WebCamTextureAccess.Instance.WebCamTexture;
        while (tex == null || tex.width <= 16 || tex.height <= 16)
        {
            tex = WebCamTextureAccess.Instance.WebCamTexture;
            yield return null;
        }

        img.material = null;             // use default unlit UI path
        img.texture  = tex;              // show the same feed YOLO uses

        // Optional: keep rotation/mirroring correct
        Application.onBeforeRender += UpdateUvAndRotation;
        UpdateUvAndRotation();
    }

    void OnDisable()
    {
        Application.onBeforeRender -= UpdateUvAndRotation;
    }

    void UpdateUvAndRotation()
    {
        var tex = WebCamTextureAccess.Instance.WebCamTexture;
        if (tex == null) return;

        // Fix rotation
        img.rectTransform.localEulerAngles = new Vector3(0, 0, -tex.videoRotationAngle);

        // Fix vertical mirroring if needed
        var r = img.uvRect;
        r.x = 0; r.y = 0; r.width = 1; r.height = tex.videoVerticallyMirrored ? -1f : 1f;
        img.uvRect = r;

        // If you add an AspectRatioFitter, keep it updated
        var fitter = GetComponent<AspectRatioFitter>();
        if (fitter != null && tex.width > 0 && tex.height > 0)
        {
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)tex.width / tex.height;
        }
    }
#else
    // On HoloLens this UI binder stays inactive; you use the PV pipeline there.
    void OnEnable() { enabled = false; }
#endif
}
