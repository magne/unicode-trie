// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    internal static class Integer
    {
        public static int reverseBytes(int i)
        {
            return i << 24 | (i & '\uff00') << 8 | ((int) ((uint) i >> 8)) & '\uff00' | ((int) ((uint) i >> 24));
        }
    }
}