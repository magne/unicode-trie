using System;
using CodeHive.unicode_trie.java;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.tests.java
{
    internal class StringBuilder : CharSequence
    {
        private const bool COMPACT_STRINGS = true;

        private byte[] value;
        private byte   coder;
        private int    count;

        internal StringBuilder(int capacity = 16)
        {
            if (COMPACT_STRINGS)
            {
                value = new byte[capacity];
                coder = 0;
            }
            else
            {
                value = StringUTF16.newBytesFor(capacity);
                coder = 1;
            }
        }

        public int length()
        {
            return count;
        }

        private void ensureCapacityInternal(int minimumCapacity)
        {
            var oldCapacity = value.Length >> coder;
            if (minimumCapacity - oldCapacity > 0)
            {
                var newValue = new byte[newCapacity(minimumCapacity) << coder];
                Array.Copy(value, newValue, value.Length);
                value = newValue;
            }
        }

        private int newCapacity(int minCapacity)
        {
            var oldCapacity = value.Length >> coder;
            var newCapacity = (oldCapacity << 1) + 2;
            if (newCapacity - minCapacity < 0)
            {
                newCapacity = minCapacity;
            }

            var SAFE_BOUND = 2147483639 >> coder;
            return newCapacity > 0 && SAFE_BOUND - newCapacity >= 0 ? newCapacity : hugeCapacity(minCapacity);
        }

        private int hugeCapacity(int minCapacity)
        {
            var SAFE_BOUND = 2147483639 >> coder;
            var UNSAFE_BOUND = 2147483647 >> coder;
            if (UNSAFE_BOUND - minCapacity < 0)
            {
                throw new OutOfMemoryException();
            }

            return minCapacity > SAFE_BOUND ? minCapacity : SAFE_BOUND;
        }

        private void inflate()
        {
            if (isLatin1())
            {
                var buf = StringUTF16.newBytesFor(value.Length);
                StringUTF16.inflate(value, 0, buf, 0, count);
                value = buf;
                coder = 1;
            }
        }

        public char charAt(in int index)
        {
            StringUTF16.checkIndex(index, count);
            return isLatin1() ? (char) (value[index] & 255) : StringUTF16.charAt(value, index);
        }

        internal int codePointAt(int index)
        {
            var count = this.count;
            var value = this.value;
            StringUTF16.checkIndex(index, count);
            return isLatin1() ? value[index] & 255 : StringUTF16.codePointAtSB(value, index, count);
        }

        internal int codePointBefore(int index)
        {
            var i = index - 1;
            if (i >= 0 && i < count)
            {
                return isLatin1() ? value[i] & 255 : StringUTF16.codePointBeforeSB(value, index);
            }

            throw new ArgumentException();
        }

        internal StringBuilder appendCodePoint(int codePoint)
        {
            return Character.isBmpCodePoint(codePoint) ? append((char) codePoint) : append(Character.toChars(codePoint));
        }

        private StringBuilder append(char c)
        {
            ensureCapacityInternal(count + 1);
            if (isLatin1() && StringLatin1.canEncode(c))
            {
                value[count++] = (byte) c;
            }
            else
            {
                if (isLatin1())
                {
                    inflate();
                }

                StringUTF16.putCharSB(value, count++, c);
            }

            return this;
        }

        private StringBuilder append(char[] str)
        {
            var len = str.Length;
            ensureCapacityInternal(count + len);
            appendChars(str, 0, len);
            return this;
        }

        private bool isLatin1()
        {
            return COMPACT_STRINGS && coder == 0;
        }

        private void appendChars(char[] s, int off, int end)
        {
            var count = this.count;
            if (isLatin1())
            {
                var val = value;
                var i = off;

                for (var j = count; i < end; ++i)
                {
                    var c = s[i];
                    if (!StringLatin1.canEncode(c))
                    {
                        this.count = j;
                        inflate();
                        StringUTF16.putCharsSB(value, j, s, i, end);
                        this.count = j + end - i;
                        return;
                    }

                    val[j++] = (byte) c;
                }
            }
            else
            {
                StringUTF16.putCharsSB(value, count, s, off, end);
            }

            this.count = count + end - off;
        }

        public override string ToString()
        {
            var chars = new char[count];
            if (isLatin1())
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