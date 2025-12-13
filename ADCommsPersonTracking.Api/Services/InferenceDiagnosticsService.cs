using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ADCommsPersonTracking.Api.Services;

public class InferenceDiagnosticsService : IInferenceDiagnosticsService
{
    private readonly ConcurrentDictionary<string, InferenceDiagnostics> _sessions = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<InferenceDiagnosticsService> _logger;
    private readonly TimeSpan _retentionTime;
    private string? _latestSessionId;

    // Channel for async processing of diagnostics data
    private readonly Channel<DiagnosticsMessage> _diagnosticsChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public bool IsEnabled { get; }

    public InferenceDiagnosticsService(
        IConfiguration configuration,
        ILogger<InferenceDiagnosticsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        IsEnabled = configuration.GetValue("Diagnostics:Enabled", true);
        _retentionTime = TimeSpan.FromMinutes(configuration.GetValue("Diagnostics:RetentionMinutes", 30));
        _cancellationTokenSource = new CancellationTokenSource();

        // Create unbounded channel for high-throughput diagnostics
        // Using unbounded to avoid blocking the caller thread
        _diagnosticsChannel = Channel.CreateUnbounded<DiagnosticsMessage>(new UnboundedChannelOptions
        {
            SingleReader = true, // Only one background task will read from this channel
            SingleWriter = false // Multiple threads may write diagnostics
        });

        // Start background tasks
        if (IsEnabled)
        {
            _ = Task.Run(() => ProcessDiagnosticsAsync(_cancellationTokenSource.Token));
            _ = Task.Run(() => CleanupOldSessionsAsync(_cancellationTokenSource.Token));
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

        // Send end session message to channel
        var message = new DiagnosticsMessage
        {
            Type = DiagnosticsMessageType.EndSession,
            SessionId = sessionId
        };

        _diagnosticsChannel.Writer.TryWrite(message);
    }

    public void AddLogEntry(string sessionId, string level, string category, string message, Dictionary<string, object>? properties = null)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        // Send log entry to channel for async processing - non-blocking!
        var diagnosticsMessage = new DiagnosticsMessage
        {
            Type = DiagnosticsMessageType.LogEntry,
            SessionId = sessionId,
            LogEntry = new DiagnosticLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Properties = properties ?? new Dictionary<string, object>()
            }
        };

        _diagnosticsChannel.Writer.TryWrite(diagnosticsMessage);
    }

    public void SetSearchCriteria(string sessionId, SearchCriteria criteria)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        // Send search criteria to channel for async processing
        var message = new DiagnosticsMessage
        {
            Type = DiagnosticsMessageType.SearchCriteria,
            SessionId = sessionId,
            SearchCriteria = criteria
        };

        _diagnosticsChannel.Writer.TryWrite(message);
    }

    public void AddImageDiagnostics(string sessionId, ImageProcessingDiagnostics imageDiagnostics)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        // Send image diagnostics to channel for async processing
        var message = new DiagnosticsMessage
        {
            Type = DiagnosticsMessageType.ImageDiagnostics,
            SessionId = sessionId,
            ImageDiagnostics = imageDiagnostics
        };

        _diagnosticsChannel.Writer.TryWrite(message);
    }

    public void SetProcessingSummary(string sessionId, ProcessingSummary summary)
    {
        if (!IsEnabled || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        // Send processing summary to channel for async processing
        var message = new DiagnosticsMessage
        {
            Type = DiagnosticsMessageType.ProcessingSummary,
            SessionId = sessionId,
            ProcessingSummary = summary
        };

        _diagnosticsChannel.Writer.TryWrite(message);
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

    /// <summary>
    /// Background task that processes diagnostics messages from the channel.
    /// This runs on a separate thread and never blocks the caller.
    /// </summary>
    private async Task ProcessDiagnosticsAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _diagnosticsChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (!_sessions.TryGetValue(message.SessionId, out var diagnostics))
                {
                    continue; // Session not found, skip
                }

                switch (message.Type)
                {
                    case DiagnosticsMessageType.LogEntry:
                        if (message.LogEntry != null)
                        {
                            lock (diagnostics.LogEntries)
                            {
                                diagnostics.LogEntries.Add(message.LogEntry);
                            }
                        }
                        break;

                    case DiagnosticsMessageType.SearchCriteria:
                        if (message.SearchCriteria != null)
                        {
                            diagnostics.ExtractedCriteria = new SearchCriteriaDiagnostics
                            {
                                Colors = message.SearchCriteria.Colors,
                                ClothingItems = message.SearchCriteria.ClothingItems,
                                Accessories = message.SearchCriteria.Accessories,
                                PhysicalAttributes = message.SearchCriteria.PhysicalAttributes,
                                HeightInfo = message.SearchCriteria.Height?.OriginalText ?? string.Empty,
                                HasAnyCriteria = message.SearchCriteria.Colors.Count > 0 || 
                                               message.SearchCriteria.ClothingItems.Count > 0 || 
                                               message.SearchCriteria.Accessories.Count > 0 || 
                                               message.SearchCriteria.PhysicalAttributes.Count > 0 ||
                                               message.SearchCriteria.Height != null
                            };
                        }
                        break;

                    case DiagnosticsMessageType.ImageDiagnostics:
                        if (message.ImageDiagnostics != null)
                        {
                            lock (diagnostics.ImageResults)
                            {
                                diagnostics.ImageResults.Add(message.ImageDiagnostics);
                            }
                        }
                        break;

                    case DiagnosticsMessageType.ProcessingSummary:
                        if (message.ProcessingSummary != null)
                        {
                            diagnostics.Summary = message.ProcessingSummary;
                        }
                        break;

                    case DiagnosticsMessageType.EndSession:
                        _logger.LogDebug("Ended diagnostics session {SessionId} with {LogCount} log entries", 
                            message.SessionId, diagnostics.LogEntries.Count);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing diagnostics message for session {SessionId}", message.SessionId);
            }
        }
    }

    private async Task CleanupOldSessionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

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
            catch (OperationCanceledException)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during diagnostics session cleanup");
            }
        }
    }
}

/// <summary>
/// Internal message type for channel-based diagnostics processing
/// </summary>
internal enum DiagnosticsMessageType
{
    LogEntry,
    SearchCriteria,
    ImageDiagnostics,
    ProcessingSummary,
    EndSession
}

/// <summary>
/// Internal message structure for channel-based diagnostics
/// </summary>
internal class DiagnosticsMessage
{
    public DiagnosticsMessageType Type { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DiagnosticLogEntry? LogEntry { get; set; }
    public SearchCriteria? SearchCriteria { get; set; }
    public ImageProcessingDiagnostics? ImageDiagnostics { get; set; }
    public ProcessingSummary? ProcessingSummary { get; set; }
}
