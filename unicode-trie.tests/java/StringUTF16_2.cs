using System;
using System.Diagnostics;
using CodeHive.unicode_trie.java;

namespace CodeHive.unicode_trie.tests.java
{
    public static class StringUTF16_2
    {
        private const int MaxLength = 1073741823;

        internal static byte[] NewBytesFor(in int len)
        {
            if (len < 0)
            {
                throw new ArgumentException();
            }

            if (len > MaxLength)
            {
                throw new InsufficientMemoryException("UTF16 String size is " + len + ", should be less than " + 1073741823);
            }

            return new byte[len << 1];
        }

        internal static void Inflate(byte[] src, int srcOff, byte[] dst, int dstOff, int len)
        {
            CheckBoundsOffCount(dstOff, len, dst);

            for (var i = 0; i < len; ++i)
            {
                PutChar(dst, dstOff++, src[srcOff++] & 255);
            }
        }

        internal static int CodePointAtSb(byte[] val, int index, int end)
        {
            return CodePointAt(val, index, end, true);
        }

        private static int CodePointAt(byte[] value, int index, int end, bool @checked)
        {
            Debug.Assert(index < end);

            if (@checked)
            {
                StringUTF16.CheckIndex(index, value);
            }

            var c1 = StringUTF16.GetChar(value, index);
            if (char.IsHighSurrogate(c1))
            {
                ++index;
                if (index < end)
                {
                    if (@checked)
                    {
                        StringUTF16.CheckIndex(index, value);
                    }

                    var c2 = StringUTF16.GetChar(value, index);
                    if (char.IsLowSurrogate(c2))
                    {
                        return Character.toCodePoint(c1, c2);
                    }
                }
            }

            return c1;
        }

        internal static int CodePointBeforeSb(byte[] val, int index)
        {
            return CodePointBefore(val, index, true);
        }

        private static int CodePointBefore(byte[] value, int index, bool @checked)
        {
            --index;
            if (@checked)
            {
                StringUTF16.CheckIndex(index, value);
            }

            var c2 = StringUTF16.GetChar(value, index);
            if (char.IsLowSurrogate(c2) && index > 0)
            {
                --index;
                if (@checked)
                {
                    StringUTF16.CheckIndex(index, value);
                }

                var c1 = StringUTF16.GetChar(value, index);
                if (char.IsHighSurrogate(c1))
                {
                    return Character.toCodePoint(c1, c2);
                }
            }

            return c2;
        }

        internal static void PutCharSb(byte[] val, int index, int c)
        {
            StringUTF16.CheckIndex(index, val);
            PutChar(val, index, c);
        }

        internal static void PutCharsSb(byte[] val, int index, char[] ca, int off, int end)
        {
            CheckBoundsBeginEnd(index, index + end - off, val);
            PutChars(val, index, ca, off, end);
        }

        private static void PutChars(byte[] val, int index, char[] str, int off, int end)
        {
            while (off < end)
            {
                PutChar(val, index++, str[off++]);
            }
        }

        private static void PutChar(byte[] val, int index, int c)
        {
            // TODO assert index >= 0 && index < length(val) : "Trusted caller missed bounds check";

            index <<= 1;
            val[index++] = (byte) (c >> StringUTF16.HiByteShift);
            val[index] = (byte) (c >> StringUTF16.LoByteShift);
        }

        private static void CheckBoundsOffCount(int offset, int count, byte[] val)
        {
            CheckBoundsOffCount(offset, count, StringUTF16.Length(val));
        }

        private static void CheckBoundsOffCount(int offset, int count, int length)
        {
            if (offset < 0 || count < 0 || offset > length - count)
            {
                throw new ArgumentException("offset " + offset + ", count " + count + ", length " + length);
            }
        }

        private static void CheckBoundsBeginEnd(int begin, int end, byte[] val)
        {
            if (begin < 0 || begin > end || end > StringUTF16.Length(val))
            {
                throw new ArgumentException("begin " + begin + ", end " + end + ", length " + StringUTF16.Length(val));
            }
        }
    }
}