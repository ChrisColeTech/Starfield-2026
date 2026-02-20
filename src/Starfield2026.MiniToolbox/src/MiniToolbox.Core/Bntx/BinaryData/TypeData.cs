using System;
using System.Collections.Generic;
using System.Reflection;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

internal class TypeData
{
	private static readonly Dictionary<Type, TypeData> _cache = new Dictionary<Type, TypeData>();

	internal Type Type { get; }

	internal TypeInfo TypeInfo { get; }

	internal BinaryObjectAttribute Attribute { get; }

	internal ConstructorInfo Constructor { get; }

	internal List<MemberData> Members { get; }

	private TypeData(Type type)
	{
		Type = type;
		TypeInfo = Type.GetTypeInfo();
		Attribute = TypeInfo.GetCustomAttribute<BinaryObjectAttribute>() ?? new BinaryObjectAttribute();
		Members = new List<MemberData>();
		MemberInfo[] members = TypeInfo.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MemberInfo memberInfo in members)
		{
			if ((object)memberInfo == null)
			{
				continue;
			}
			if (!(memberInfo is ConstructorInfo constructorInfo))
			{
				if (!(memberInfo is FieldInfo fieldInfo))
				{
					if (memberInfo is PropertyInfo propertyInfo)
					{
						PropertyInfo prop = propertyInfo;
						ValidatePropertyInfo(prop);
					}
				}
				else
				{
					FieldInfo field = fieldInfo;
					ValidateFieldInfo(field);
				}
			}
			else
			{
				ConstructorInfo constructorInfo2 = constructorInfo;
				if (constructorInfo2.GetParameters().Length == 0)
				{
					Constructor = constructorInfo2;
				}
			}
		}
	}

	internal static TypeData GetTypeData(Type type)
	{
		if (!_cache.TryGetValue(type, out var value))
		{
			value = new TypeData(type);
			_cache.Add(type, value);
		}
		return value;
	}

	internal object GetInstance()
	{
		if (TypeInfo.IsValueType)
		{
			return Activator.CreateInstance(Type);
		}
		if (Constructor == null)
		{
			throw new MissingMethodException($"No parameterless constructor found for {Type}.");
		}
		return Constructor.Invoke(null);
	}

	private void ValidateFieldInfo(FieldInfo field)
	{
		BinaryMemberAttribute customAttribute = field.GetCustomAttribute<BinaryMemberAttribute>();
		bool num = customAttribute != null;
		customAttribute = customAttribute ?? new BinaryMemberAttribute();
		if (num || (!Attribute.Explicit && field.IsPublic))
		{
			if (field.FieldType.IsEnumerable() && customAttribute.Length <= 0)
			{
				throw new InvalidOperationException(string.Format("Field {0} requires an element count specified with a {1}.", field, "BinaryMemberAttribute"));
			}
			Members.Add(new MemberData(field, field.FieldType, customAttribute));
		}
	}

	private void ValidatePropertyInfo(PropertyInfo prop)
	{
		BinaryMemberAttribute customAttribute = prop.GetCustomAttribute<BinaryMemberAttribute>();
		bool num = customAttribute != null;
		customAttribute = customAttribute ?? new BinaryMemberAttribute();
		if (num && (prop.GetMethod == null || prop.SetMethod == null))
		{
			throw new InvalidOperationException($"Getter and setter on property {prop} not found.");
		}
		if (!num)
		{
			if (Attribute.Explicit)
			{
				return;
			}
			MethodInfo getMethod = prop.GetMethod;
			if ((object)getMethod == null || !getMethod.IsPublic)
			{
				return;
			}
			MethodInfo setMethod = prop.SetMethod;
			if ((object)setMethod == null || !setMethod.IsPublic)
			{
				return;
			}
		}
		if (prop.PropertyType.IsEnumerable() && customAttribute.Length <= 0)
		{
			throw new InvalidOperationException(string.Format("Property {0} requires an element count specified with a {1}.", prop, "BinaryMemberAttribute"));
		}
		Members.Add(new MemberData(prop, prop.PropertyType, customAttribute));
	}
}
