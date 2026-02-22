using System;
using System.Text;

class Test {
    static uint Crc32Drp(string text) {
        byte[] data = Encoding.ASCII.GetBytes(text);
        for (int i = 0; i < 4 && i < data.Length; i++)
            data[i] = (byte)(~data[i] & 0xFF);
        uint crc = 0xFFFFFFFF;
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++) {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
            table[i] = c;
        }
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
    
    static uint Crc32Std(string text) {
        byte[] data = Encoding.ASCII.GetBytes(text);
        uint crc = 0xFFFFFFFF;
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++) {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
            table[i] = c;
        }
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
    
    static void Main() {
        string[] bones = {"BASE","Spine1","Spine2","Hip","Neck1","Head","R_Shoulder","R_Arm","CENTER_RT","THROW_NULL"};
        Console.WriteLine("DRP-style vs Standard CRC32, target OMO hash 0x4F101F52:");
        foreach(var b in bones) {
            Console.WriteLine($"  {b,-15} DRP={Crc32Drp(b):X8}  Std={Crc32Std(b):X8}");
        }
    }
}
