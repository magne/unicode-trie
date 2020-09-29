// ReSharper disable InconsistentNaming
namespace CodeHive.unicode_trie.java
{
    internal class ByteBufferAsCharBufferL : CharBuffer
    {
        private readonly ByteBuffer bb;

        internal ByteBufferAsCharBufferL(ByteBuffer bb, int mark, int pos, int lim, int cap, long addr, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.bb = bb;
            address = addr;

            // TODO assert this.address >= bb.address;
        }

        private long byteOffset(long i)
        {
            return (i << 1) + address;
        }

        public override char get()
        {
            checkSegment();
            char x = UNSAFE.getCharUnaligned(bb.hb, byteOffset(nextGetIndex()), false);
            return x;
        }
    }
}