using System;
using System.Collections.Generic;
using DrpToDae.IO;

namespace DrpToDae.Formats.NUT
{
    public class NUTFile
    {
        public List<NutTexture> Textures = new List<NutTexture>();
        public ushort Version = 0x0200;
        public bool IsBigEndian { get; private set; }

        public void Read(string filename)
        {
            Read(new FileData(filename));
        }

        public void Read(FileData d)
        {
            d.endian = Endianness.Big;

            uint magic = d.ReadUInt();
            Version = d.ReadUShort();

            if (magic == 0x4E545033)
            {
                IsBigEndian = true;
                ReadNTP3(d);
            }
            else if (magic == 0x4E545755)
            {
                IsBigEndian = true;
                ReadNTWU(d);
            }
            else if (magic == 0x4E545744)
            {
                IsBigEndian = false;
                d.endian = Endianness.Little;
                ReadNTP3(d);
            }
            else
            {
                throw new InvalidDataException($"Unknown NUT magic: 0x{magic:X8}");
            }
        }

        private void ReadNTP3(FileData d)
        {
            d.Seek(0x6);

            ushort count = d.ReadUShort();

            d.Skip(0x8);
            int headerPtr = 0x10;

            for (ushort i = 0; i < count; ++i)
            {
                d.Seek(headerPtr);

                NutTexture tex = new NutTexture();

                int totalSize = d.ReadInt();
                d.Skip(4);
                int dataSize = d.ReadInt();
                int headerSize = d.ReadUShort();
                d.Skip(2);

                d.Skip(1);
                byte mipmapCount = d.ReadByte();
                d.Skip(1);
                int formatByte = d.ReadByte();
                tex.Format = NutTexture.GetFormatFromNutFormat(formatByte);
                tex.Width = d.ReadUShort();
                tex.Height = d.ReadUShort();
                d.Skip(4);
                uint caps2 = d.ReadUInt();

                bool isCubemap = false;
                byte surfaceCount = 1;
                if ((caps2 & 0x200) == 0x200)
                {
                    if ((caps2 & 0xFC00) == 0xFC00)
                    {
                        isCubemap = true;
                        surfaceCount = 6;
                    }
                }

                int dataOffset = 0;
                if (Version < 0x0200)
                {
                    dataOffset = headerPtr + headerSize;
                    d.ReadInt();
                }
                else
                {
                    dataOffset = d.ReadInt() + headerPtr;
                }
                d.ReadInt();
                d.ReadInt();
                d.ReadInt();

                int cmapSize1 = 0;
                if (isCubemap)
                {
                    cmapSize1 = d.ReadInt();
                    d.ReadInt();
                    d.Skip(8);
                }

                int[] mipSizes = new int[mipmapCount];
                if (mipmapCount == 1)
                {
                    if (isCubemap)
                        mipSizes[0] = cmapSize1;
                    else
                        mipSizes[0] = dataSize;
                }
                else
                {
                    for (byte mipLevel = 0; mipLevel < mipmapCount; ++mipLevel)
                    {
                        mipSizes[mipLevel] = d.ReadInt();
                    }
                    d.Align(0x10);
                }

                d.Skip(0x10);

                d.Skip(4);
                d.ReadInt();
                tex.HashId = (uint)d.ReadInt();
                d.Skip(4);

                for (byte surfaceLevel = 0; surfaceLevel < surfaceCount; ++surfaceLevel)
                {
                    TextureSurface surface = new TextureSurface();
                    for (byte mipLevel = 0; mipLevel < mipmapCount; ++mipLevel)
                    {
                        byte[] texArray = d.GetSection(dataOffset, mipSizes[mipLevel]);
                        surface.mipmaps.Add(texArray);
                        dataOffset += mipSizes[mipLevel];
                    }
                    tex.surfaces.Add(surface);
                }

                if (formatByte == 14 || formatByte == 17)
                {
                    tex.SwapChannelOrderUp();
                }

                if (Version < 0x0200)
                    headerPtr += totalSize;
                else
                    headerPtr += headerSize;

                Textures.Add(tex);
            }
        }

        private void ReadNTWU(FileData d)
        {
            d.Seek(0x6);

            ushort count = d.ReadUShort();

            d.Skip(0x8);
            int headerPtr = 0x10;

            for (ushort i = 0; i < count; ++i)
            {
                d.Seek(headerPtr);

                NutTexture tex = new NutTexture();

                int totalSize = d.ReadInt();
                d.Skip(4);
                int dataSize = d.ReadInt();
                int headerSize = d.ReadUShort();
                d.Skip(2);

                d.Skip(1);
                byte mipmapCount = d.ReadByte();
                d.Skip(1);
                int formatByte = d.ReadByte();
                tex.Format = NutTexture.GetFormatFromNutFormat(formatByte);
                tex.Width = d.ReadUShort();
                tex.Height = d.ReadUShort();
                d.ReadInt();
                uint caps2 = d.ReadUInt();

                bool isCubemap = false;
                byte surfaceCount = 1;
                if ((caps2 & 0x200) == 0x200)
                {
                    if ((caps2 & 0xFC00) == 0xFC00)
                    {
                        isCubemap = true;
                        surfaceCount = 6;
                    }
                }

                int dataOffset = d.ReadInt() + headerPtr;
                int mipDataOffset = d.ReadInt() + headerPtr;
                int gtxHeaderOffset = d.ReadInt() + headerPtr;
                d.ReadInt();

                int cmapSize1 = 0;
                if (isCubemap)
                {
                    cmapSize1 = d.ReadInt();
                    d.ReadInt();
                    d.Skip(8);
                }

                int imageSize = 0;
                int mipSize = 0;
                if (mipmapCount == 1)
                {
                    if (isCubemap)
                        imageSize = cmapSize1;
                    else
                        imageSize = dataSize;
                }
                else
                {
                    imageSize = d.ReadInt();
                    mipSize = d.ReadInt();
                    d.Skip((mipmapCount - 2) * 4);
                    d.Align(0x10);
                }

                d.Skip(0x10);

                d.Skip(4);
                d.ReadInt();
                tex.HashId = (uint)d.ReadInt();
                d.Skip(4);

                d.Seek(gtxHeaderOffset);

                int gtxDim = d.ReadInt();
                int gtxWidth = d.ReadInt();
                int gtxHeight = d.ReadInt();
                int gtxDepth = d.ReadInt();
                int gtxNumMips = d.ReadInt();
                int gtxFormat = d.ReadInt();
                d.ReadInt();
                d.ReadInt();
                d.ReadInt();
                int gtxImageSize = d.ReadInt();
                int gtxImagePtr = d.ReadInt();
                int gtxMipSize = d.ReadInt();
                int gtxMipPtr = d.ReadInt();
                int gtxTileMode = d.ReadInt();
                int gtxSwizzle = d.ReadInt();
                d.ReadInt();
                int gtxPitch = d.ReadInt();

                int[] mipOffsets = new int[mipmapCount];
                mipOffsets[0] = 0;
                for (byte mipLevel = 1; mipLevel < mipmapCount; ++mipLevel)
                {
                    mipOffsets[mipLevel] = mipOffsets[1] + d.ReadInt();
                }

                for (byte surfaceLevel = 0; surfaceLevel < surfaceCount; ++surfaceLevel)
                {
                    tex.surfaces.Add(new TextureSurface());
                }

                int w = tex.Width, h = tex.Height;
                for (byte mipLevel = 0; mipLevel < mipmapCount; ++mipLevel)
                {
                    int p = gtxPitch / (gtxWidth / w);

                    int size;
                    if (mipmapCount == 1)
                        size = imageSize;
                    else if (mipLevel + 1 == mipmapCount)
                        size = (mipSize + mipOffsets[1]) - mipOffsets[mipLevel];
                    else
                        size = mipOffsets[mipLevel + 1] - mipOffsets[mipLevel];

                    size /= surfaceCount;

                    int bpp = GTXSwizzle.GetFormatBpp(gtxFormat);
                    if (size < (bpp / 8))
                        size = (bpp / 8);

                    for (byte surfaceLevel = 0; surfaceLevel < surfaceCount; ++surfaceLevel)
                    {
                        byte[] gtxData = d.GetSection(dataOffset + mipOffsets[mipLevel] + (size * surfaceLevel), size);

                        byte[] deswiz = GTXSwizzle.SwizzleBc(
                            gtxData,
                            w,
                            h,
                            gtxFormat,
                            gtxTileMode,
                            p,
                            gtxSwizzle
                        );

                        tex.surfaces[surfaceLevel].mipmaps.Add(new FileData(deswiz).GetSection(0, size));
                    }

                    w /= 2;
                    h /= 2;

                    if (w < 1) w = 1;
                    if (h < 1) h = 1;
                }

                headerPtr += headerSize;

                Textures.Add(tex);
            }
        }
    }
}
