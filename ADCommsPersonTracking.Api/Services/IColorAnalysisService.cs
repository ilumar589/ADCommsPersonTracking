using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IColorAnalysisService
{
    Task<PersonColorProfile> AnalyzePersonColorsAsync(byte[] imageBytes, BoundingBox personBox);
    bool MatchesColorCriteria(PersonColorProfile profile, List<string> searchColors);
}
