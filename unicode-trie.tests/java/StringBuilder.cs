using System;
using CodeHive.unicode_trie.java;

// Unreachable code
#pragma warning disable 162
// ReSharper disable HeuristicUnreachableCode

namespace CodeHive.unicode_trie.tests.java
{
    internal class StringBuilder : ICharSequence
    {
        private const bool CompactStrings = true;

        private byte[] value;
        private byte   coder;
        private int    count;

        internal StringBuilder(int capacity = 16)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (CompactStrings)
            {
                value = new byte[capacity];
                coder = 0;
            }
            else
            {
                value = StringUtf16.NewBytesFor(capacity);
                coder = 1;
            }
        }

        public int Length => count;

        private void EnsureCapacityInternal(int minimumCapacity)
        {
            var oldCapacity = value.Length >> coder;
            if (minimumCapacity - oldCapacity > 0)
            {
                var newValue = new byte[NewCapacity(minimumCapacity) << coder];
                Array.Copy(value, newValue, value.Length);
                value = newValue;
            }
        }

        private int NewCapacity(int minCapacity)
        {
            var oldCapacity = value.Length >> coder;
            var newCapacity = (oldCapacity << 1) + 2;
            if (newCapacity - minCapacity < 0)
            {
                newCapacity = minCapacity;
            }

            var safeBound = 2147483639 >> coder;
            return newCapacity > 0 && safeBound - newCapacity >= 0 ? newCapacity : HugeCapacity(minCapacity);
        }

        private int HugeCapacity(int minCapacity)
        {
            var safeBound = 2147483639 >> coder;
            var unsafeBound = 2147483647 >> coder;
            if (unsafeBound - minCapacity < 0)
            {
                throw new OutOfMemoryException();
            }

            return minCapacity > safeBound ? minCapacity : safeBound;
        }

        private void Inflate()
        {
            if (IsLatin1())
            {
                var buf = StringUtf16.NewBytesFor(value.Length);
                StringUtf16.Inflate(value, 0, buf, 0, count);
                value = buf;
                coder = 1;
            }
        }

        public char CharAt(in int index)
        {
            StringUtf16.CheckIndex(index, count);
            return IsLatin1() ? (char) (value[index] & 255) : StringUtf16.CharAt(value, index);
        }

        internal int CodePointAt(int index)
        {
            StringUtf16.CheckIndex(index, count);
            return IsLatin1() ? value[index] & 255 : StringUtf16.CodePointAtSb(value, index, count);
        }

        internal int CodePointBefore(int index)
        {
            var i = index - 1;
            if (i >= 0 && i < count)
            {
                return IsLatin1() ? value[i] & 255 : StringUtf16.CodePointBeforeSb(value, index);
            }

            throw new ArgumentException();
        }

        internal StringBuilder AppendCodePoint(int codePoint)
        {
            return Character2.IsBmpCodePoint(codePoint) ? Append((char) codePoint) : Append(Character2.ToChars(codePoint));
        }

        private StringBuilder Append(char c)
        {
            EnsureCapacityInternal(count + 1);
            if (IsLatin1() && StringLatin1.CanEncode(c))
            {
                value[count++] = (byte) c;
            }
            else
            {
                if (IsLatin1())
                {
                    Inflate();
                }

                StringUtf16.PutCharSb(value, count++, c);
            }

            return this;
        }

        private StringBuilder Append(char[] str)
        {
            var len = str.Length;
            EnsureCapacityInternal(count + len);
            AppendChars(str, 0, len);
            return this;
        }

        private bool IsLatin1()
        {
            return CompactStrings && coder == 0;
        }

        private void AppendChars(char[] s, int off, int end)
        {
            if (IsLatin1())
            {
                var val = value;
                var i = off;

                for (var j = count; i < end; ++i)
                {
                    var c = s[i];
                    if (!StringLatin1.CanEncode(c))
                    {
                        count = j;
                        Inflate();
                        StringUtf16.PutCharsSb(value, j, s, i, end);
                        count = j + end - i;
                        return;
                    }

                    val[j++] = (byte) c;
                }
            }
            else
            {
                StringUtf16.PutCharsSb(value, count, s, off, end);
            }

            count = count + end - off;
        }

        public override string ToString()
        {
            var chars = new char[count];
            if (IsLatin1())
            {
                for (var i = 0; i < count; ++i)
                {
                    chars[i] = (char) value[i];
                }
            }
            else
            {
                for (var i = 0; i < count; ++i)
                {
                    chars[i] = (char) ((value[i << 1] << 8) | value[(i << 1) + 1]);
                }
            }

            return new string(chars);
        }
    }
}