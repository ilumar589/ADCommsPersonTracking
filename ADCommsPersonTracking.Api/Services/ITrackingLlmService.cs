namespace ADCommsPersonTracking.Api.Services;

public interface ITrackingLlmService
{
    Task<string> ParseTrackingPromptAsync(string prompt);
    Task<List<string>> ExtractSearchFeaturesAsync(string prompt);
    Task<List<string>> MatchDetectionsToPromptAsync(string prompt, List<string> detectionDescriptions);
}
