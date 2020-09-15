using System;
// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    public abstract class ByteBuffer : Buffer
    {
        internal readonly byte[] hb;
        readonly          int    offset;
        bool                     isReadOnly;
        bool                     bigEndian;
        bool                     nativeByteOrder;

        protected ByteBuffer(int mark, in int pos, int lim, in int cap, byte[] hb, int offset, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.bigEndian = true;
            this.nativeByteOrder = ByteOrder.nativeOrder() == ByteOrder.BIG_ENDIAN;
            this.hb = hb;
            this.offset = offset;
        }

        public static ByteBuffer wrap(byte[] array, int offset, int length)
        {
            return new HeapByteBuffer(array, offset, length, (MemorySegmentProxy) null);
        }

        public static ByteBuffer wrap(byte[] array)
        {
            return wrap(array, 0, array.Length);
        }

        public abstract byte get();

        public abstract byte get(int var1);

        public ByteBuffer get(byte[] dst, int offset, int length)
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

        public ByteBuffer get(byte[] dst)
        {
            return this.get(dst, 0, dst.Length);
        }

        public new ByteBuffer position(int newPosition)
        {
            base.position(newPosition);
            return this;
        }

        public new ByteBuffer rewind()
        {
            base.rewind();
            return this;
        }

        public ByteOrder order()
        {
            return this.bigEndian ? ByteOrder.BIG_ENDIAN : ByteOrder.LITTLE_ENDIAN;
        }

        public ByteBuffer order(ByteOrder bo)
        {
            this.bigEndian = bo == ByteOrder.BIG_ENDIAN;
            this.nativeByteOrder = this.bigEndian == (ByteOrder.nativeOrder() == ByteOrder.BIG_ENDIAN);
            return this;
        }

        public abstract char getChar();

        public abstract CharBuffer asCharBuffer();

        public abstract int getInt();

        public abstract IntBuffer asIntBuffer();

        private class HeapByteBuffer : ByteBuffer
        {
            private static readonly long ARRAY_BASE_OFFSET;
            private static readonly long ARRAY_INDEX_SCALE;

            static HeapByteBuffer()
            {
                ARRAY_BASE_OFFSET = (long) UNSAFE.arrayBaseOffset(typeof(byte[]));
                ARRAY_INDEX_SCALE = (long) UNSAFE.arrayIndexScale(typeof(byte[]));
            }

            public HeapByteBuffer(byte[] buf, int off, int len, MemorySegmentProxy segment)
                : base(-1, off, off + len, buf.Length, buf, 0, segment)
            {
                this.address = ARRAY_BASE_OFFSET;
            }

            protected int ix(int i)
            {
                return i + this.offset;
            }

            private long byteOffset(long i)
            {
                return this.address + i;
            }

            public override byte get()
            {
                this.checkSegment();
                return this.hb[this.ix(this.nextGetIndex())];
            }

            public override byte get(int i)
            {
                this.checkSegment();
                return this.hb[this.ix(this.checkIndex(i))];
            }

            public override char getChar()
            {
                this.checkSegment();
                return UNSAFE.getCharUnaligned(this.hb, this.byteOffset((long) this.nextGetIndex(2)), this.bigEndian);
            }

            public override CharBuffer asCharBuffer()
            {
                int pos = this.position();
                int size = this.limit() - pos >> 1;
                long addr = this.address + (long) pos;
                return this.bigEndian
                    ? (CharBuffer) new ByteBufferAsCharBufferB(this, -1, 0, size, size, addr, this.segment)
                    : new ByteBufferAsCharBufferL(this, -1, 0, size, size, addr, this.segment);
            }

            public override int getInt()
            {
                this.checkSegment();
                return UNSAFE.getIntUnaligned(this.hb, this.byteOffset((long) this.nextGetIndex(4)), this.bigEndian);
            }

            public override IntBuffer asIntBuffer()
            {
                int pos = this.position();
                int size = this.limit() - pos >> 2;
                long addr = this.address + (long) pos;
                return (this.bigEndian
                    ? (IntBuffer) new ByteBufferAsIntBufferB(this, -1, 0, size, size, addr, this.segment)
                    : new ByteBufferAsIntBufferL(this, -1, 0, size, size, addr, this.segment));
            }
        }
    }
}