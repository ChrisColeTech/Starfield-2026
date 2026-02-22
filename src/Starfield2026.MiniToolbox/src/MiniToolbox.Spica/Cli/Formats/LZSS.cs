namespace SpicaCli.Formats
{
    /// <summary>
    /// Nintendo LZ11 decompression. Header byte 0x11 indicates this format.
    /// </summary>
    public static class LZSS
    {
        public static bool IsCompressed(byte[] data)
        {
            return data.Length > 4 && data[0] == 0x11;
        }

        public static byte[] Decompress(byte[] buffer)
        {
            uint decodedLength = (uint)(buffer[1] | (buffer[2] << 8) | (buffer[3] << 16));

            int inputOffset = 4;

            // Extended header for large files
            if (decodedLength == 0)
            {
                decodedLength = (uint)(buffer[4] | (buffer[5] << 8) | (buffer[6] << 16) | (buffer[7] << 24));
                inputOffset = 8;
            }

            byte[] input = buffer;
            byte[] output = new byte[decodedLength];
            long outputOffset = 0;

            byte mask = 0;
            byte header = 0;

            while (outputOffset < decodedLength)
            {
                if ((mask >>= 1) == 0)
                {
                    header = input[inputOffset++];
                    mask = 0x80;
                }

                if ((header & mask) == 0)
                {
                    output[outputOffset++] = input[inputOffset++];
                }
                else
                {
                    int byte1, byte2, byte3, byte4;
                    byte1 = input[inputOffset++];
                    int position, length;
                    switch (byte1 >> 4)
                    {
                        case 0:
                            byte2 = input[inputOffset++];
                            byte3 = input[inputOffset++];

                            position = ((byte2 & 0xf) << 8) | byte3;
                            length = (((byte1 & 0xf) << 4) | (byte2 >> 4)) + 0x11;
                            break;
                        case 1:
                            byte2 = input[inputOffset++];
                            byte3 = input[inputOffset++];
                            byte4 = input[inputOffset++];

                            position = ((byte3 & 0xf) << 8) | byte4;
                            length = (((byte1 & 0xf) << 12) | (byte2 << 4) | (byte3 >> 4)) + 0x111;
                            break;
                        default:
                            byte2 = input[inputOffset++];

                            position = ((byte1 & 0xf) << 8) | byte2;
                            length = (byte1 >> 4) + 1;
                            break;
                    }
                    position++;

                    while (length > 0)
                    {
                        output[outputOffset] = output[outputOffset - position];
                        outputOffset++;
                        length--;
                    }
                }
            }

            return output;
        }
    }
}
