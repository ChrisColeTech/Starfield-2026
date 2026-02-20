namespace MiniToolbox.Core.Bntx;

public class Offset
{
	public BinaryDataWriter Writer { get; private set; }

	public uint Position { get; private set; }

	public Offset(BinaryDataWriter writer)
	{
		Writer = writer;
		Position = (uint)Writer.Position;
		Writer.Position += 4L;
	}

	public void Satisfy()
	{
		Satisfy((int)Writer.Position);
	}

	public void Satisfy(int value)
	{
		uint num = (uint)Writer.Position;
		Writer.Position = Position;
		Writer.Write(value);
		Writer.Position = num;
	}
}
