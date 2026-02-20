using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

public class RelocationTable : IResData
{
	private const string _signature = "_RLT";

	internal uint position { get; set; }

	void IResData.Load(BntxFileLoader loader)
	{
		position = (uint)loader.Position;
		loader.CheckSignature("_RLT");
		int num = loader.ReadInt32();
		int num2 = loader.ReadInt32();
		loader.Seek(4L);
	}
}
