using System;
using System.Diagnostics;

namespace CodeHive.unicode_trie.java
{
    internal static class StringUTF16
    {
        internal static readonly int HiByteShift;
        internal static readonly int LoByteShift;

        static StringUTF16()
        {
            if (IsBigEndian())
            {
                HiByteShift = 8;
                LoByteShift = 0;
            }
            else
            {
                HiByteShift = 0;
                LoByteShift = 8;
            }
        }

        private static bool IsBigEndian()
        {
            return !BitConverter.IsLittleEndian;
        }

        internal static char CharAt(byte[] value, int index)
        {
            CheckIndex(index, value);
            return GetChar(value, index);
        }

        internal static void CheckIndex(int off, byte[] val)
        {
            CheckIndex(off, Length(val));
        }

        internal static int Length(byte[] value)
        {
            return value.Length >> 1;
        }

        internal static char GetChar(byte[] val, int index)
        {
            Debug.Assert(index >= 0 && index < Length(val), "Trusted caller missed bounds check");

            index <<= 1;
            return (char) ((val[index++] & 255) << HiByteShift | (val[index] & 255) << LoByteShift);
        }

        internal static void CheckIndex(in int index, in int length)
        {
            if (index < 0 || index >= length)
            {
                throw new ArgumentException("index " + index + ", length " + length);
            }
        }
    }
}