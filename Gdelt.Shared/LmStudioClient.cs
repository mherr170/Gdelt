using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal static class LmStudioClient
{
    private const string Endpoint = "http://localhost:1234/v1/chat/completions";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };

    public static async Task<string> CallAsync(
        string systemPrompt, string userMessage, int maxTokens, double temperature,
        CancellationToken ct = default)
    {
        var payload = new
        {
            model = "llama-3.2-3b-instruct",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage },
            },
            max_tokens = maxTokens,
            temperature,
        };

        var response = await _http.PostAsJsonAsync(Endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            AppLogger.Log($"LM Studio {(int)response.StatusCode}.");
            return string.Empty;
        }

        var json = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        return json?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; init; }
    }
    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; init; }
    }
    private sealed class ChatMessage
    {
        [JsonPropertyName("content")] public string? Content { get; init; }
    }
}
