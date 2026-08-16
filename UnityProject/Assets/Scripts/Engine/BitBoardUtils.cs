using System.Runtime.CompilerServices;

namespace Utils
{
    public static class BitboardUtils
    {
        private const ulong DeBruijn64 = 0x03F79D71B4CB0A89UL;

        private static readonly int[] Index64 =
        {
            0, 47,  1, 56, 48, 27,  2, 60,
            57, 49, 41, 37, 28, 16,  3, 61,
            54, 58, 35, 52, 50, 42, 21, 44,
            38, 32, 29, 23, 17, 11,  4, 62,
            46, 55, 26, 59, 40, 36, 15, 53,
            34, 51, 20, 43, 31, 22, 10, 45,
            25, 39, 14, 33, 19, 30,  9, 24,
            13, 18,  8, 12,  7,  6,  5, 63
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong x)
        {
            if (x == 0)
                return 64;

            return Index64[((x ^ (x - 1)) * DeBruijn64) >> 58];
        }

        public static bool IsSingleBit(ulong x)
        {
            return x != 0 && (x & (x - 1)) == 0;
        }
    }
}