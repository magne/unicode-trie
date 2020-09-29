using System;

namespace CodeHive.unicode_trie.java
{
    public class ByteOrder
    {
        private readonly        string    name;
        public static readonly  ByteOrder BigEndian    = new ByteOrder("BigEndian");
        public static readonly  ByteOrder LittleEndian = new ByteOrder("LittleEndian");
        private static readonly ByteOrder NativeOrder;

        private ByteOrder(string name)
        {
            this.name = name;
        }

        public static ByteOrder nativeOrder()
        {
            return NativeOrder;
        }

        public override string ToString()
        {
            return name;
        }

        static ByteOrder()
        {
            NativeOrder = BitConverter.IsLittleEndian ? LittleEndian : BigEndian;
        }
    }
}