using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    internal static class Character
    {
        internal static int codePointAt(CharSequence seq, int index)
        {
            var c1 = seq.charAt(index);
            if (char.IsHighSurrogate(c1))
            {
                ++index;
                if (index < seq.length())
                {
                    var c2 = seq.charAt(index);
                    if (char.IsLowSurrogate(c2))
                    {
                        return toCodePoint(c1, c2);
                    }
                }
            }

            return c1;
        }

        internal static int codePointBefore(CharSequence seq, int index)
        {
            --index;
            var c2 = seq.charAt(index);
            if (char.IsLowSurrogate(c2) && index > 0)
            {
                --index;
                var c1 = seq.charAt(index);
                if (char.IsHighSurrogate(c1))
                {
                    return toCodePoint(c1, c2);
                }
            }

            return c2;
        }

        internal static int charCount(int codePoint)
        {
            return codePoint >= 65536 ? 2 : 1;
        }

        internal static int toCodePoint(char high, char low)
        {
            return (high << 10) + low + -56613888;
        }
    }
}