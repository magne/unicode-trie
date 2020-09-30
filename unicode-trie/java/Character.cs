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

        private static bool isValidCodePoint(int codePoint)
        {
            var plane = (int) ((uint) codePoint >> 16);
            return plane < 17;
        }

        internal static bool isBmpCodePoint(in int codePoint)
        {
            return (int) ((uint) codePoint >> 16) == 0;
        }

        private static char highSurrogate(int codePoint)
        {
            return (char) ((int) ((uint) codePoint >> 10) + '\ud7c0');
        }

        private static char lowSurrogate(int codePoint)
        {
            return (char) ((codePoint & 1023) + '\udc00');
        }

        internal static int toCodePoint(char high, char low)
        {
            return (high << 10) + low + -56613888;
        }

        internal static char[] toChars(in int codePoint)
        {
            if (isBmpCodePoint(codePoint))
            {
                return new[] {(char) codePoint};
            }

            if (isValidCodePoint(codePoint))
            {
                var result = new char[2];
                toSurrogates(codePoint, result, 0);
                return result;
            }

            throw new ArgumentException($"Not a valid Unicode code point: 0x{codePoint:X}");
        }

        private static void toSurrogates(int codePoint, char[] dst, int index)
        {
            dst[index + 1] = lowSurrogate(codePoint);
            dst[index] = highSurrogate(codePoint);
        }
    }
}