using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    public abstract class CharBuffer : Buffer
    {
        readonly char[] hb;
        readonly int    offset;
        bool            isReadOnly;

        internal CharBuffer(int mark, int pos, int lim, int cap, char[] hb, int offset, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.hb = hb;
            this.offset = offset;
        }

        internal CharBuffer(int mark, int pos, int lim, int cap, MemorySegmentProxy segment)
            : this(mark, pos, lim, cap, (char[]) null, 0, segment)
        { }

        public abstract char get();

        public CharBuffer get(char[] dst, int offset, int length)
        {
            Objects.checkFromIndexSize(offset, length, dst.Length);
            if (length > this.remaining())
            {
                throw new Exception("BufferUnderflowException");
            }
            else
            {
                int end = offset + length;

                for (int i = offset; i < end; ++i)
                {
                    dst[i] = this.get();
                }

                return this;
            }
        }

        public CharBuffer get(char[] dst)
        {
            return this.get(dst, 0, dst.Length);
        }
    }
}