using System.IO;
// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    internal class DataOutputStream : FilterOutputStream
    {
        protected int written;

        public DataOutputStream(Stream os)
            : base(os)
        { }

        private void incCount(int value)
        {
            int temp = this.written + value;
            if (temp < 0)
            {
                temp = 2147483647;
            }

            this.written = temp;
        }

        public void writeByte(int v)
        {
            this.os.WriteByte((byte) ((int) ((uint) v >> 0) & 255));
            this.incCount(1);
        }


        public void writeChar(int v)
        {
            this.os.WriteByte((byte) ((int) ((uint) v >> 8) & 255));
            this.os.WriteByte((byte) ((int) ((uint) v >> 0) & 255));
            this.incCount(2);
        }

        public void writeInt(int v)
        {
            this.os.WriteByte((byte) ((int) ((uint) v >> 24) & 255));
            this.os.WriteByte((byte) ((int) ((uint) v >> 16) & 255));
            this.os.WriteByte((byte) ((int) ((uint) v >> 8) & 255));
            this.os.WriteByte((byte) ((int) ((uint) v >> 0) & 255));
            this.incCount(4);
        }
    }
}