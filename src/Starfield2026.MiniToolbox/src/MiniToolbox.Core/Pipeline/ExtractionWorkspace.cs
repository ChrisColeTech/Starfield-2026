namespace MiniToolbox.Core.Pipeline;

/// <summary>
/// Manages workspace directories for extraction operations.
/// Handles temp folder lifecycle and output validation.
/// </summary>
public class ExtractionWorkspace : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _outputRoot;
    private readonly bool _keepTemp;
    private bool _disposed;
    private bool _validated;

    public string TempRoot => _tempRoot;
    public string OutputRoot => _outputRoot;

    /// <summary>
    /// Creates a new extraction workspace.
    /// </summary>
    /// <param name="outputRoot">Final output directory</param>
    /// <param name="keepTemp">If true, temp files are kept in .raw subfolder</param>
    public ExtractionWorkspace(string outputRoot, bool keepTemp = true)
    {
        _outputRoot = outputRoot;
        _keepTemp = keepTemp;

        if (keepTemp)
        {
            // Keep raw files in output/.raw/
            _tempRoot = Path.Combine(outputRoot, ".raw");
        }
        else
        {
            // Use system temp with unique folder
            _tempRoot = Path.Combine(Path.GetTempPath(), "MiniToolbox", Guid.NewGuid().ToString("N"));
        }

        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_outputRoot);
    }

    /// <summary>
    /// Gets temp path for a specific job.
    /// </summary>
    public string GetJobTempPath(string jobId)
    {
        string path = Path.Combine(_tempRoot, jobId);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Gets output path for a specific job, using displayName for the folder if provided.
    /// </summary>
    public string GetJobOutputPath(string jobId, string? displayName = null)
    {
        string folderName = !string.IsNullOrWhiteSpace(displayName) ? displayName : jobId;
        string path = Path.Combine(_outputRoot, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Marks the workspace as validated (all jobs completed successfully).
    /// Call this before disposal to prevent temp cleanup.
    /// </summary>
    public void MarkValidated()
    {
        _validated = true;
    }

    /// <summary>
    /// Validates that expected output files exist.
    /// </summary>
    public bool ValidateOutput(string jobId, params string[] expectedFiles)
    {
        string jobOutput = Path.Combine(_outputRoot, jobId);
        foreach (var file in expectedFiles)
        {
            string fullPath = Path.Combine(jobOutput, file);
            if (!File.Exists(fullPath))
                return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Only cleanup temp if not keeping and not validated
        if (!_keepTemp && !_validated)
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                    Directory.Delete(_tempRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
