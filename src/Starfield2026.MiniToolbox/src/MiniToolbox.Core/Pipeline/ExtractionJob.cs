namespace MiniToolbox.Core.Pipeline;

/// <summary>
/// Represents a single extraction job (file group).
/// </summary>
public class ExtractionJob
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] SourceFiles { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Temp directory for this job's intermediate files.
    /// </summary>
    public string TempPath { get; set; } = string.Empty;

    /// <summary>
    /// Output directory for this job's final files.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
}

/// <summary>
/// Result of processing a single extraction job.
/// </summary>
public class ExtractionResult
{
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public Dictionary<string, object> Stats { get; set; } = new();
    public TimeSpan Duration { get; set; }

    public static ExtractionResult Failed(string jobId, string error) => new()
    {
        JobId = jobId,
        Success = false,
        ErrorMessage = error
    };

    public static ExtractionResult Succeeded(string jobId, string jobName) => new()
    {
        JobId = jobId,
        JobName = jobName,
        Success = true
    };
}

/// <summary>
/// Summary of batch extraction results.
/// </summary>
public class ExtractionSummary
{
    public int TotalJobs { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ExtractionResult> Results { get; set; } = new();

    public List<ExtractionResult> Succeeded => Results.Where(r => r.Success).ToList();
    public List<ExtractionResult> Failed => Results.Where(r => !r.Success).ToList();
}
