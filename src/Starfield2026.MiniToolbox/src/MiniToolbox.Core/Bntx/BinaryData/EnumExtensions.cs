using System;
using System.Linq;
using System.Reflection;

namespace MiniToolbox.Core.Bntx;

internal static class EnumExtensions
{
	internal static bool IsValid(Type type, object value)
	{
		bool flag = Enum.IsDefined(type, value);
		if (!flag)
		{
			object[] customAttributes = type.GetTypeInfo().GetCustomAttributes(typeof(FlagsAttribute), inherit: true);
			if (customAttributes != null && customAttributes.Any())
			{
				long num = 0L;
				foreach (object value2 in Enum.GetValues(type))
				{
					num |= Convert.ToInt64(value2);
				}
				long num2 = Convert.ToInt64(value);
				flag = (num & num2) == num2;
			}
		}
		return flag;
	}
}
