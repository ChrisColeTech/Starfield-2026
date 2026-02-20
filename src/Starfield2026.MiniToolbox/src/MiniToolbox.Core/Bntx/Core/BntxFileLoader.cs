using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

public class BntxFileLoader : BinaryDataReader
{
	private IDictionary<uint, IResData> _dataMap;

	internal BntxFile BntxFile { get; }

	internal Texture Texture { get; }

	internal BntxFileLoader(BntxFile bntxFile, Stream stream, bool leaveOpen = false)
		: base(stream, Encoding.ASCII, leaveOpen: true)
	{
		base.ByteOrder = ByteOrder.LittleEndian;
		BntxFile = bntxFile;
		_dataMap = new Dictionary<uint, IResData>();
	}

	internal BntxFileLoader(Texture texture, Stream stream, bool leaveOpen = false)
		: base(stream, Encoding.ASCII, leaveOpen: true)
	{
		base.ByteOrder = ByteOrder.LittleEndian;
		Texture = texture;
		_dataMap = new Dictionary<uint, IResData>();
	}

	internal BntxFileLoader(BntxFile BntxFile, string fileName)
		: this(BntxFile, new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
	{
	}

	internal BntxFileLoader(Texture texture, string fileName)
		: this(texture, new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
	{
	}

	internal void ImportTexture()
	{
		Seek(48L);
		((IResData)Texture).Load(this);
	}

	internal void Execute()
	{
		((IResData)BntxFile).Load(this);
	}

	[DebuggerStepThrough]
	internal T Load<T>(bool Relocated = false) where T : IResData, new()
	{
		long num = ReadOffset();
		if (num == 0)
		{
			return default(T);
		}
		if (Relocated)
		{
		}
		using (TemporarySeek(num, SeekOrigin.Begin))
		{
			return ReadResData<T>();
		}
	}

	[DebuggerStepThrough]
	internal T LoadCustom<T>(Func<T> callback, long? offset = null)
	{
		offset = offset ?? ReadOffset();
		if (offset == 0)
		{
			return default(T);
		}
		using (TemporarySeek(offset.Value, SeekOrigin.Begin))
		{
			return callback();
		}
	}

	[DebuggerStepThrough]
	internal ResDict LoadDict()
	{
		long num = ReadInt64();
		if (num == 0)
		{
			return new ResDict();
		}
		using (TemporarySeek(num, SeekOrigin.Begin))
		{
			ResDict resDict = new ResDict();
			((IResData)resDict).Load(this);
			return resDict;
		}
	}

	[DebuggerStepThrough]
	internal IList<T> LoadList<T>(int count, long? offset = null) where T : IResData, new()
	{
		List<T> list = new List<T>(count);
		offset = offset ?? ReadOffset();
		if (offset == 0 || count == 0)
		{
			return list;
		}
		using (TemporarySeek(offset.Value, SeekOrigin.Begin))
		{
			while (count > 0)
			{
				list.Add(ReadResData<T>());
				count--;
			}
			return list;
		}
	}

	[DebuggerStepThrough]
	internal string LoadString(Encoding encoding = null, long offset = 0L)
	{
		if (offset == 0)
		{
			offset = ReadOffset();
		}
		if (offset == 0)
		{
			return null;
		}
		encoding = encoding ?? base.Encoding;
		using (TemporarySeek(offset, SeekOrigin.Begin))
		{
			string text = ReadString(BinaryStringFormat.WordLengthPrefix, encoding);
			if (BntxFile != null && !BntxFile.StringTable.Strings.ContainsKey(offset))
			{
				BntxFile.StringTable.Strings.Add(offset, text);
			}
			return text;
		}
	}

	[DebuggerStepThrough]
	internal IList<string> LoadStrings(int count, Encoding encoding = null)
	{
		long[] array = ReadOffsets(count);
		encoding = encoding ?? base.Encoding;
		string[] array2 = new string[array.Length];
		using (TemporarySeek())
		{
			for (int i = 0; i < array.Length; i++)
			{
				long num = array[i];
				if (num != 0)
				{
					base.Position = num;
					short num2 = ReadInt16();
					array2[i] = ReadString(BinaryStringFormat.ZeroTerminated, encoding);
					if (BntxFile != null && !BntxFile.StringTable.Strings.ContainsKey(num))
					{
						BntxFile.StringTable.Strings.Add(num, array2[i]);
					}
				}
			}
			return array2;
		}
	}

	internal void CheckSignature(string validSignature)
	{
		string text = ReadString(4, Encoding.ASCII);
		if (text != validSignature)
		{
			throw new Exception("Invalid signature, expected '" + validSignature + "' but got '" + text + "'.");
		}
	}

	internal long ReadOffset(bool Relocated = false)
	{
		long num = ReadInt64();
		if (Relocated)
		{
		}
		return (num == 0L) ? 0 : num;
	}

	internal long[] ReadOffsets(int count)
	{
		long[] array = new long[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = ReadOffset();
		}
		return array;
	}

	internal void LoadHeaderBlock()
	{
		uint offset = ReadUInt32();
		long size = ReadInt64();
		SetHeaderBlock(offset, size);
	}

	internal byte[] SetHeaderBlock(uint Offset, long Size)
	{
		using (TemporarySeek(Offset, SeekOrigin.Begin))
		{
			return ReadBytes((int)Size);
		}
	}

	[DebuggerStepThrough]
	private T ReadResData<T>() where T : IResData, new()
	{
		uint key = (uint)base.Position;
		T val = new T();
		val.Load(this);
		if (_dataMap.TryGetValue(key, out var value))
		{
			return (T)value;
		}
		_dataMap.Add(key, val);
		return val;
	}
}
