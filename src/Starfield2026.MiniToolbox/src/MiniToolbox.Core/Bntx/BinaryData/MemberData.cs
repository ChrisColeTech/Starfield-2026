using System;
using System.Diagnostics;
using System.Reflection;

namespace MiniToolbox.Core.Bntx;

[DebuggerDisplay("MemberData MemberInfo={MemberInfo}")]
internal class MemberData
{
	internal MemberInfo MemberInfo { get; }

	internal Type Type { get; }

	internal BinaryMemberAttribute Attribute { get; }

	internal MemberData(MemberInfo memberInfo, Type type, BinaryMemberAttribute attribute)
	{
		MemberInfo = memberInfo;
		Type = type;
		Attribute = attribute;
	}
}
