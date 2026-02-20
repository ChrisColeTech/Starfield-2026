using System;

namespace MiniToolbox.Core.Bntx;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class BinaryMemberAttribute : Attribute
{
	internal static readonly BinaryMemberAttribute Default = new BinaryMemberAttribute();

	public int Offset { get; set; }

	public OffsetOrigin OffsetOrigin { get; set; }

	public BinaryBooleanFormat BooleanFormat { get; set; }

	public BinaryDateTimeFormat DateTimeFormat { get; set; }

	public BinaryStringFormat StringFormat { get; set; }

	public int Length { get; set; }

	public bool Strict { get; set; }

	public Type Converter { get; set; }
}
