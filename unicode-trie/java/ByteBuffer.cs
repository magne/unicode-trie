using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    public abstract class ByteBuffer : Buffer
    {
        internal readonly byte[] hb;
        readonly          int    offset;
        bool                     bigEndian;
        bool                     nativeByteOrder;

        private ByteBuffer(int mark, in int pos, int lim, in int cap, byte[] hb, int offset, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            bigEndian = true;
            nativeByteOrder = ByteOrder.nativeOrder() == ByteOrder.BIG_ENDIAN;
            this.hb = hb;
            this.offset = offset;
        }

        private static ByteBuffer wrap(byte[] array, int offset, int length)
        {
            return new HeapByteBuffer(array, offset, length, null);
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
            if (length > remaining())
            {
                throw new Exception("BufferUnderflowException");
            }

            int end = offset + length;

            for (int i = offset; i < end; ++i)
            {
                dst[i] = get();
            }

            return this;
        }

        public ByteBuffer get(byte[] dst)
        {
            return get(dst, 0, dst.Length);
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
            return bigEndian ? ByteOrder.BIG_ENDIAN : ByteOrder.LITTLE_ENDIAN;
        }

        public ByteBuffer order(ByteOrder bo)
        {
            bigEndian = bo == ByteOrder.BIG_ENDIAN;
            nativeByteOrder = bigEndian == (ByteOrder.nativeOrder() == ByteOrder.BIG_ENDIAN);
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
                ARRAY_BASE_OFFSET = UNSAFE.arrayBaseOffset(typeof(byte[]));
                ARRAY_INDEX_SCALE = UNSAFE.arrayIndexScale(typeof(byte[]));
            }

            public HeapByteBuffer(byte[] buf, int off, int len, MemorySegmentProxy segment)
                : base(-1, off, off + len, buf.Length, buf, 0, segment)
            {
                address = ARRAY_BASE_OFFSET;
            }

            protected int ix(int i)
            {
                return i + offset;
            }

            private long byteOffset(long i)
            {
                return address + i;
            }

            public override byte get()
            {
                checkSegment();
                return hb[ix(nextGetIndex())];
            }

            public override byte get(int i)
            {
                checkSegment();
                return hb[ix(checkIndex(i))];
            }

            public override char getChar()
            {
                checkSegment();
                return UNSAFE.getCharUnaligned(hb, byteOffset(nextGetIndex(2)), bigEndian);
            }

            public override CharBuffer asCharBuffer()
            {
                int pos = position();
                int size = limit() - pos >> 1;
                long addr = address + pos;
                return bigEndian
                    ? (CharBuffer) new ByteBufferAsCharBufferB(this, -1, 0, size, size, addr, segment)
                    : new ByteBufferAsCharBufferL(this, -1, 0, size, size, addr, segment);
            }

            public override int getInt()
            {
                checkSegment();
                return UNSAFE.getIntUnaligned(hb, byteOffset(nextGetIndex(4)), bigEndian);
            }

            public override IntBuffer asIntBuffer()
            {
                int pos = position();
                int size = limit() - pos >> 2;
                long addr = address + pos;
                return (bigEndian
                    ? (IntBuffer) new ByteBufferAsIntBufferB(this, -1, 0, size, size, addr, segment)
                    : new ByteBufferAsIntBufferL(this, -1, 0, size, size, addr, segment));
            }
        }
    }
}