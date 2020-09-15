// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    internal static class StringLatin1
    {
        public static bool canEncode(int cp)
        {
            return (int) ((uint) cp >> 8) == 0;
        }

        public static void inflate(byte[] src, int srcOff, byte[] dst, int dstOff, int len)
        {
            StringUTF16.inflate(src, srcOff, dst, dstOff, len);
        }
    }
}