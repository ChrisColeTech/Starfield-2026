using System;
using System.IO;

namespace MiniToolbox.Core.Bntx;

public class SeekTask : IDisposable
{
	public Stream Stream { get; private set; }

	public long PreviousPosition { get; private set; }

	public SeekTask(Stream stream, long offset, SeekOrigin origin)
	{
		Stream = stream;
		PreviousPosition = stream.Position;
		Stream.Seek(offset, origin);
	}

	public void Dispose()
	{
		Stream.Seek(PreviousPosition, SeekOrigin.Begin);
	}
}
