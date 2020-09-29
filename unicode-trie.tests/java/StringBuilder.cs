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

        public StringBuilder(int capacity = 16)
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
            int oldCapacity = value.Length >> coder;
            if (minimumCapacity - oldCapacity > 0)
            {
                var newValue = new byte[newCapacity(minimumCapacity) << coder];
                Array.Copy(value, newValue, value.Length);
                value = newValue;
            }
        }

        private int newCapacity(int minCapacity)
        {
            int oldCapacity = value.Length >> coder;
            int newCapacity = (oldCapacity << 1) + 2;
            if (newCapacity - minCapacity < 0)
            {
                newCapacity = minCapacity;
            }

            int SAFE_BOUND = 2147483639 >> coder;
            return newCapacity > 0 && SAFE_BOUND - newCapacity >= 0 ? newCapacity : hugeCapacity(minCapacity);
        }

        private int hugeCapacity(int minCapacity)
        {
            int SAFE_BOUND = 2147483639 >> coder;
            int UNSAFE_BOUND = 2147483647 >> coder;
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
                byte[] buf = StringUTF16.newBytesFor(value.Length);
                StringLatin1.inflate(value, 0, buf, 0, count);
                value = buf;
                coder = 1;
            }
        }

        public char charAt(in int index)
        {
            StringUTF16.checkIndex(index, count);
            return isLatin1() ? (char) (value[index] & 255) : StringUTF16.charAt(value, index);
        }

        public int codePointAt(int index)
        {
            int count = this.count;
            byte[] value = this.value;
            StringUTF16.checkIndex(index, count);
            return isLatin1() ? value[index] & 255 : StringUTF16.codePointAtSB(value, index, count);
        }

        public int codePointBefore(int index)
        {
            int i = index - 1;
            if (i >= 0 && i < count)
            {
                return isLatin1() ? value[i] & 255 : StringUTF16.codePointBeforeSB(value, index);
            }

            throw new ArgumentException();
        }

        public StringBuilder appendCodePoint(int codePoint)
        {
            return Character.isBmpCodePoint(codePoint) ? append((char) codePoint) : append(Character.toChars(codePoint));
        }

        public StringBuilder append(char c)
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

        public StringBuilder append(char[] str)
        {
            int len = str.Length;
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
            int count = this.count;
            if (isLatin1())
            {
                byte[] val = value;
                int i = off;

                for (int j = count; i < end; ++i)
                {
                    char c = s[i];
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
    }
}