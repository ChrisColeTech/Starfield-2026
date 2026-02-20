using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MiniToolbox.Core.Bntx;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

/// <summary>
/// BNTX file container (read-only). Loads and provides access to texture entries.
/// </summary>
[DebuggerDisplay("BntxFile {Name}")]
public class BntxFile : IResData
{
	internal uint FileSizeToRLT;
	internal byte[] OriginalRLTChunk;

	public string Name { get; set; }

	public string PlatformTarget
	{
		get => new string(Target);
		set => Target = value.ToCharArray();
	}

	public string VersionFull => $"{VersionMajor}.{VersionMajor2}.{VersionMinor}.{VersionMinor2}";
	public uint VersionMajor { get; set; }
	public uint VersionMajor2 { get; set; }
	public uint VersionMinor { get; set; }
	public uint VersionMinor2 { get; set; }
	public ByteOrder ByteOrder { get; private set; }
	public uint Alignment { get; set; }
	public int DataAlignment => 1 << (int)Alignment;
	public uint TargetAddressSize { get; set; }
	public uint Flag { get; set; }
	public uint BlockOffset { get; set; }
	public RelocationTable RelocationTable { get; set; }
	public char[] Target { get; set; }
	public IList<Texture> Textures { get; set; }
	public ResDict TextureDict { get; set; }
	public StringTable StringTable { get; set; }

	public BntxFile()
	{
		StringTable = new StringTable();
	}

	public BntxFile(Stream stream, bool leaveOpen = false)
	{
		using BntxFileLoader bntxFileLoader = new BntxFileLoader(this, stream, leaveOpen);
		bntxFileLoader.Execute();
	}

	public BntxFile(string fileName)
	{
		using BntxFileLoader bntxFileLoader = new BntxFileLoader(this, fileName);
		bntxFileLoader.Execute();
	}

	private void SetVersionInfo(uint Version)
	{
		VersionMajor = Version >> 24;
		VersionMajor2 = (Version >> 16) & 0xFF;
		VersionMinor = (Version >> 8) & 0xFF;
		VersionMinor2 = Version & 0xFF;
	}

	void IResData.Load(BntxFileLoader loader)
	{
		StringTable = new StringTable();
		loader.CheckSignature("BNTX");
		uint num = loader.ReadUInt32();
		uint versionInfo = loader.ReadUInt32();
		SetVersionInfo(versionInfo);
		ByteOrder = loader.ReadEnum<ByteOrder>(strict: false);
		Alignment = loader.ReadByte();
		TargetAddressSize = loader.ReadByte();
		uint num2 = loader.ReadUInt32();
		Flag = loader.ReadUInt16();
		BlockOffset = loader.ReadUInt16();
		uint num3 = loader.ReadUInt32();
		uint num4 = loader.ReadUInt32();
		Target = loader.ReadChars(4);
		int textureCount = loader.ReadInt32();
		long num5 = loader.ReadInt64();
		FileSizeToRLT = num3;
		OriginalRLTChunk = loader.LoadCustom(() => loader.ReadBytes((int)(loader.BaseStream.Length - FileSizeToRLT)), num3);
		Textures = loader.LoadCustom(delegate
		{
			IList<Texture> list = new List<Texture>();
			for (int i = 0; i < textureCount; i++)
			{
				list.Add(loader.Load<Texture>());
			}
			return list;
		}, num5);
		long num6 = loader.ReadInt64();
		TextureDict = loader.LoadDict();
		Name = loader.LoadString(null, num2 - 2);
		loader.Seek(num5 + textureCount * 8, SeekOrigin.Begin);
		StringTable.Load(loader);
	}
}
