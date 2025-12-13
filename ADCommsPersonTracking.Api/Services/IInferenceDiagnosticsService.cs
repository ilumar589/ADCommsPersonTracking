using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IInferenceDiagnosticsService
{
    /// <summary>
    /// Start a new diagnostics session
    /// </summary>
    string StartSession(string trackingId, string prompt);

    /// <summary>
    /// End a diagnostics session
    /// </summary>
    void EndSession(string sessionId);

    /// <summary>
    /// Add a log entry to the current session
    /// </summary>
    void AddLogEntry(string sessionId, string level, string category, string message, Dictionary<string, object>? properties = null);

    /// <summary>
    /// Set the extracted search criteria for the session
    /// </summary>
    void SetSearchCriteria(string sessionId, SearchCriteria criteria);

    /// <summary>
    /// Add image processing diagnostics
    /// </summary>
    void AddImageDiagnostics(string sessionId, ImageProcessingDiagnostics imageDiagnostics);

    /// <summary>
    /// Set the processing summary for the session
    /// </summary>
    void SetProcessingSummary(string sessionId, ProcessingSummary summary);

    /// <summary>
    /// Record frame retrieval information
    /// </summary>
    void RecordFrameRetrieval(string sessionId, int frameCount, bool success, string? errorMessage = null);

    /// <summary>
    /// Add a warning to the diagnostics session
    /// </summary>
    void AddWarning(string sessionId, string warning);

    /// <summary>
    /// Get diagnostics for a session
    /// </summary>
    InferenceDiagnostics? GetDiagnostics(string sessionId);

    /// <summary>
    /// Get the most recent diagnostics session
    /// </summary>
    InferenceDiagnostics? GetLatestDiagnostics();

    /// <summary>
    /// Check if diagnostics is enabled
    /// </summary>
    bool IsEnabled { get; }
}
