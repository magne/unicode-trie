using System;
using CodeHive.unicode_trie.java;

namespace CodeHive.unicode_trie.util
{
    internal class StringCharSequence : CharSequence
    {
        private readonly byte[] value;

        internal StringCharSequence(string str)
        {
            var chars = str.ToCharArray();
            value = new byte[chars.Length << 1];
            for (var i = 0; i < chars.Length; ++i)
            {
                value[i << 1] = (byte) (chars[i] >> 8);
                value[(i << 1) + 1] = (byte) (chars[i] & 0xff);
            }
        }

        public int length()
        {
            return value.Length >> 1;
        }

        public char charAt(in int index)
        {
            StringUTF16.checkIndex(index, length());
            return StringUTF16.charAt(value, index);
        }
    }
}