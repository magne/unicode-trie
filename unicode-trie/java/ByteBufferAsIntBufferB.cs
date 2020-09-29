// ReSharper disable InconsistentNaming
namespace CodeHive.unicode_trie.java
{
    internal class ByteBufferAsIntBufferB : IntBuffer
    {
        private readonly ByteBuffer bb;

        internal ByteBufferAsIntBufferB(ByteBuffer bb, int mark, int pos, int lim, int cap, long addr, MemorySegmentProxy segment)
            : base(mark, pos, lim, cap, segment)
        {
            this.bb = bb;
            address = addr;

            // TODO assert this.address >= bb.address;
        }

        private long byteOffset(long i)
        {
            return (i << 2) + address;
        }

        public override int get()
        {
            checkSegment();
            int x = UNSAFE.getIntUnaligned(bb.hb, byteOffset(nextGetIndex()), true);
            return x;
        }
    }
}