using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

[DebuggerDisplay("BinaryDataWriter, Position={Position}")]
public class BinaryDataWriter : BinaryWriter
{
	private ByteOrder _byteOrder;

	public ByteOrder ByteOrder
	{
		get
		{
			return _byteOrder;
		}
		set
		{
			_byteOrder = value;
			NeedsReversion = _byteOrder != ByteOrderHelper.SystemByteOrder;
		}
	}

	public Encoding Encoding { get; private set; }

	public bool NeedsReversion { get; private set; }

	public long Position
	{
		get
		{
			return BaseStream.Position;
		}
		set
		{
			BaseStream.Position = value;
		}
	}

	public BinaryDataWriter(Stream output)
		: this(output, new UTF8Encoding(), leaveOpen: false)
	{
	}

	public BinaryDataWriter(Stream output, bool leaveOpen)
		: this(output, new UTF8Encoding(), leaveOpen)
	{
	}

	public BinaryDataWriter(Stream output, Encoding encoding)
		: this(output, encoding, leaveOpen: false)
	{
	}

	public BinaryDataWriter(Stream output, Encoding encoding, bool leaveOpen)
		: base(output, encoding, leaveOpen)
	{
		Encoding = encoding;
		ByteOrder = ByteOrderHelper.SystemByteOrder;
	}

	public void Align(int alignment)
	{
		Seek((-Position % alignment + alignment) % alignment);
	}

	public Offset ReserveOffset()
	{
		return new Offset(this);
	}

	public Offset[] ReserveOffset(int count)
	{
		Offset[] array = new Offset[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = ReserveOffset();
		}
		return array;
	}

	public long Seek(long offset)
	{
		return Seek(offset, SeekOrigin.Current);
	}

	public long Seek(long offset, SeekOrigin origin)
	{
		return BaseStream.Seek(offset, origin);
	}

	public SeekTask TemporarySeek()
	{
		return TemporarySeek(0L, SeekOrigin.Current);
	}

	public SeekTask TemporarySeek(long offset)
	{
		return TemporarySeek(offset, SeekOrigin.Current);
	}

	public SeekTask TemporarySeek(long offset, SeekOrigin origin)
	{
		return new SeekTask(BaseStream, offset, origin);
	}

	public void Write(bool value, BinaryBooleanFormat format)
	{
		switch (format)
		{
		case BinaryBooleanFormat.NonZeroByte:
			base.Write(value);
			break;
		case BinaryBooleanFormat.NonZeroWord:
			Write((short)(value ? 1 : 0));
			break;
		case BinaryBooleanFormat.NonZeroDword:
			Write(value ? 1 : 0);
			break;
		default:
			throw new ArgumentOutOfRangeException("format", "The specified binary boolean format is invalid.");
		}
	}

	public void Write(IEnumerable<bool> values)
	{
		foreach (bool value in values)
		{
			Write(value);
		}
	}

	public void Write(IEnumerable<bool> values, BinaryBooleanFormat format)
	{
		foreach (bool value in values)
		{
			Write(value, format);
		}
	}

	public void Write(DateTime value)
	{
		Write(value, BinaryDateTimeFormat.NetTicks);
	}

	public void Write(DateTime value, BinaryDateTimeFormat format)
	{
		switch (format)
		{
		case BinaryDateTimeFormat.CTime:
			Write((uint)(new DateTime(1970, 1, 1) - value.ToLocalTime()).TotalSeconds);
			break;
		case BinaryDateTimeFormat.NetTicks:
			Write(value.Ticks);
			break;
		default:
			throw new ArgumentOutOfRangeException("format", "The specified binary date time format is invalid.");
		}
	}

	public void Write(IEnumerable<DateTime> values)
	{
		foreach (DateTime value in values)
		{
			Write(value, BinaryDateTimeFormat.NetTicks);
		}
	}

	public void Write(IEnumerable<DateTime> values, BinaryDateTimeFormat format)
	{
		foreach (DateTime value in values)
		{
			Write(value, format);
		}
	}

	public override void Write(decimal value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = DecimalToBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<decimal> values)
	{
		foreach (decimal value in values)
		{
			Write(value);
		}
	}

	public override void Write(double value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<double> values)
	{
		foreach (double value in values)
		{
			Write(value);
		}
	}

	public void Write<T>(T value, bool strict) where T : struct, IComparable, IFormattable
	{
		WriteEnum(typeof(T), value, strict);
	}

	public void Write<T>(IEnumerable<T> values, bool strict) where T : struct, IComparable, IFormattable
	{
		foreach (T value in values)
		{
			Write(value, strict);
		}
	}

	public override void Write(short value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<short> values)
	{
		foreach (short value in values)
		{
			Write(value);
		}
	}

	public override void Write(int value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<int> values)
	{
		foreach (int value in values)
		{
			Write(value);
		}
	}

	public override void Write(long value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<long> values)
	{
		foreach (long value in values)
		{
			Write(value);
		}
	}

	public void WriteObject(object value)
	{
		if (value != null)
		{
			WriteObject(null, BinaryMemberAttribute.Default, value.GetType(), value);
		}
	}

	public override void Write(float value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<float> values)
	{
		foreach (float value in values)
		{
			Write(value);
		}
	}

	public void Write(string value, BinaryStringFormat format)
	{
		Write(value, format, Encoding);
	}

	public void Write(string value, BinaryStringFormat format, Encoding encoding)
	{
		switch (format)
		{
		case BinaryStringFormat.ByteLengthPrefix:
			WriteByteLengthPrefixString(value, encoding);
			break;
		case BinaryStringFormat.WordLengthPrefix:
			WriteWordLengthPrefixString(value, encoding);
			break;
		case BinaryStringFormat.DwordLengthPrefix:
			WriteDwordLengthPrefixString(value, encoding);
			break;
		case BinaryStringFormat.VariableLengthPrefix:
			WriteVariableLengthPrefixString(value, encoding);
			break;
		case BinaryStringFormat.ZeroTerminated:
			WriteZeroTerminatedString(value, encoding);
			break;
		case BinaryStringFormat.NoPrefixOrTermination:
			WriteNoPrefixOrTerminationString(value, encoding);
			break;
		default:
			throw new ArgumentOutOfRangeException("format", "The specified binary string format is invalid.");
		}
	}

	public void Write(IEnumerable<string> values)
	{
		foreach (string value in values)
		{
			Write(value);
		}
	}

	public void Write(IEnumerable<string> values, BinaryStringFormat format)
	{
		foreach (string value in values)
		{
			Write(value, format);
		}
	}

	public void Write(IEnumerable<string> values, BinaryStringFormat format, Encoding encoding)
	{
		foreach (string value in values)
		{
			Write(value, format, encoding);
		}
	}

	public override void Write(ushort value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<ushort> values)
	{
		foreach (ushort value in values)
		{
			Write(value);
		}
	}

	public override void Write(uint value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<uint> values)
	{
		foreach (uint value in values)
		{
			Write(value);
		}
	}

	public override void Write(ulong value)
	{
		if (NeedsReversion)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			WriteReversed(bytes);
		}
		else
		{
			base.Write(value);
		}
	}

	public void Write(IEnumerable<ulong> values)
	{
		foreach (ulong value in values)
		{
			Write(value);
		}
	}

	private void WriteReversed(byte[] bytes)
	{
		Array.Reverse(bytes);
		base.Write(bytes);
	}

	private byte[] DecimalToBytes(decimal value)
	{
		byte[] array = new byte[16];
		Buffer.BlockCopy(decimal.GetBits(value), 0, array, 0, 16);
		return array;
	}

	private void WriteEnum(Type type, object value, bool strict)
	{
		if (strict && !EnumExtensions.IsValid(type, value))
		{
			throw new InvalidDataException($"Value {value} to write is not defined in the given enum type {type}.");
		}
		WriteObject(null, BinaryMemberAttribute.Default, Enum.GetUnderlyingType(type), value);
	}

	private void WriteObject(object instance, BinaryMemberAttribute attribute, Type type, object value)
	{
		if (attribute.Converter == null)
		{
			if (value == null)
			{
				return;
			}
			if (type == typeof(string))
			{
				Write((string)value, attribute.StringFormat);
				return;
			}
			if (type.TryGetEnumerableElementType(out var elementType))
			{
				foreach (object item in (IEnumerable)value)
				{
					WriteObject(null, BinaryMemberAttribute.Default, elementType, item);
				}
				return;
			}
			if (type == typeof(bool))
			{
				Write((bool)value, attribute.BooleanFormat);
			}
			else if (type == typeof(byte))
			{
				Write((byte)value);
			}
			else if (type == typeof(DateTime))
			{
				Write((DateTime)value, attribute.DateTimeFormat);
			}
			else if (type == typeof(decimal))
			{
				Write((decimal)value);
			}
			else if (type == typeof(double))
			{
				Write((double)value);
			}
			else if (type == typeof(short))
			{
				Write((short)value);
			}
			else if (type == typeof(int))
			{
				Write((int)value);
			}
			else if (type == typeof(long))
			{
				Write((long)value);
			}
			else if (type == typeof(sbyte))
			{
				Write((sbyte)value);
			}
			else if (type == typeof(float))
			{
				Write((float)value);
			}
			else if (type == typeof(ushort))
			{
				Write((ushort)value);
			}
			else if (type == typeof(uint))
			{
				Write((uint)value);
			}
			else if (type == typeof(ulong))
			{
				Write((uint)value);
			}
			else if (type.GetTypeInfo().IsEnum)
			{
				WriteEnum(type, value, attribute.Strict);
			}
			else
			{
				WriteCustomObject(type, value, Position);
			}
		}
		else
		{
			BinaryConverterCache.GetConverter(attribute.Converter).Write(this, instance, attribute, value);
		}
	}

	private void WriteCustomObject(Type type, object instance, long startOffset)
	{
		TypeData typeData = TypeData.GetTypeData(type);
		if (typeData.Attribute.Inherit && typeData.TypeInfo.BaseType != null)
		{
			WriteCustomObject(typeData.TypeInfo.BaseType, instance, startOffset);
		}
		foreach (MemberData member in typeData.Members)
		{
			if (member.Attribute.OffsetOrigin == OffsetOrigin.Begin)
			{
				Position = startOffset + member.Attribute.Offset;
			}
			else
			{
				Position += member.Attribute.Offset;
			}
			MemberInfo memberInfo = member.MemberInfo;
			if ((object)memberInfo != null)
			{
				object value;
				if (!(memberInfo is FieldInfo fieldInfo))
				{
					if (!(memberInfo is PropertyInfo propertyInfo))
					{
						goto IL_00cc;
					}
					value = propertyInfo.GetValue(instance);
				}
				else
				{
					value = fieldInfo.GetValue(instance);
				}
				if (member.Type.GetEnumerableElementType() == null)
				{
					WriteObject(instance, member.Attribute, member.Type, value);
					continue;
				}
				foreach (object item in (IEnumerable)value)
				{
					WriteObject(instance, member.Attribute, member.Type, item);
				}
				continue;
			}
			goto IL_00cc;
			IL_00cc:
			throw new InvalidOperationException($"Tried to write an invalid member {member.MemberInfo}.");
		}
	}

	private void WriteByteLengthPrefixString(string value, Encoding encoding)
	{
		Write((byte)value.Length);
		Write(encoding.GetBytes(value));
	}

	private void WriteWordLengthPrefixString(string value, Encoding encoding)
	{
		Write((short)value.Length);
		Write(encoding.GetBytes(value));
	}

	private void WriteDwordLengthPrefixString(string value, Encoding encoding)
	{
		Write(value.Length);
		Write(encoding.GetBytes(value));
	}

	private void WriteVariableLengthPrefixString(string value, Encoding encoding)
	{
		Write7BitEncodedInt(value.Length);
		Write(encoding.GetBytes(value));
	}

	private void WriteZeroTerminatedString(string value, Encoding encoding)
	{
		Write(encoding.GetBytes(value));
		Write((byte)0);
	}

	private void WriteNoPrefixOrTerminationString(string value, Encoding encoding)
	{
		Write(encoding.GetBytes(value));
	}
}
