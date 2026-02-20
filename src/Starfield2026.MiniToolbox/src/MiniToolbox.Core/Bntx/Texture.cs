using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using MiniToolbox.Core.Bntx;
using MiniToolbox.Core.Bntx;

namespace MiniToolbox.Core.Bntx;

/// <summary>
/// Represents a texture entry inside a BNTX file (read-only).
/// </summary>
[DebuggerDisplay("Texture {Name}")]
public class Texture : IResData
{
	public ChannelType ChannelRed { get; set; }
	public ChannelType ChannelGreen { get; set; }
	public ChannelType ChannelBlue { get; set; }
	public ChannelType ChannelAlpha { get; set; }
	public uint Width { get; set; }
	public uint Height { get; set; }
	public uint MipCount { get; set; }
	public SurfaceFormat Format { get; set; }
	public string Name { get; set; }
	public string Path { get; set; }
	public uint Depth { get; set; }
	public TileMode TileMode { get; set; }
	public uint Swizzle { get; set; }
	public int Alignment { get; set; }
	public uint Pitch { get; set; }
	public Dim Dim { get; set; }
	public SurfaceDim SurfaceDim { get; set; }
	public long[] MipOffsets { get; set; }
	public List<List<byte[]>> TextureData { get; set; }
	public uint textureLayout { get; set; }
	public uint textureLayout2 { get; set; }
	public AccessFlags AccessFlags { get; set; }
	public uint[] Regs { get; set; }
	public uint ArrayLength { get; set; }
	public byte Flags { get; set; }
	public uint ImageSize { get; set; }
	public uint SampleCount { get; set; }
	public int ReadTextureLayout { get; set; }
	public int sparseBinding { get; set; }
	public int sparseResidency { get; set; }
	public uint BlockHeightLog2 { get; set; }

	void IResData.Load(BntxFileLoader loader)
	{
		loader.CheckSignature("BRTI");
		loader.LoadHeaderBlock();
		Flags = loader.ReadByte();
		Dim = loader.ReadEnum<Dim>(strict: true);
		TileMode = loader.ReadEnum<TileMode>(strict: true);
		Swizzle = loader.ReadUInt16();
		MipCount = loader.ReadUInt16();
		SampleCount = loader.ReadUInt32();
		Format = loader.ReadEnum<SurfaceFormat>(strict: false);
		AccessFlags = loader.ReadEnum<AccessFlags>(strict: false);
		Width = loader.ReadUInt32();
		Height = loader.ReadUInt32();
		Depth = loader.ReadUInt32();
		ArrayLength = loader.ReadUInt32();
		textureLayout = loader.ReadUInt32();
		textureLayout2 = loader.ReadUInt32();
		byte[] array = loader.ReadBytes(20);
		ImageSize = loader.ReadUInt32();
		if (ImageSize == 0)
		{
			throw new Exception("Empty image size!");
		}
		Alignment = loader.ReadInt32();
		uint num = loader.ReadUInt32();
		SurfaceDim = loader.ReadEnum<SurfaceDim>(strict: false);
		Name = loader.LoadString(null, 0L);
		long num2 = loader.ReadInt64();
		long value = loader.ReadInt64();
		long value2 = loader.ReadInt64();
		long num3 = loader.ReadInt64();
		long num4 = loader.ReadInt64();
		long num5 = loader.ReadInt64();
		// Skip UserDataDict and UserData (we don't need them)
		loader.LoadDict();
		MipOffsets = loader.LoadCustom(() => loader.ReadInt64s((int)MipCount), value);
		ChannelRed = (ChannelType)(num & 0xFF);
		ChannelGreen = (ChannelType)((num >> 8) & 0xFF);
		ChannelBlue = (ChannelType)((num >> 16) & 0xFF);
		ChannelAlpha = (ChannelType)((num >> 24) & 0xFF);
		TextureData = new List<List<byte[]>>();
		ReadTextureLayout = Flags & 1;
		sparseBinding = Flags >> 1;
		sparseResidency = Flags >> 2;
		BlockHeightLog2 = textureLayout & 7;
		int num6 = 0;
		for (int num7 = 0; num7 < ArrayLength; num7++)
		{
			List<byte[]> list = new List<byte[]>();
			for (int num8 = 0; num8 < MipCount; num8++)
			{
				int num9 = (int)((MipOffsets[0] + ImageSize - MipOffsets[num8]) / ArrayLength);
				using (loader.TemporarySeek(num6 + MipOffsets[num8], SeekOrigin.Begin))
				{
					list.Add(loader.ReadBytes(num9));
				}
				if (list[num8].Length == 0)
				{
					throw new Exception($"Empty mip size! Texture {Name} ImageSize {ImageSize} mips level {num8} sizee {num9} ArrayLength {ArrayLength}");
				}
			}
			TextureData.Add(list);
			num6 += list[0].Length;
		}
		int num10 = 0;
		long num11 = MipOffsets[0];
		long[] mipOffsets = MipOffsets;
		foreach (long num13 in mipOffsets)
		{
			MipOffsets[num10++] = num13 - num11;
		}
	}

}
