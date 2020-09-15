// ReSharper disable InconsistentNaming
namespace unicode_trie.java
{
    internal class ByteBufferAsIntBufferB : IntBuffer
    {
        protected readonly ByteBuffer bb;

        internal ByteBufferAsIntBufferB(ByteBuffer bb, int mark, int pos, int lim, int cap, long addr, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.bb = bb;
            this.address = addr;

            // TODO assert this.address >= bb.address;
        }

        protected long byteOffset(long i)
        {
            return (i << 2) + this.address;
        }

        public override int get()
        {
            this.checkSegment();
            int x = UNSAFE.getIntUnaligned(this.bb.hb, this.byteOffset((long) this.nextGetIndex()), true);
            return x;
        }
    }
}