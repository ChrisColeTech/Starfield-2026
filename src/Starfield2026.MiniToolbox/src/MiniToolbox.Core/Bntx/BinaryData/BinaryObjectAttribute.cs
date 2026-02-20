using System;

namespace MiniToolbox.Core.Bntx;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class BinaryObjectAttribute : Attribute
{
	public bool Inherit { get; set; }

	public bool Explicit { get; set; }
}
