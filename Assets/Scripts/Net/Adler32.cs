// Adler-32 checksum, matching zlib::adler32 used by muduo's ProtobufCodec.
// Algorithm reference: RFC 1950, section 9.
namespace MmorpgClient.Net
{
    public static class Adler32
    {
        private const uint MOD_ADLER = 65521;

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < count; i++)
            {
                a = (a + data[offset + i]) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }
            return (b << 16) | a;
        }
    }
}
