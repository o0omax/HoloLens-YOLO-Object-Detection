using System;
using UnityEngine;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http.Headers;

public class OpenAIWrapper : MonoBehaviour
{
    [SerializeField] private string basePrompt = "you are OCR scanner. Return only text:";
    [SerializeField] private string openAIKey = "api-key";
    [SerializeField] private int maxTokens = 300;
    [SerializeField, Range(0f, 1f)] private float samplingTemperature = 0.5f; 
    [SerializeField] private ImageDetail imageDetail = ImageDetail.Auto;
    
    public static event Action<string> OnOpenAIResponse;
    public static event Action OnOpenAIRequest;

    public async Task AnalyzeImage(byte[] imageData, string prompt = "")
    {
        string base64Image = Convert.ToBase64String(imageData);
        await MakeOpenAIRequest(base64Image, String.IsNullOrEmpty(prompt) ? basePrompt : prompt);
    }
    
private async Task MakeOpenAIRequest(string base64Image, string prompt)
{
    Debug.Log("[OpenAI] Sending request…");
    OnOpenAIRequest?.Invoke();

    try
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", openAIKey);

        var payload = new
        {
            // Falls dein Account gpt-4-vision-preview nicht mehr hat: probier "gpt-4o"
            model = "gpt-4o",
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = "data:image/jpeg;base64," + base64Image,
                                detail = imageDetail.ToString().ToLower()
                            }
                        }
                    }
                }
            },
            max_tokens = maxTokens,
            temperature = samplingTemperature
        };

        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        string json = await httpResponse.Content.ReadAsStringAsync();

        if (httpResponse.IsSuccessStatusCode)
        {
            string text = GetResponseContent(json);
            OnOpenAIResponse?.Invoke(text);
            Debug.Log("[OpenAI] Success");
        }
        else
        {
            // *** WICHTIG: auch bei Fehler ein Event feuern, damit dein UI sich ändert ***
            string msg = $"OpenAI HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}\n{json}";
            Debug.LogError("[OpenAI] " + msg);
            OnOpenAIResponse?.Invoke("⚠️ " + msg);
        }
    }
    catch (Exception ex)
    {
        // *** Auch Exceptions an die UI zurückgeben ***
        Debug.LogError("[OpenAI] Exception: " + ex.Message);
        OnOpenAIResponse?.Invoke("❌ OpenAI Fehler: " + ex.Message);
    }
}

    
    // function to parse the response JSON and return the actual response message from OpenAI
    string GetResponseContent(string response)
    {
        try
        {
            var jsonObj = JObject.Parse(response);
            return jsonObj["choices"][0]["message"]["content"].ToString();
        }
        catch (JsonException jsonEx)
        {
            Debug.Log("JSON Parsing Error: " + jsonEx.Message);
        }
        catch (Exception ex)
        {
            Debug.Log("Error in parsing response: " + ex.Message);
        }
    
        return "Error parsing response: " + response;
    }
}