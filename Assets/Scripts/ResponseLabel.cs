using TMPro;
using UnityEngine;

public class ResponseLabel : MonoBehaviour
{
    [Header("Ziel-Text (TMP) – zieh hier 'ApiText (TextMeshProUGUI)' rein")]
    [SerializeField] private TMP_Text textTarget;

    [TextArea]
    [SerializeField] private string waitingText = "Sende Bild an OpenAI…";

    private void Awake()
    {
        // Falls nichts zugewiesen wurde: erst auf diesem GO, dann in Kindern suchen
        if (!textTarget)
            textTarget = GetComponent<TMP_Text>();
        if (!textTarget)
            textTarget = GetComponentInChildren<TMP_Text>(true);

        if (!textTarget)
            Debug.LogWarning("[ResponseLabel] Kein TMP_Text gefunden – bitte 'Text Target' im Inspector zuweisen.");
    }

    private void OnEnable()
    {
        OpenAIWrapper.OnOpenAIRequest  += HandleRequest;
        OpenAIWrapper.OnOpenAIResponse += HandleResponse;
    }

    private void OnDisable()
    {
        OpenAIWrapper.OnOpenAIRequest  -= HandleRequest;
        OpenAIWrapper.OnOpenAIResponse -= HandleResponse;
    }

    private void HandleRequest()
    {
        ShowText(waitingText);
    }

    private void HandleResponse(string s)
    {
        ShowText(s);
    }

    public void ShowText(string s)
    {
        if (!textTarget)
        {
            Debug.LogWarning("[ResponseLabel] Kein TMP_Text gefunden, kann Text nicht setzen.");
            return;
        }

        textTarget.text = string.IsNullOrEmpty(s) ? "(leer)" : s;
    }
}
