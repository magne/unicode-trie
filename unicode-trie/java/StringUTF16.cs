using System;
using System.Diagnostics;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    internal static class StringUTF16
    {
        private static readonly int HI_BYTE_SHIFT;
        private static readonly int LO_BYTE_SHIFT;

        static StringUTF16()
        {
            if (isBigEndian())
            {
                HI_BYTE_SHIFT = 8;
                LO_BYTE_SHIFT = 0;
            }
            else
            {
                HI_BYTE_SHIFT = 0;
                LO_BYTE_SHIFT = 8;
            }
        }

        private static bool isBigEndian()
        {
            return !BitConverter.IsLittleEndian;
        }

        private const int MAX_LENGTH = 1073741823;

        internal static byte[] newBytesFor(in int len)
        {
            if (len < 0)
            {
                throw new ArgumentException();
            }

            if (len > MAX_LENGTH)
            {
                throw new InsufficientMemoryException("UTF16 String size is " + len + ", should be less than " + 1073741823);
            }

            return new byte[len << 1];
        }

        internal static char charAt(byte[] value, int index)
        {
            checkIndex(index, value);
            return getChar(value, index);
        }

        private static void checkIndex(int off, byte[] val)
        {
            checkIndex(off, length(val));
        }

        private static int length(byte[] value)
        {
            return value.Length >> 1;
        }

        private static char getChar(byte[] val, int index)
        {
            Debug.Assert(index >= 0 && index < length(val), "Trusted caller missed bounds check");

            index <<= 1;
            return (char) ((val[index++] & 255) << HI_BYTE_SHIFT | (val[index] & 255) << LO_BYTE_SHIFT);
        }

        internal static int codePointAtSB(byte[] val, int index, int end)
        {
            return codePointAt(val, index, end, true);
        }

        internal static int codePointBeforeSB(byte[] val, int index)
        {
            return codePointBefore(val, index, true);
        }

        private static int codePointAt(byte[] value, int index, int end, bool @checked)
        {
            Debug.Assert(index < end);

            if (@checked)
            {
                checkIndex(index, value);
            }

            var c1 = getChar(value, index);
            if (char.IsHighSurrogate(c1))
            {
                ++index;
                if (index < end)
                {
                    if (@checked)
                    {
                        checkIndex(index, value);
                    }

                    var c2 = getChar(value, index);
                    if (char.IsLowSurrogate(c2))
                    {
                        return Character.toCodePoint(c1, c2);
                    }
                }
            }

            return c1;
        }

        private static int codePointBefore(byte[] value, int index, bool @checked)
        {
            --index;
            if (@checked)
            {
                checkIndex(index, value);
            }

            var c2 = getChar(value, index);
            if (char.IsLowSurrogate(c2) && index > 0)
            {
                --index;
                if (@checked)
                {
                    checkIndex(index, value);
                }

                var c1 = getChar(value, index);
                if (char.IsHighSurrogate(c1))
                {
                    return Character.toCodePoint(c1, c2);
                }
            }

            return c2;
        }

        internal static void inflate(byte[] src, int srcOff, byte[] dst, int dstOff, int len)
        {
            checkBoundsOffCount(dstOff, len, dst);

            for (var i = 0; i < len; ++i)
            {
                putChar(dst, dstOff++, src[srcOff++] & 255);
            }
        }

        private static void putChars(byte[] val, int index, char[] str, int off, int end)
        {
            while (off < end)
            {
                putChar(val, index++, str[off++]);
            }
        }

        internal static void putCharSB(byte[] val, int index, int c)
        {
            checkIndex(index, val);
            putChar(val, index, c);
        }

        internal static void putCharsSB(byte[] val, int index, char[] ca, int off, int end)
        {
            checkBoundsBeginEnd(index, index + end - off, val);
            putChars(val, index, ca, off, end);
        }

        private static void putChar(byte[] val, int index, int c)
        {
            // TODO assert index >= 0 && index < length(val) : "Trusted caller missed bounds check";

            index <<= 1;
            val[index++] = (byte) (c >> HI_BYTE_SHIFT);
            val[index] = (byte) (c >> LO_BYTE_SHIFT);
        }

        private static void checkBoundsOffCount(int offset, int count, byte[] val)
        {
            checkBoundsOffCount(offset, count, length(val));
        }

        private static void checkBoundsBeginEnd(int begin, int end, byte[] val)
        {
            if (begin < 0 || begin > end || end > length(val))
            {
                throw new ArgumentException("begin " + begin + ", end " + end + ", length " + length(val));
            }
        }

        private static void checkBoundsOffCount(int offset, int count, int length)
        {
            if (offset < 0 || count < 0 || offset > length - count)
            {
                throw new ArgumentException("offset " + offset + ", count " + count + ", length " + length);
            }
        }

        internal static void checkIndex(in int index, in int length)
        {
            if (index < 0 || index >= length)
            {
                throw new ArgumentException("index " + index + ", length " + length);
            }
        }
    }
}