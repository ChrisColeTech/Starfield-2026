using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MiniToolbox.Core.Bntx;

internal static class TypeExtensions
{
	private static readonly TypeInfo _iEnumerableTypeInfo = typeof(IEnumerable).GetTypeInfo();

	internal static bool IsEnumerable(this Type type)
	{
		if (type != typeof(string))
		{
			if (!type.IsArray)
			{
				return _iEnumerableTypeInfo.IsAssignableFrom(type);
			}
			return true;
		}
		return false;
	}

	internal static Type GetEnumerableElementType(this Type type)
	{
		if (type == typeof(string))
		{
			return null;
		}
		if (type.IsArray)
		{
			Type elementType;
			if (type.GetArrayRank() > 1 || (elementType = type.GetElementType()).IsArray)
			{
				throw new NotImplementedException($"Type {type} is a multidimensional array and not supported at the moment.");
			}
			return elementType;
		}
		if (_iEnumerableTypeInfo.IsAssignableFrom(type))
		{
			Type[] interfaces = type.GetTypeInfo().GetInterfaces();
			for (int i = 0; i < interfaces.Length; i++)
			{
				TypeInfo typeInfo = interfaces[i].GetTypeInfo();
				if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				{
					return typeInfo.GetGenericArguments()[0];
				}
			}
		}
		return null;
	}

	internal static bool TryGetEnumerableElementType(this Type type, out Type elementType)
	{
		if (type != typeof(string))
		{
			if (type.IsArray)
			{
				Type elementType2;
				if (type.GetArrayRank() > 1 || (elementType2 = type.GetElementType()).IsArray)
				{
					throw new NotImplementedException($"Type {type} is a multidimensional array and not supported at the moment.");
				}
				elementType = elementType2;
				return true;
			}
			if (_iEnumerableTypeInfo.IsAssignableFrom(type))
			{
				Type[] interfaces = type.GetTypeInfo().GetInterfaces();
				for (int i = 0; i < interfaces.Length; i++)
				{
					TypeInfo typeInfo = interfaces[i].GetTypeInfo();
					if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
					{
						elementType = typeInfo.GetGenericArguments()[0];
						return true;
					}
				}
			}
		}
		elementType = null;
		return false;
	}
}
