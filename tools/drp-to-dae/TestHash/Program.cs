using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Read VBN and dump bone names + BoneIds
string vbnPath = args.Length > 0 ? args[0] : "";
if (!File.Exists(vbnPath)) { Console.WriteLine("Usage: TestHash <path-to.vbn>"); return; }

byte[] data = File.ReadAllBytes(vbnPath);
string magic = Encoding.ASCII.GetString(data, 0, 4);
bool bigEndian = magic == "VBN ";

int ReadInt(int off) {
    if (bigEndian) return (data[off] << 24) | (data[off+1] << 16) | (data[off+2] << 8) | data[off+3];
    return BitConverter.ToInt32(data, off);
}
uint ReadUInt(int off) => (uint)ReadInt(off);

int boneCount = ReadInt(8);
Console.WriteLine($"VBN: {boneCount} bones, {(bigEndian ? "Big" : "Little")}-endian");
Console.WriteLine($"{'Name',-20} {'BoneId':X8}");

int headerSize = 0x1C;
int boneHeaderSize = 76;
for (int i = 0; i < boneCount; i++) {
    int entryStart = headerSize + i * boneHeaderSize;
    int nameEnd = entryStart;
    while (nameEnd < entryStart + 64 && data[nameEnd] != 0) nameEnd++;
    string name = Encoding.ASCII.GetString(data, entryStart, nameEnd - entryStart);
    uint boneId = ReadUInt(entryStart + 72);
    Console.WriteLine($"  {name,-20} 0x{boneId:X8}");
}
