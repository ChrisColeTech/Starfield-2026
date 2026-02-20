using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

[DebuggerDisplay("BinaryDataReader, Position={Position}")]
public class BinaryDataReader : BinaryReader
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

	public bool EndOfStream => BaseStream.Position >= BaseStream.Length;

	public long Length => BaseStream.Length;

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

	public BinaryDataReader(Stream input)
		: this(input, new UTF8Encoding(), leaveOpen: false)
	{
	}

	public BinaryDataReader(Stream input, bool leaveOpen)
		: this(input, new UTF8Encoding(), leaveOpen)
	{
	}

	public BinaryDataReader(Stream input, Encoding encoding)
		: this(input, encoding, leaveOpen: false)
	{
	}

	public BinaryDataReader(Stream input, Encoding encoding, bool leaveOpen)
		: base(input, encoding, leaveOpen)
	{
		Encoding = encoding;
		ByteOrder = ByteOrderHelper.SystemByteOrder;
	}

	public void Align(int alignment)
	{
		Seek((-Position % alignment + alignment) % alignment);
	}

	public bool ReadBoolean(BinaryBooleanFormat format)
	{
		return format switch
		{
			BinaryBooleanFormat.NonZeroByte => base.ReadBoolean(), 
			BinaryBooleanFormat.NonZeroWord => ReadInt16() != 0, 
			BinaryBooleanFormat.NonZeroDword => ReadInt32() != 0, 
			_ => throw new ArgumentOutOfRangeException("format", "The specified binary boolean format is invalid."), 
		};
	}

	public bool[] ReadBooleans(int count)
	{
		return ReadMultiple(count, base.ReadBoolean);
	}

	public bool[] ReadBooleans(int count, BinaryBooleanFormat format)
	{
		bool[] array = new bool[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadBoolean(format);
		}
		return array;
	}

	public DateTime ReadDateTime()
	{
		return ReadDateTime(BinaryDateTimeFormat.NetTicks);
	}

	public DateTime ReadDateTime(BinaryDateTimeFormat format)
	{
		return format switch
		{
			BinaryDateTimeFormat.CTime => new DateTime(1970, 1, 1).ToLocalTime().AddSeconds(ReadUInt32()), 
			BinaryDateTimeFormat.NetTicks => new DateTime(ReadInt64()), 
			_ => throw new ArgumentOutOfRangeException("format", "The specified binary date time format is invalid."), 
		};
	}

	public DateTime[] ReadDateTimes(int count)
	{
		DateTime[] array = new DateTime[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadDateTime();
		}
		return array;
	}

	public DateTime[] ReadDateTimes(int count, BinaryDateTimeFormat format)
	{
		DateTime[] array = new DateTime[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadDateTime(format);
		}
		return array;
	}

	public override decimal ReadDecimal()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(16);
			Array.Reverse(array);
			return DecimalFromBytes(array);
		}
		return base.ReadDecimal();
	}

	public decimal[] ReadDecimals(int count)
	{
		return ReadMultiple(count, ReadDecimal);
	}

	public override double ReadDouble()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(8);
			Array.Reverse(array);
			return BitConverter.ToDouble(array, 0);
		}
		return base.ReadDouble();
	}

	public double[] ReadDoubles(int count)
	{
		return ReadMultiple(count, ReadDouble);
	}

	public T ReadEnum<T>(bool strict) where T : struct, IComparable, IFormattable
	{
		return (T)ReadEnum(typeof(T), strict);
	}

	public T[] ReadEnums<T>(int count, bool strict) where T : struct, IComparable, IFormattable
	{
		T[] array = new T[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadEnum<T>(strict);
		}
		return array;
	}

	public override short ReadInt16()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(2);
			Array.Reverse(array);
			return BitConverter.ToInt16(array, 0);
		}
		return base.ReadInt16();
	}

	public short[] ReadInt16s(int count)
	{
		return ReadMultiple(count, ReadInt16);
	}

	public override int ReadInt32()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(4);
			Array.Reverse(array);
			return BitConverter.ToInt32(array, 0);
		}
		return base.ReadInt32();
	}

	public int[] ReadInt32s(int count)
	{
		return ReadMultiple(count, ReadInt32);
	}

	public override long ReadInt64()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(8);
			Array.Reverse(array);
			return BitConverter.ToInt64(array, 0);
		}
		return base.ReadInt64();
	}

	public long[] ReadInt64s(int count)
	{
		return ReadMultiple(count, ReadInt64);
	}

	public T ReadObject<T>()
	{
		return (T)ReadObject(null, BinaryMemberAttribute.Default, typeof(T));
	}

	public T[] ReadObjects<T>(int count)
	{
		return ReadMultiple(count, ReadObject<T>);
	}

	public sbyte[] ReadSBytes(int count)
	{
		return ReadMultiple(count, ReadSByte);
	}

	public override float ReadSingle()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(4);
			Array.Reverse(array);
			return BitConverter.ToSingle(array, 0);
		}
		return base.ReadSingle();
	}

	public float[] ReadSingles(int count)
	{
		return ReadMultiple(count, ReadSingle);
	}

	public string ReadString(BinaryStringFormat format)
	{
		return ReadString(format, Encoding);
	}

	public string ReadString(BinaryStringFormat format, Encoding encoding)
	{
		return format switch
		{
			BinaryStringFormat.ByteLengthPrefix => ReadStringInternal(ReadByte(), encoding), 
			BinaryStringFormat.WordLengthPrefix => ReadStringInternal(ReadInt16(), encoding), 
			BinaryStringFormat.DwordLengthPrefix => ReadStringInternal(ReadInt32(), encoding), 
			BinaryStringFormat.VariableLengthPrefix => ReadStringInternal(Read7BitEncodedInt(), encoding), 
			BinaryStringFormat.ZeroTerminated => ReadZeroTerminatedString(encoding), 
			BinaryStringFormat.NoPrefixOrTermination => throw new ArgumentException("NoPrefixOrTermination cannot be used for read operations if no length has been specified.", "format"), 
			_ => throw new ArgumentOutOfRangeException("format", "The specified binary string format is invalid."), 
		};
	}

	public string ReadString(int length)
	{
		return ReadString(length, Encoding);
	}

	public string ReadString(int length, Encoding encoding)
	{
		return encoding.GetString(ReadBytes(length));
	}

	public string[] ReadStrings(int count)
	{
		string[] array = new string[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadString();
		}
		return array;
	}

	public string[] ReadStrings(int count, BinaryStringFormat format)
	{
		string[] array = new string[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadString(format);
		}
		return array;
	}

	public string[] ReadStrings(int count, BinaryStringFormat format, Encoding encoding)
	{
		string[] array = new string[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadString(format, encoding);
		}
		return array;
	}

	public string[] ReadStrings(int count, int length)
	{
		string[] array = new string[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadString(length);
		}
		return array;
	}

	public string[] ReadStrings(int count, int length, Encoding encoding)
	{
		string[] array = new string[count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadString(length, Encoding);
		}
		return array;
	}

	public override ushort ReadUInt16()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(2);
			Array.Reverse(array);
			return BitConverter.ToUInt16(array, 0);
		}
		return base.ReadUInt16();
	}

	public ushort[] ReadUInt16s(int count)
	{
		return ReadMultiple(count, ReadUInt16);
	}

	public override uint ReadUInt32()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(4);
			Array.Reverse(array);
			return BitConverter.ToUInt32(array, 0);
		}
		return base.ReadUInt32();
	}

	public uint[] ReadUInt32s(int count)
	{
		return ReadMultiple(count, ReadUInt32);
	}

	public override ulong ReadUInt64()
	{
		if (NeedsReversion)
		{
			byte[] array = base.ReadBytes(8);
			Array.Reverse(array);
			return BitConverter.ToUInt64(array, 0);
		}
		return base.ReadUInt64();
	}

	public ulong[] ReadUInt64s(int count)
	{
		return ReadMultiple(count, ReadUInt64);
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

	private T[] ReadMultiple<T>(int count, Func<T> readFunc)
	{
		T[] array = new T[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = readFunc();
		}
		return array;
	}

	private decimal DecimalFromBytes(byte[] bytes)
	{
		if (bytes.Length < 16)
		{
			throw new ArgumentException("Not enough bytes to convert decimal from.", "bytes");
		}
		int[] array = new int[4];
		for (int i = 0; i < 16; i += 4)
		{
			array[i / 4] = BitConverter.ToInt32(bytes, i);
		}
		return new decimal(array);
	}

	private object ReadEnum(Type type, bool strict)
	{
		object obj = ReadObject(null, BinaryMemberAttribute.Default, Enum.GetUnderlyingType(type));
		if (strict && !EnumExtensions.IsValid(type, obj))
		{
			throw new InvalidDataException($"Read value {obj} is not defined in the given enum type {type}.");
		}
		return obj;
	}

	private object ReadObject(object instance, BinaryMemberAttribute attribute, Type type)
	{
		if (attribute.Converter == null)
		{
			if (type == typeof(string))
			{
				if (attribute.StringFormat == BinaryStringFormat.NoPrefixOrTermination)
				{
					return ReadString(attribute.Length);
				}
				return ReadString(attribute.StringFormat);
			}
			if (type.IsEnumerable())
			{
				throw new InvalidOperationException("Multidimensional arrays cannot be read directly.");
			}
			if (type == typeof(bool))
			{
				return ReadBoolean(attribute.BooleanFormat);
			}
			if (type == typeof(byte))
			{
				return ReadByte();
			}
			if (type == typeof(DateTime))
			{
				return ReadDateTime(attribute.DateTimeFormat);
			}
			if (type == typeof(decimal))
			{
				return ReadDecimal();
			}
			if (type == typeof(double))
			{
				return ReadDouble();
			}
			if (type == typeof(short))
			{
				return ReadInt16();
			}
			if (type == typeof(int))
			{
				return ReadInt32();
			}
			if (type == typeof(long))
			{
				return ReadInt64();
			}
			if (type == typeof(sbyte))
			{
				return ReadSByte();
			}
			if (type == typeof(float))
			{
				return ReadSingle();
			}
			if (type == typeof(ushort))
			{
				return ReadUInt16();
			}
			if (type == typeof(uint))
			{
				return ReadUInt32();
			}
			if (type == typeof(ulong))
			{
				return ReadUInt64();
			}
			if (type.GetTypeInfo().IsEnum)
			{
				return ReadEnum(type, attribute.Strict);
			}
			return ReadCustomObject(type, null, Position);
		}
		return BinaryConverterCache.GetConverter(attribute.Converter).Read(this, instance, attribute);
	}

	private object ReadCustomObject(Type type, object instance, long startOffset)
	{
		TypeData typeData = TypeData.GetTypeData(type);
		instance = instance ?? typeData.GetInstance();
		if (typeData.Attribute.Inherit && typeData.TypeInfo.BaseType != null)
		{
			ReadCustomObject(typeData.TypeInfo.BaseType, instance, startOffset);
		}
		foreach (MemberData member in typeData.Members)
		{
			if (member.Attribute.OffsetOrigin == OffsetOrigin.Begin)
			{
				Position = startOffset + member.Attribute.Offset;
			}
			else if (member.Attribute.Offset != 0)
			{
				Position += member.Attribute.Offset;
			}
			Type enumerableElementType = member.Type.GetEnumerableElementType();
			object value;
			if (enumerableElementType == null)
			{
				value = ReadObject(instance, member.Attribute, member.Type);
			}
			else
			{
				Array array = Array.CreateInstance(enumerableElementType, member.Attribute.Length);
				for (int i = 0; i < array.Length; i++)
				{
					array.SetValue(ReadObject(instance, member.Attribute, enumerableElementType), i);
				}
				value = array;
			}
			MemberInfo memberInfo = member.MemberInfo;
			if ((object)memberInfo == null)
			{
				continue;
			}
			if (!(memberInfo is FieldInfo fieldInfo))
			{
				if (memberInfo is PropertyInfo propertyInfo)
				{
					propertyInfo.SetValue(instance, value);
				}
			}
			else
			{
				fieldInfo.SetValue(instance, value);
			}
		}
		return instance;
	}

	private string ReadStringInternal(int length, Encoding encoding)
	{
		return encoding.GetString(ReadBytes(length * encoding.GetByteCount("a")));
	}

	private string ReadZeroTerminatedString(Encoding encoding)
	{
		int byteCount = encoding.GetByteCount("a");
		List<byte> list = new List<byte>();
		switch (byteCount)
		{
		case 1:
		{
			for (byte b = ReadByte(); b != 0; b = ReadByte())
			{
				list.Add(b);
			}
			break;
		}
		case 2:
		{
			for (uint num = ReadUInt16(); num != 0; num = ReadUInt16())
			{
				byte[] bytes = BitConverter.GetBytes(num);
				list.Add(bytes[0]);
				list.Add(bytes[1]);
			}
			break;
		}
		}
		return encoding.GetString(list.ToArray());
	}
}
