using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    public class StringBuilder : CharSequence
    {
        private const bool COMPACT_STRINGS = true;

        private byte[] value;
        private byte   coder;
        private int    count;

        public StringBuilder(int capacity = 16)
        {
            if (COMPACT_STRINGS)
            {
                this.value = new byte[capacity];
                this.coder = 0;
            }
            else
            {
                this.value = StringUTF16.newBytesFor(capacity);
                this.coder = 1;
            }
        }

        public int length()
        {
            return count;
        }

        private void ensureCapacityInternal(int minimumCapacity)
        {
            int oldCapacity = this.value.Length >> this.coder;
            if (minimumCapacity - oldCapacity > 0)
            {
                var newValue = new byte[this.newCapacity(minimumCapacity) << this.coder];
                Array.Copy(this.value, newValue, this.value.Length);
                this.value = newValue;
            }
        }

        private int newCapacity(int minCapacity)
        {
            int oldCapacity = this.value.Length >> this.coder;
            int newCapacity = (oldCapacity << 1) + 2;
            if (newCapacity - minCapacity < 0)
            {
                newCapacity = minCapacity;
            }

            int SAFE_BOUND = 2147483639 >> this.coder;
            return newCapacity > 0 && SAFE_BOUND - newCapacity >= 0 ? newCapacity : this.hugeCapacity(minCapacity);
        }

        private int hugeCapacity(int minCapacity)
        {
            int SAFE_BOUND = 2147483639 >> this.coder;
            int UNSAFE_BOUND = 2147483647 >> this.coder;
            if (UNSAFE_BOUND - minCapacity < 0)
            {
                throw new OutOfMemoryException();
            }
            else
            {
                return minCapacity > SAFE_BOUND ? minCapacity : SAFE_BOUND;
            }
        }

        private void inflate()
        {
            if (this.isLatin1())
            {
                byte[] buf = StringUTF16.newBytesFor(this.value.Length);
                StringLatin1.inflate(this.value, 0, buf, 0, this.count);
                this.value = buf;
                this.coder = 1;
            }
        }

        public char charAt(in int index)
        {
            checkIndex(index, this.count);
            return this.isLatin1() ? (char) (this.value[index] & 255) : StringUTF16.charAt(this.value, index);
        }

        public int codePointAt(int index)
        {
            int count = this.count;
            byte[] value = this.value;
            checkIndex(index, count);
            return this.isLatin1() ? value[index] & 255 : StringUTF16.codePointAtSB(value, index, count);
        }

        public int codePointBefore(int index)
        {
            int i = index - 1;
            if (i >= 0 && i < this.count)
            {
                return this.isLatin1() ? this.value[i] & 255 : StringUTF16.codePointBeforeSB(this.value, index);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public StringBuilder appendCodePoint(int codePoint)
        {
            return Character.isBmpCodePoint(codePoint) ? this.append((char) codePoint) : this.append(Character.toChars(codePoint));
        }

        public StringBuilder append(char c)
        {
            this.ensureCapacityInternal(this.count + 1);
            if (this.isLatin1() && StringLatin1.canEncode(c))
            {
                this.value[this.count++] = (byte) c;
            }
            else
            {
                if (this.isLatin1())
                {
                    this.inflate();
                }

                StringUTF16.putCharSB(this.value, this.count++, c);
            }

            return this;
        }

        public StringBuilder append(char[] str)
        {
            int len = str.Length;
            this.ensureCapacityInternal(this.count + len);
            this.appendChars((char[]) str, 0, len);
            return this;
        }

        private bool isLatin1()
        {
            return COMPACT_STRINGS && this.coder == 0;
        }

        private void appendChars(char[] s, int off, int end)
        {
            int count = this.count;
            if (this.isLatin1())
            {
                byte[] val = this.value;
                int i = off;

                for (int j = count; i < end; ++i)
                {
                    char c = s[i];
                    if (!StringLatin1.canEncode(c))
                    {
                        this.count = j;
                        this.inflate();
                        StringUTF16.putCharsSB(this.value, j, s, i, end);
                        this.count = j + end - i;
                        return;
                    }

                    val[j++] = (byte) c;
                }
            }
            else
            {
                StringUTF16.putCharsSB(this.value, count, s, off, end);
            }

            this.count = count + end - off;
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