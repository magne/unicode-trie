using System;

namespace CodeHive.unicode_trie
{
    public class ByteOrder
    {
        public static readonly bool      IsBigEndian  = !BitConverter.IsLittleEndian;
        public static readonly ByteOrder BigEndian    = new ByteOrder("BigEndian");
        public static readonly ByteOrder LittleEndian = new ByteOrder("LittleEndian");
        public static readonly ByteOrder NativeOrder  = IsBigEndian ? BigEndian : LittleEndian;

        private readonly string name;

        private ByteOrder(string name)
        {
            this.name = name;
        }

        public bool IsNative => this == NativeOrder;

        public override string ToString()
        {
            return name;
        }
    }
}