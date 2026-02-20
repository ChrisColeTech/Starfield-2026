using System.Buffers;
using System.Buffers.Binary;

namespace MiniToolbox.Core.Pipeline;

/// <summary>
/// Provides streaming file reading utilities to avoid loading entire files into memory.
/// </summary>
public static class StreamingFileReader
{
    private const int DefaultBufferSize = 81920; // 80KB buffer

    /// <summary>
    /// Copies a file to another location using streaming.
    /// </summary>
    public static async Task CopyFileAsync(string source, string destination, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await sourceStream.CopyToAsync(destStream, ct);
    }

    /// <summary>
    /// Reads a portion of a file into a buffer.
    /// </summary>
    public static async Task<byte[]> ReadBytesAsync(string path, long offset, int count, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.RandomAccess);

        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[count];
        int bytesRead = await stream.ReadAsync(buffer, ct);

        if (bytesRead < count)
            Array.Resize(ref buffer, bytesRead);

        return buffer;
    }

    /// <summary>
    /// Reads file header (first N bytes).
    /// </summary>
    public static async Task<byte[]> ReadHeaderAsync(string path, int headerSize = 64, CancellationToken ct = default)
    {
        return await ReadBytesAsync(path, 0, headerSize, ct);
    }

    /// <summary>
    /// Gets file size without reading content.
    /// </summary>
    public static long GetFileSize(string path)
    {
        return new FileInfo(path).Length;
    }

    /// <summary>
    /// Reads a uint32 at specific offset in file.
    /// </summary>
    public static uint ReadUInt32(string path, long offset)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offset, SeekOrigin.Begin);
        Span<byte> buffer = stackalloc byte[4];
        stream.Read(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Processes a file in chunks, calling the processor for each chunk.
    /// </summary>
    public static async Task ProcessInChunksAsync(
        string path,
        int chunkSize,
        Func<ReadOnlyMemory<byte>, long, Task> processor,
        CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            long offset = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, chunkSize), ct)) > 0)
            {
                await processor(buffer.AsMemory(0, bytesRead), offset);
                offset += bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Writes data to file using streaming.
    /// </summary>
    public static async Task WriteBytesAsync(string path, byte[] data, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await stream.WriteAsync(data, ct);
    }

    /// <summary>
    /// Writes text to file using streaming.
    /// </summary>
    public static async Task WriteTextAsync(string path, string text, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var writer = new StreamWriter(path);
        await writer.WriteAsync(text);
    }
}
