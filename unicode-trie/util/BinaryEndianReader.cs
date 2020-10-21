using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace CodeHive.unicode_trie.util
{
    internal class BinaryEndianReader : BinaryReader
    {
        private bool bigEndian;
        private bool nativeByteOrder;

        public BinaryEndianReader(Stream input, ByteOrder order = null) : base(input)
        {
            bigEndian = true;
            nativeByteOrder = ByteOrder.BigEndian.IsNative;
        }

        public BinaryEndianReader(Stream input, Encoding encoding, ByteOrder order = null) : base(input, encoding)
        {
            bigEndian = true;
            nativeByteOrder = ByteOrder.BigEndian.IsNative;
        }

        public BinaryEndianReader(Stream input, Encoding encoding, bool leaveOpen, ByteOrder order = null) : base(input, encoding, leaveOpen)
        {
            order ??= ByteOrder.BigEndian;
            bigEndian = order == ByteOrder.BigEndian;
            nativeByteOrder = order.IsNative;
        }

        public ByteOrder Order()
        {
            return bigEndian ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
        }

        public BinaryEndianReader Order(ByteOrder bo)
        {
            bigEndian = bo == ByteOrder.BigEndian;
            nativeByteOrder = bigEndian == (ByteOrder.NativeOrder == ByteOrder.BigEndian);
            return this;
        }

        public override char ReadChar()
        {
            // TODO Does not work
            var ch = base.ReadChar();
            return (char) (nativeByteOrder ? ch : BinaryPrimitives.ReverseEndianness(ch));
        }

        public override short ReadInt16()
        {
            return base.ReadInt16();
        }

        public override ushort ReadUInt16()
        {
            var ui = base.ReadUInt16();
            return nativeByteOrder ? ui : BinaryPrimitives.ReverseEndianness(ui);
        }

        public override int ReadInt32()
        {
            var i = base.ReadInt32();
            return nativeByteOrder ? i : BinaryPrimitives.ReverseEndianness(i);
        }

        public override long ReadInt64()
        {
            return base.ReadInt64();
        }

        public override ulong ReadUInt64()
        {
            return base.ReadUInt64();
        }

        public override uint ReadUInt32()
        {
            return base.ReadUInt32();
        }

        public override float ReadSingle()
        {
            return base.ReadSingle();
        }

        public override double ReadDouble()
        {
            return base.ReadDouble();
        }

        public override decimal ReadDecimal()
        {
            return base.ReadDecimal();
        }

        public override string ReadString()
        {
            return base.ReadString();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            return base.Read(buffer, index, count);
        }

        public override int Read(Span<char> buffer)
        {
            return base.Read(buffer);
        }

        public override char[] ReadChars(int count)
        {
            return base.ReadChars(count);
        }
    }
}