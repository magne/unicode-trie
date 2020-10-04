// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    internal static class Character
    {
        internal static int codePointAt(ICharSequence seq, int index)
        {
            var c1 = seq.CharAt(index);
            if (char.IsHighSurrogate(c1))
            {
                ++index;
                if (index < seq.Length)
                {
                    var c2 = seq.CharAt(index);
                    if (char.IsLowSurrogate(c2))
                    {
                        return toCodePoint(c1, c2);
                    }
                }
            }

            return c1;
        }

        internal static int codePointBefore(ICharSequence seq, int index)
        {
            --index;
            var c2 = seq.CharAt(index);
            if (char.IsLowSurrogate(c2) && index > 0)
            {
                --index;
                var c1 = seq.CharAt(index);
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