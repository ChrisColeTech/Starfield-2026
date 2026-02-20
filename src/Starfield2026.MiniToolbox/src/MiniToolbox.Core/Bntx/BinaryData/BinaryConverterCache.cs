using System;
using System.Collections.Generic;

namespace MiniToolbox.Core.Bntx;

internal static class BinaryConverterCache
{
	private static readonly Dictionary<Type, IBinaryConverter> _cache = new Dictionary<Type, IBinaryConverter>();

	internal static IBinaryConverter GetConverter(Type type)
	{
		if (!_cache.TryGetValue(type, out var value))
		{
			value = (IBinaryConverter)Activator.CreateInstance(type);
			_cache.Add(type, value);
		}
		return value;
	}
}
