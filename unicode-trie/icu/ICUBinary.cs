// ReSharper disable InconsistentNaming

using unicode_trie.java;

namespace unicode_trie.icu
{
    internal static class ICUBinary
    {
        public static void skipBytes(ByteBuffer bytes, int skipLength)
        {
            if (skipLength > 0)
            {
                bytes.position(bytes.position() + skipLength);
            }
        }

        public static byte[] getBytes(ByteBuffer bytes, in int length, int additionalSkipLength)
        {
            byte[] dest = new byte[length];
            bytes.get(dest);
            if (additionalSkipLength > 0)
            {
                skipBytes(bytes, additionalSkipLength);
            }

            return dest;
        }

        public static char[] getChars(ByteBuffer bytes, in int length, int additionalSkipLength)
        {
            char[] dest = new char[length];
            bytes.asCharBuffer().get(dest);
            skipBytes(bytes, length * 2 + additionalSkipLength);
            return dest;
        }

        public static int[] getInts(ByteBuffer bytes, in int length, int additionalSkipLength)
        {
            int[] dest = new int[length];
            bytes.asIntBuffer().get(dest);
            skipBytes(bytes, length * 4 + additionalSkipLength);
            return dest;
        }
    }
}