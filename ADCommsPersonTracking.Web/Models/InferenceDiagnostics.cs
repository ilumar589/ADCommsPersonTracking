namespace ADCommsPersonTracking.Web.Models;

// These models mirror the API models for diagnostics
public class InferenceDiagnostics
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string TrackingId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string RawPrompt { get; set; } = string.Empty;
    public int FramesRetrieved { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<DiagnosticLogEntry> LogEntries { get; set; } = new();
    public SearchCriteriaDiagnostics ExtractedCriteria { get; set; } = new();
    public List<ImageProcessingDiagnostics> ImageResults { get; set; } = new();
    public ProcessingSummary Summary { get; set; } = new();
}

public class DiagnosticLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class SearchCriteriaDiagnostics
{
    public List<string> Colors { get; set; } = new();
    public List<string> ClothingItems { get; set; } = new();
    public List<string> Accessories { get; set; } = new();
    public List<string> PhysicalAttributes { get; set; } = new();
    public string HeightInfo { get; set; } = string.Empty;
    public bool HasAnyCriteria { get; set; }
}

public class ImageProcessingDiagnostics
{
    public int ImageIndex { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<YoloDetectionDiagnostics> AllYoloDetections { get; set; } = new();
    public List<PersonDetectionDiagnostics> PersonAnalysis { get; set; } = new();
}

public class YoloDetectionDiagnostics
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBoxDiagnostics BoundingBox { get; set; } = new();
}

public class BoundingBoxDiagnostics
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float Confidence { get; set; }
}

public class PersonDetectionDiagnostics
{
    public int PersonIndex { get; set; }
    public BoundingBoxDiagnostics PersonBox { get; set; } = new();
    public ColorAnalysisDiagnostics ColorAnalysis { get; set; } = new();
    public AccessoryMatchingDiagnostics AccessoryMatching { get; set; } = new();
    public CriteriaMatchingDiagnostics CriteriaMatching { get; set; } = new();
    public bool WasIncludedInResults { get; set; }
    public string ExclusionReason { get; set; } = string.Empty;
}

public class ColorAnalysisDiagnostics
{
    public List<string> UpperBodyColors { get; set; } = new();
    public List<string> LowerBodyColors { get; set; } = new();
    public List<string> OverallColors { get; set; } = new();
}

public class AccessoryMatchingDiagnostics
{
    public List<AccessoryAssociationAttempt> AssociationAttempts { get; set; } = new();
    public List<string> AssociatedAccessories { get; set; } = new();
    public List<string> AssociatedClothing { get; set; } = new();
}

public class AccessoryAssociationAttempt
{
    public string AccessoryType { get; set; } = string.Empty;
    public float AccessoryConfidence { get; set; }
    public BoundingBoxDiagnostics AccessoryBox { get; set; } = new();
    public float IoUScore { get; set; }
    public bool WithinExtendedBounds { get; set; }
    public bool WasAssociated { get; set; }
    public string AssociationReason { get; set; } = string.Empty;
}

public class CriteriaMatchingDiagnostics
{
    public bool MatchesColors { get; set; }
    public string ColorMatchDetails { get; set; } = string.Empty;
    public bool MatchesAccessories { get; set; }
    public string AccessoryMatchDetails { get; set; } = string.Empty;
    public bool MatchesPhysical { get; set; }
    public string PhysicalMatchDetails { get; set; } = string.Empty;
    public bool OverallMatch { get; set; }
}

public class ProcessingSummary
{
    public int TotalImagesProcessed { get; set; }
    public int TotalPersonsDetected { get; set; }
    public int TotalAccessoriesDetected { get; set; }
    public int PersonsMatchingCriteria { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}

public class TrackByIdWithDiagnosticsResponse : TrackByIdJobResponse
{
    public string DiagnosticsSessionId { get; set; } = string.Empty;
}
