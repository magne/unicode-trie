using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    internal static class Character
    {
        public static int codePointAt(CharSequence seq, int index)
        {
            char c1 = seq.charAt(index);
            if (isHighSurrogate(c1))
            {
                ++index;
                if (index < seq.length())
                {
                    char c2 = seq.charAt(index);
                    if (isLowSurrogate(c2))
                    {
                        return toCodePoint(c1, c2);
                    }
                }
            }

            return c1;
        }

        public static int codePointBefore(CharSequence seq, int index)
        {
            --index;
            char c2 = seq.charAt(index);
            if (isLowSurrogate(c2) && index > 0)
            {
                --index;
                char c1 = seq.charAt(index);
                if (isHighSurrogate(c1))
                {
                    return toCodePoint(c1, c2);
                }
            }

            return c2;
        }

        public static int charCount(int codePoint)
        {
            return codePoint >= 65536 ? 2 : 1;
        }

        public static bool isHighSurrogate(char ch)
        {
            return ch >= '\ud800' && ch < '\udc00';
        }

        public static bool isLowSurrogate(char ch)
        {
            return ch >= '\udc00' && ch < '\ue000';
        }

        public static bool isValidCodePoint(int codePoint)
        {
            int plane = (int) ((uint) codePoint >> 16);
            return plane < 17;
        }

        public static bool isBmpCodePoint(in int codePoint)
        {
            return (int) ((uint) codePoint >> 16) == 0;
        }

        public static bool isSurrogate(char ch)
        {
            return ch >= '\ud800' && ch < '\ue000';
        }

        public static char highSurrogate(int codePoint)
        {
            return (char) ((int) ((uint) codePoint >> 10) + 'ퟀ');
        }

        public static char lowSurrogate(int codePoint)
        {
            return (char) ((codePoint & 1023) + '\udc00');
        }

        public static int toCodePoint(char high, char low)
        {
            return (high << 10) + low + -56613888;
        }

        public static char[] toChars(in int codePoint)
        {
            if (isBmpCodePoint(codePoint))
            {
                return new[] {(char) codePoint};
            }
            else if (isValidCodePoint(codePoint))
            {
                char[] result = new char[2];
                toSurrogates(codePoint, result, 0);
                return result;
            }
            else
            {
                throw new ArgumentException($"Not a valid Unicode code point: 0x{codePoint:X}");
            }
        }

        static void toSurrogates(int codePoint, char[] dst, int index)
        {
            dst[index + 1] = lowSurrogate(codePoint);
            dst[index] = highSurrogate(codePoint);
        }

        public static char reverseBytes(char ch)
        {
            return (char) ((ch & '\uff00') >> 8 | ch << 8);
        }
    }
}