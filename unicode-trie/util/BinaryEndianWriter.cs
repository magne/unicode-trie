using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CodeHive.unicode_trie.util
{
    internal class BinaryEndianWriter : BinaryWriter
    {
        private bool bigEndian;
        private bool nativeByteOrder;

        public BinaryEndianWriter(Stream output, ByteOrder order = null) : base(output)
        {
            bigEndian = true;
            nativeByteOrder = ByteOrder.BigEndian.IsNative;
        }

        public BinaryEndianWriter(Stream output, Encoding encoding, ByteOrder order = null) : base(output, encoding)
        {
            bigEndian = true;
            nativeByteOrder = ByteOrder.BigEndian.IsNative;
        }

        public BinaryEndianWriter(Stream output, Encoding encoding, bool leaveOpen, ByteOrder order = null) : base(output, encoding, leaveOpen)
        {
            order ??= ByteOrder.BigEndian;
            bigEndian = order == ByteOrder.BigEndian;
            nativeByteOrder = order.IsNative;
        }

        public ByteOrder Order()
        {
            return bigEndian ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
        }

        public BinaryEndianWriter Order(ByteOrder bo)
        {
            bigEndian = bo == ByteOrder.BigEndian;
            nativeByteOrder = bigEndian == (ByteOrder.NativeOrder == ByteOrder.BigEndian);
            return this;
        }

        public override void Write(char ch)
        {
            // TODO Probably not correct, need to take encoded character and reverse bytes
            base.Write(nativeByteOrder ? ch : BinaryPrimitives.ReverseEndianness(ch));
        }

        public override void Write(char[] chars)
        {
            base.Write(chars);
        }

        public override void Write(char[] chars, int index, int count)
        {
            base.Write(chars, index, count);
        }

        public override void Write(double value)
        {
            base.Write(value);
        }

        public override void Write(decimal value)
        {
            base.Write(value);
        }

        public override void Write(short value)
        {
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            base.Write(nativeByteOrder ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        public override void Write(int value)
        {
            base.Write(nativeByteOrder ? value : BinaryPrimitives.ReverseEndianness(value));
        }

        public override void Write(uint value)
        {
            base.Write(value);
        }

        public override void Write(long value)
        {
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            base.Write(value);
        }

        public override void Write(float value)
        {
            base.Write(value);
        }

        public override void Write(string value)
        {
            base.Write(value);
        }

        public override void Write(ReadOnlySpan<char> chars)
        {
            base.Write(chars);
        }
    }
}