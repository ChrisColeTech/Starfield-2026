namespace MiniToolbox.Core.Pipeline;

/// <summary>
/// Interface for file group extractors.
/// Implement this to integrate with the extraction pipeline.
/// </summary>
public interface IFileGroupExtractor
{
    /// <summary>
    /// Enumerates all available jobs (file groups) for extraction.
    /// </summary>
    IEnumerable<ExtractionJob> EnumerateJobs();

    /// <summary>
    /// Processes a single extraction job.
    /// Should write intermediate files to job.TempPath and final output to job.OutputPath.
    /// </summary>
    Task<ExtractionResult> ProcessJobAsync(ExtractionJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a job's output is complete and correct.
    /// </summary>
    bool ValidateJobOutput(ExtractionJob job, ExtractionResult result);
}

/// <summary>
/// Options for extraction pipeline.
/// </summary>
public class ExtractionOptions
{
    /// <summary>
    /// Maximum number of parallel jobs. Default is processor count.
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Keep intermediate/raw files in output. Default is true.
    /// </summary>
    public bool KeepRawFiles { get; set; } = true;

    /// <summary>
    /// Continue processing remaining jobs if one fails. Default is true.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Output format for models.
    /// </summary>
    public string OutputFormat { get; set; } = "obj";

    /// <summary>
    /// Animation export mode: "split" or "baked".
    /// </summary>
    public string AnimationMode { get; set; } = "split";

    /// <summary>
    /// Optional filter string. When set, only jobs whose source files contain this string are processed.
    /// </summary>
    public string? Filter { get; set; }
}
