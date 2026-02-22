namespace DrpToDae.Core;

public class RandomXS
{
    private int[] _data;

    public RandomXS(int seed)
    {
        const int init = 0x41C64E6D;
        _data = new int[4];
        _data[0] = (seed * init) + 0x3039;
        _data[1] = (_data[0] * init) + 0x3039;
        _data[2] = (_data[1] * init) + 0x3039;
        _data[3] = (_data[2] * init) + 0x3039;
    }

    public int GetInt()
    {
        var XOR_INT = _data[0] ^ (_data[0] >> 0x13) ^
                        _data[3] ^ (_data[3] << 11) ^
                        ((_data[3] ^ (_data[3] << 11)) >> 8);

        int tmp = _data[1];
        _data[1] = _data[0];
        _data[3] = _data[2];
        _data[2] = tmp;
        _data[0] = XOR_INT;
        return XOR_INT & 0x7FFFFFFF;
    }
}
