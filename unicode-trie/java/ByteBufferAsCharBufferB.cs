// ReSharper disable InconsistentNaming
namespace CodeHive.unicode_trie.java
{
    internal class ByteBufferAsCharBufferB : CharBuffer
    {
        protected readonly ByteBuffer bb;

        internal ByteBufferAsCharBufferB(ByteBuffer bb, int mark, int pos, int lim, int cap, long addr, MemorySegmentProxy segment)
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
            char x = UNSAFE.getCharUnaligned(this.bb.hb, this.byteOffset((long) this.nextGetIndex()), true);
            return x;
        }
    }
}