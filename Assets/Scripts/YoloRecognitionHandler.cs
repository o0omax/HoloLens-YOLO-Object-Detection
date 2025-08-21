using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    /// Handles YOLO recognitions and triggers actions once a threshold is hit.
    /// </summary>
    public class YoloRecognitionHandler : MonoBehaviour
    {
        // ====== Inspector ======
        [Header("Trigger")]
        [SerializeField] private ObjectClass triggerClass = ObjectClass.Cup;
        [SerializeField, Range(0f, 1f)] private float triggerConfidence = 0.80f;
        [Tooltip("Minimum times an item must be re-seen before we trust it.")]
        [SerializeField] private int minTimesSeen = 2;

        [Header("UI")]
        [SerializeField] private GameObject labelObject;    // existing 3D marker prefab
        [SerializeField] private TMP_Text apiText;          // <-- drag your 'ApiText' here
        [SerializeField] private TMP_Text statusText;       // optional status line

        [Header("Action Mode")]
        [SerializeField] private ActionMode mode = ActionMode.DebugOnly;
        [Tooltip("Prevents spamming the action each frame.")]
        [SerializeField] private float triggerCooldownSeconds = 3f;

        [Header("Optional: OpenAI Bridge")]
        [SerializeField] private GPT4Vision gpt4Vision;     // <-- drag your AIClient (with GPT4Vision) here

        // ====== Runtime ======
        private readonly List<DisplayedItem> yoloItems = new();
        private YoloDebugOutput yoloDebugOutput;
        private float lastTriggerTime = -999f;

        public enum ActionMode { DebugOnly, RandomText, OpenAI }

        private void Start()
        {
            yoloDebugOutput = GetComponent<YoloDebugOutput>();
            if (!apiText)
                Debug.LogWarning("[YoloRecognitionHandler] ApiText is not assigned. RandomText/OpenAI won't display UI text.");

            if (!gpt4Vision && mode == ActionMode.OpenAI)
                Debug.LogWarning("[YoloRecognitionHandler] GPT4Vision not assigned. Assign your AIClient object when using OpenAI mode.");
        }

        /// <summary>
        /// Entry from your pipeline.
        /// </summary>
        public void ShowRecognitions(List<YoloItem> recognitions, CameraTransform cameraTransform)
        {
            AddNewlyRecognizedObjects(recognitions, cameraTransform);
            RemoveOutdatedObjects();
            TriggerDetectionActions();
        }

        private void AddNewlyRecognizedObjects(List<YoloItem> recognitions, CameraTransform cameraTransform)
        {
            var unmatchedExistingItems = new List<DisplayedItem>(yoloItems);

            foreach (var newItem in recognitions)
            {
                // Fast skip if class isn’t interesting and we are in debug/threshold mode
                // (We still add markers below; adjust if you want only trigger class).
                bool classMatches = newItem.MostLikelyClass == triggerClass;

                // Compute position once
                Vector3? pos = PositionCalculator.CalculatePointInSpace(newItem, cameraTransform);
                if (pos == null) continue;

                // Associate with closest existing item of same class
                var existing = GetClosestExistingItem(unmatchedExistingItems, newItem, pos.Value);
                if (existing == null)
                {
                    yoloItems.Add(new DisplayedItem(newItem, pos.Value));
                }
                else
                {
                    unmatchedExistingItems.Remove(existing);
                    existing.UpdateItem(newItem, pos.Value);
                }
            }
        }

        private DisplayedItem GetClosestExistingItem(List<DisplayedItem> oldItems, YoloItem item, Vector3 positionInSpace)
        {
            DisplayedItem best = null;
            float bestDist = float.MaxValue;

            // Only compare items of same class, keep smallest distance under threshold
            foreach (var old in oldItems)
            {
                if (old.YoloItem.MostLikelyClass != item.MostLikelyClass) continue;

                float d = Vector3.Distance(old.PositionInSpace, positionInSpace);
                if (d > Parameters.MaxIdenticalObject || d >= bestDist) continue;

                best = old;
                bestDist = d;
            }
            return best;
        }

        private void RemoveOutdatedObjects()
        {
            for (int i = yoloItems.Count - 1; i >= 0; i--)
            {
                var it = yoloItems[i];
                bool wasInView = it.IsInCameraView;
                bool inView = PositionCalculator.IsObjectInCameraView(it.PositionInSpace);
                it.IsInCameraView = inView;

                if (!inView) continue;

                if (!wasInView)
                {
                    it.TimeLastSeen = Time.time;
                    continue;
                }

                if (Time.time - it.TimeLastSeen <= Parameters.ObjectTimeOut) continue;

                if (it.TrackingMarker) Destroy(it.TrackingMarker);
                yoloItems.RemoveAt(i);
            }
        }

        private void TriggerDetectionActions()
        {
            // Only consider stable items
            foreach (var item in yoloItems)
            {
                if (!item.IsInCameraView) continue;
                if (item.TimesSeen < Math.Max(minTimesSeen, Parameters.MinTimesSeen)) continue;

                // Debug marker + overlay
                ManageTrackingMarker(item);
                yoloDebugOutput?.ShowDebugInformationForItem(item);

                // Debug output for the IF logic
                bool classOk = item.YoloItem.MostLikelyClass == triggerClass;
                bool confOk  = item.YoloItem.Confidence >= triggerConfidence;
                Debug.Log($"[YOLO] Seen: {item.YoloItem.MostLikelyClass} conf={item.YoloItem.Confidence:0.00} " +
                          $"(need {triggerClass} @ >= {triggerConfidence:0.00}) -> classOk={classOk}, confOk={confOk}, timesSeen={item.TimesSeen}");

                if (!classOk || !confOk) continue;

                // Cooldown
                if (Time.time - lastTriggerTime < triggerCooldownSeconds) continue;
                lastTriggerTime = Time.time;

                // === TRIGGER ===
                Debug.LogWarning($"=== TRIGGERED {triggerClass} ({item.YoloItem.Confidence:0.00}) ===");

                if (statusText)
                    statusText.text = $"{item.YoloItem.MostLikelyClass} hit {item.YoloItem.Confidence:0.00}, mode={mode}";

                switch (mode)
                {
                    case ActionMode.DebugOnly:
                        // Nothing else; purely logs + status
                        break;

                    case ActionMode.RandomText:
                        if (apiText)
                        {
                            apiText.text = $"[Test] {DateTime.Now:HH:mm:ss}: {triggerClass} erkannt " +
                                           $"({Mathf.Round(item.YoloItem.Confidence * 100f)}%)";
                        }
                        break;

                    case ActionMode.OpenAI:
                        if (apiText) apiText.text = "Sende Bild an OpenAI…";
                        if (gpt4Vision)
                        {
                            // This will capture and forward to OpenAIWrapper.AnalyzeImage(imageData)
                            gpt4Vision.CapturePhoto();
                        }
                        else
                        {
                            Debug.LogError("[YOLO] OpenAI mode selected but GPT4Vision is not assigned.");
                        }
                        break;
                }
            }
        }

        private void ManageTrackingMarker(DisplayedItem item)
        {
            if (!labelObject) return;

            if (!item.TrackingMarker)
                item.TrackingMarker = Instantiate(labelObject, item.PositionInSpace, Quaternion.identity);

            var label = item.TrackingMarker.GetComponent<ObjectLabelController>();
            if (label)
            {
                label.Text = $"{item.YoloItem.MostLikelyClass} ({Math.Round(item.YoloItem.Confidence * 100, 1)}%)";
                label.UpdatePosition(item.PositionInSpace);
            }
        }
    }
}
