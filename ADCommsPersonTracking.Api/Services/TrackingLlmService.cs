using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Anthropic.SDK.Constants;

namespace ADCommsPersonTracking.Api.Services;

public class TrackingLlmService : ITrackingLlmService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<TrackingLlmService> _logger;

    public TrackingLlmService(IConfiguration configuration, ILogger<TrackingLlmService> logger)
    {
        var apiKey = configuration["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic API key not configured");
        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<string> ParseTrackingPromptAsync(string prompt)
    {
        try
        {
            var messages = new List<Message>
            {
                new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new TextContent
                        {
                            Text = $"Parse this person tracking prompt and extract key visual features in a structured format. " +
                                   $"Focus on: clothing colors, accessories, physical items carried, distinctive features. " +
                                   $"Return ONLY a JSON object with fields: colors, accessories, items, features. " +
                                   $"Prompt: {prompt}"
                        }
                    }
                }
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 500,
                Model = "claude-3-5-sonnet-20241022",
                Stream = false,
                Temperature = 0.0m
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            
            var textContent = response.Content?.OfType<TextContent>().FirstOrDefault();
            if (textContent?.Text != null)
            {
                return textContent.Text;
            }

            _logger.LogWarning("Claude returned no content for prompt parsing");
            return "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing prompt with Claude");
            return "{}";
        }
    }

    public async Task<List<string>> ExtractSearchFeaturesAsync(string prompt)
    {
        try
        {
            var messages = new List<Message>
            {
                new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new TextContent
                        {
                            Text = $"Extract the key visual search features from this person tracking prompt. " +
                                   $"Return ONLY a comma-separated list of features (e.g., 'yellow jacket, black hat, suitcase'). " +
                                   $"Prompt: {prompt}"
                        }
                    }
                }
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 200,
                Model = "claude-3-5-sonnet-20241022",
                Stream = false,
                Temperature = 0.0m
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            
            var textContent = response.Content?.OfType<TextContent>().FirstOrDefault();
            if (textContent?.Text != null)
            {
                var featuresText = textContent.Text;
                return featuresText.Split(',')
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToList();
            }

            _logger.LogWarning("Claude returned no features for prompt");
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting features with Claude");
            return new List<string>();
        }
    }

    public async Task<List<string>> MatchDetectionsToPromptAsync(string prompt, List<string> detectionDescriptions)
    {
        try
        {
            var detectionsText = string.Join("\n", detectionDescriptions.Select((d, i) => $"{i}: {d}"));
            
            var messages = new List<Message>
            {
                new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new TextContent
                        {
                            Text = $"You are an object tracking LLM processing user prompts for person detection in train camera footage. " +
                                   $"User prompt: '{prompt}'\n\n" +
                                   $"Detected persons in frame:\n{detectionsText}\n\n" +
                                   $"Analyze which detected persons match the user's description. " +
                                   $"Return ONLY a comma-separated list of detection indices that match (e.g., '0,2' or 'none'). " +
                                   $"Be lenient with partial matches based on available visual features."
                        }
                    }
                }
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 100,
                Model = "claude-3-5-sonnet-20241022",
                Stream = false,
                Temperature = 0.0m
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            
            var textContent = response.Content?.OfType<TextContent>().FirstOrDefault();
            if (textContent?.Text != null)
            {
                var matchText = textContent.Text.Trim().ToLower();
                if (matchText == "none" || string.IsNullOrEmpty(matchText))
                {
                    return new List<string>();
                }

                return matchText.Split(',')
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m))
                    .ToList();
            }

            _logger.LogWarning("Claude returned no matches for detections");
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching detections with Claude");
            return new List<string>();
        }
    }
}
