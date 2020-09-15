// ReSharper disable InconsistentNaming
namespace unicode_trie.java
{
    internal class ByteBufferAsCharBufferL : CharBuffer
    {
        protected readonly ByteBuffer bb;

        internal ByteBufferAsCharBufferL(ByteBuffer bb, int mark, int pos, int lim, int cap, long addr, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.bb = bb;
            this.address = addr;

            // TODO assert this.address >= bb.address;
        }

        protected long byteOffset(long i)
        {
            return (i << 1) + this.address;
        }

        public override char get()
        {
            this.checkSegment();
            char x = UNSAFE.getCharUnaligned(this.bb.hb, this.byteOffset((long) this.nextGetIndex()), false);
            return x;
        }
    }
}