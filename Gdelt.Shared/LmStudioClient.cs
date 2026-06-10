using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GdeltSearchUI;

internal static class LmStudioClient
{
    private const string Endpoint = "http://10.0.0.119:1234/v1/chat/completions";

    // Gemma 4 thinking overhead — the model burns ~400 tokens on reasoning before the answer
    private const int ThinkingOverhead = 512;

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };

    // Gemma 4 wraps thinking in <|channel>thought ... <channel|>  — the closing tag is the answer boundary
    private static readonly Regex _channelClose = new(@"<channel\|>", RegexOptions.Compiled);
    private static readonly Regex _channelThought = new(@"<\|channel>thought[\s\S]*?<channel\|>", RegexOptions.Compiled);

    public static async Task<string> CallAsync(
        string systemPrompt, string userMessage, int maxTokens, double temperature,
        CancellationToken ct = default)
    {
        var payload = new
        {
            model = "google/gemma-4-e4b",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage },
            },
            max_tokens = maxTokens + ThinkingOverhead,
            temperature,
        };

        var response = await _http.PostAsJsonAsync(Endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            AppLogger.Log($"LM Studio {(int)response.StatusCode}.");
            return string.Empty;
        }

        var json = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        var content = json?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return StripThinking(content);
    }

    private static string StripThinking(string content)
    {
        // Gemma 4: <|channel>thought ... <channel|>ANSWER — take everything after the closing tag
        var m = _channelClose.Match(content);
        if (m.Success)
            return content[(m.Index + m.Length)..].Trim();

        // Fallback: strip the whole thought block if close tag is missing (truncated response)
        return _channelThought.Replace(content, "").Trim();
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
