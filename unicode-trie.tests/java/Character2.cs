using System;

namespace CodeHive.unicode_trie.tests.java
{
    internal static class Character2
    {
        internal static char[] ToChars(in int codePoint)
        {
            if (IsBmpCodePoint(codePoint))
            {
                return new[] {(char) codePoint};
            }

            if (IsValidCodePoint(codePoint))
            {
                var result = new char[2];
                ToSurrogates(codePoint, result, 0);
                return result;
            }

            throw new ArgumentException($"Not a valid Unicode code point: 0x{codePoint:X}");
        }

        internal static bool IsBmpCodePoint(in int codePoint)
        {
            return (int) ((uint) codePoint >> 16) == 0;
        }

        private static bool IsValidCodePoint(int codePoint)
        {
            var plane = (int) ((uint) codePoint >> 16);
            return plane < 17;
        }

        private static void ToSurrogates(int codePoint, char[] dst, int index)
        {
            dst[index + 1] = LowSurrogate(codePoint);
            dst[index] = HighSurrogate(codePoint);
        }

        private static char HighSurrogate(int codePoint)
        {
            return (char) ((int) ((uint) codePoint >> 10) + '\ud7c0');
        }

        private static char LowSurrogate(int codePoint)
        {
            return (char) ((codePoint & 1023) + '\udc00');
        }
    }
}