using System.Runtime.InteropServices;

namespace System;

public static class FloatExtension
{
    public static float Reverse(this float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }
}
