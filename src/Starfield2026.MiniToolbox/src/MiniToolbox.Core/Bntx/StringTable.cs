using System.Collections.Generic;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

public class StringTable
{
	public SortedDictionary<long, string> Strings = new SortedDictionary<long, string>();

	public void Load(BntxFileLoader loader)
	{
		loader.CheckSignature("_STR");
		uint num = loader.ReadUInt32();
		long num2 = loader.ReadInt64();
		uint num3 = loader.ReadUInt32();
	}
}
