using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IPromptFeatureExtractor
{
    SearchCriteria ExtractFeatures(string prompt);
}
