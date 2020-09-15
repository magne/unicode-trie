using System;
// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    public abstract class IntBuffer : Buffer
    {
        readonly int[] hb;
        readonly int   offset;
        bool           isReadOnly;

        private IntBuffer(int mark, int pos, int lim, int cap, int[] hb, int offset, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.hb = hb;
            this.offset = offset;
        }

        protected IntBuffer(in int mark, in int pos, in int lim, in int cap, MemorySegmentProxy segment)
            : this(mark, pos, lim, cap, (int[]) null, 0, segment)
        { }

        public abstract int get();

        public IntBuffer get(int[] dst, int offset, int length)
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

        public IntBuffer get(int[] dst)
        {
            return this.get(dst, 0, dst.Length);
        }
    }
}