using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;

namespace ADCommsPersonTracking.Api.Services;

public class InferenceDiagnosticsService : IInferenceDiagnosticsService
{
    private readonly ConcurrentDictionary<string, InferenceDiagnostics> _sessions = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<InferenceDiagnosticsService> _logger;
    private readonly TimeSpan _retentionTime;
    private string? _latestSessionId;

    public bool IsEnabled { get; }

    public InferenceDiagnosticsService(
        IConfiguration configuration,
        ILogger<InferenceDiagnosticsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        IsEnabled = configuration.GetValue("Diagnostics:Enabled", true);
        _retentionTime = TimeSpan.FromMinutes(configuration.GetValue("Diagnostics:RetentionMinutes", 30));

        // Start background cleanup task
        if (IsEnabled)
        {
            _ = Task.Run(CleanupOldSessionsAsync);
        }
    }

    public string StartSession(string trackingId, string prompt)
    {
        if (!IsEnabled)
        {
            return string.Empty;
        }

        var sessionId = $"diag_{Guid.NewGuid():N}";
        var diagnostics = new InferenceDiagnostics
        {
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            TrackingId = trackingId,
            Prompt = prompt
        };

        _sessions[sessionId] = diagnostics;
        _latestSessionId = sessionId;

        _logger.LogDebug("Started diagnostics session {SessionId} for tracking ID {TrackingId}", sessionId, trackingId);

        return sessionId;
    }

    public void EndSession(string sessionId)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var diagnostics))
        {
            _logger.LogDebug("Ended diagnostics session {SessionId} with {LogCount} log entries", 
                sessionId, diagnostics.LogEntries.Count);
        }
    }

    public void AddLogEntry(string sessionId, string level, string category, string message, Dictionary<string, object>? properties = null)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var diagnostics))
        {
            var entry = new DiagnosticLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Properties = properties ?? new Dictionary<string, object>()
            };

            lock (diagnostics.LogEntries)
            {
                diagnostics.LogEntries.Add(entry);
            }
        }
    }

    public void SetSearchCriteria(string sessionId, SearchCriteria criteria)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var diagnostics))
        {
            diagnostics.ExtractedCriteria = new SearchCriteriaDiagnostics
            {
                Colors = criteria.Colors,
                ClothingItems = criteria.ClothingItems,
                Accessories = criteria.Accessories,
                PhysicalAttributes = criteria.PhysicalAttributes,
                HeightInfo = criteria.Height?.OriginalText ?? string.Empty,
                HasAnyCriteria = criteria.Colors.Count > 0 || 
                               criteria.ClothingItems.Count > 0 || 
                               criteria.Accessories.Count > 0 || 
                               criteria.PhysicalAttributes.Count > 0 ||
                               criteria.Height != null
            };
        }
    }

    public void AddImageDiagnostics(string sessionId, ImageProcessingDiagnostics imageDiagnostics)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var diagnostics))
        {
            lock (diagnostics.ImageResults)
            {
                diagnostics.ImageResults.Add(imageDiagnostics);
            }
        }
    }

    public void SetProcessingSummary(string sessionId, ProcessingSummary summary)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var diagnostics))
        {
            diagnostics.Summary = summary;
        }
    }

    public InferenceDiagnostics? GetDiagnostics(string sessionId)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        _sessions.TryGetValue(sessionId, out var diagnostics);
        return diagnostics;
    }

    public InferenceDiagnostics? GetLatestDiagnostics()
    {
        if (!IsEnabled || _latestSessionId == null)
        {
            return null;
        }

        return GetDiagnostics(_latestSessionId);
    }

    private async Task CleanupOldSessionsAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                var cutoffTime = DateTime.UtcNow - _retentionTime;
                var expiredSessions = _sessions
                    .Where(kvp => kvp.Value.Timestamp < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var sessionId in expiredSessions)
                {
                    _sessions.TryRemove(sessionId, out _);
                    _logger.LogDebug("Removed expired diagnostics session {SessionId}", sessionId);
                }

                if (expiredSessions.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired diagnostics sessions", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during diagnostics session cleanup");
            }
        }
    }
}
