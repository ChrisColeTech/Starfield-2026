using System.Collections.Concurrent;
using System.Diagnostics;

namespace MiniToolbox.Core.Pipeline;

/// <summary>
/// Orchestrates parallel extraction of file groups.
/// </summary>
public class ExtractionPipeline
{
    private readonly IFileGroupExtractor _extractor;
    private readonly ExtractionOptions _options;
    private readonly ExtractionWorkspace _workspace;

    public event Action<ExtractionProgress>? OnProgress;

    public ExtractionPipeline(
        IFileGroupExtractor extractor,
        string outputFolder,
        ExtractionOptions? options = null)
    {
        _extractor = extractor;
        _options = options ?? new ExtractionOptions();
        _workspace = new ExtractionWorkspace(outputFolder, _options.KeepRawFiles);
    }

    /// <summary>
    /// Runs the extraction pipeline for all jobs.
    /// </summary>
    public async Task<ExtractionSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        IEnumerable<ExtractionJob> enumerable = _extractor.EnumerateJobs();
        if (!string.IsNullOrEmpty(_options.Filter))
        {
            string filter = _options.Filter;
            enumerable = enumerable.Where(j =>
                j.SourceFiles.Any(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        var jobs = enumerable.ToList();
        var results = new ConcurrentBag<ExtractionResult>();

        int completed = 0;
        int total = jobs.Count;

        // Assign workspace paths to jobs
        foreach (var job in jobs)
        {
            job.TempPath = _workspace.GetJobTempPath(job.Id);
            job.OutputPath = _workspace.GetJobOutputPath(job.Id, job.Name);
        }

        // Process jobs in parallel
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(jobs, parallelOptions, async (job, ct) =>
        {
            var jobStopwatch = Stopwatch.StartNew();
            ExtractionResult result;

            try
            {
                result = await _extractor.ProcessJobAsync(job, ct);
                result.Duration = jobStopwatch.Elapsed;

                // Validate output
                if (result.Success && !_extractor.ValidateJobOutput(job, result))
                {
                    result.Success = false;
                    result.ErrorMessage = "Output validation failed";
                }
            }
            catch (OperationCanceledException)
            {
                result = ExtractionResult.Failed(job.Id, "Cancelled");
                result.Duration = jobStopwatch.Elapsed;
            }
            catch (Exception ex)
            {
                result = ExtractionResult.Failed(job.Id, ex.Message);
                result.Duration = jobStopwatch.Elapsed;

                if (!_options.ContinueOnError)
                    throw;
            }

            results.Add(result);

            int current = Interlocked.Increment(ref completed);
            OnProgress?.Invoke(new ExtractionProgress
            {
                Current = current,
                Total = total,
                JobId = job.Id,
                JobName = job.Name,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                Stats = result.Stats
            });
        });

        stopwatch.Stop();

        var summary = new ExtractionSummary
        {
            TotalJobs = total,
            SuccessCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            TotalDuration = stopwatch.Elapsed,
            Results = results.ToList()
        };

        // Mark workspace as validated if all jobs succeeded
        if (summary.FailedCount == 0)
        {
            _workspace.MarkValidated();
        }

        return summary;
    }

    /// <summary>
    /// Runs extraction for a single job by ID.
    /// </summary>
    public async Task<ExtractionResult> RunSingleAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = _extractor.EnumerateJobs().FirstOrDefault(j => j.Id == jobId);
        if (job == null)
        {
            return ExtractionResult.Failed(jobId, "Job not found");
        }

        job.TempPath = _workspace.GetJobTempPath(job.Id);
        job.OutputPath = _workspace.GetJobOutputPath(job.Id, job.Name);

        var stopwatch = Stopwatch.StartNew();
        var result = await _extractor.ProcessJobAsync(job, cancellationToken);
        result.Duration = stopwatch.Elapsed;

        if (result.Success && !_extractor.ValidateJobOutput(job, result))
        {
            result.Success = false;
            result.ErrorMessage = "Output validation failed";
        }

        if (result.Success)
        {
            _workspace.MarkValidated();
        }

        return result;
    }

    public ExtractionWorkspace Workspace => _workspace;
}

/// <summary>
/// Progress information during extraction.
/// </summary>
public class ExtractionProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Stats { get; set; } = new();

    public double PercentComplete => Total > 0 ? (double)Current / Total * 100 : 0;
}
