using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    public class ByteOrder
    {
        private                 String    name;
        public static readonly  ByteOrder BIG_ENDIAN    = new ByteOrder("BIG_ENDIAN");
        public static readonly  ByteOrder LITTLE_ENDIAN = new ByteOrder("LITTLE_ENDIAN");
        private static readonly ByteOrder NATIVE_ORDER;

        private ByteOrder(String name)
        {
            this.name = name;
        }

        public static ByteOrder nativeOrder()
        {
            return NATIVE_ORDER;
        }

        public String toString()
        {
            return this.name;
        }

        static ByteOrder()
        {
            NATIVE_ORDER = BitConverter.IsLittleEndian ? LITTLE_ENDIAN : BIG_ENDIAN;
        }
    }
}