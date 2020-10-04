using System;
using System.Diagnostics;
using CodeHive.unicode_trie.java;

namespace CodeHive.unicode_trie.util
{
    internal class StringCharSequence : ICharSequence
    {
        private static readonly int HiByteShift;
        private static readonly int LoByteShift;

        static StringCharSequence()
        {
            if (ByteOrder.IsBigEndian)
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

        private readonly byte[] value;

        internal StringCharSequence(string str)
        {
            value = str.ToByteArray();
        }

        public int Length => value.Length >> 1;

        public char CharAt(in int index)
        {
            CheckIndex(index);
            return GetChar(index);
        }

        private char GetChar(int index)
        {
            Debug.Assert(index >= 0 && index < Length, "Trusted caller missed bounds check");

            index <<= 1;
            return (char) ((value[index++] & 255) << HiByteShift | (value[index] & 255) << LoByteShift);
        }

        private void CheckIndex(in int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new ArgumentException($"index {index}, length {Length}");
            }
        }
    }
}