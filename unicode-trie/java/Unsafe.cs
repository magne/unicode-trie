using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.java
{
    internal class Unsafe
    {
        private static readonly Unsafe theUnsafe;

        private const bool BIG_ENDIAN = false;

        static Unsafe()
        {
            theUnsafe = new Unsafe();
        }

        private Unsafe()
        { }

        public static Unsafe getUnsafe()
        {
            return theUnsafe;
        }

        public int getInt(Object var1, long var2)
        {
            if (var1 is byte[] ba)
            {
                return makeInt(ba[var2], ba[var2 + 1L], ba[var2 + 2L], ba[var2 + 3L]);
            }

            throw new ArgumentException();
        }

        public byte getByte(Object var1, long var2)
        {
            if (var1 is byte[] ba)
            {
                return ba[var2];
            }

            throw new ArgumentException();
        }

        public short getShort(Object var1, long var2)
        {
            if (var1 is byte[] ba)
            {
                return makeShort(ba[var2], ba[var2 + 1L]);
            }

            throw new ArgumentException();
        }

        public char getChar(Object var1, long var2)
        {
            if (var1 is byte[] ba)
            {
                return (char) makeShort(ba[var2], ba[var2 + 1L]);
            }

            throw new ArgumentException();
        }

        public int getIntUnaligned(Object o, long offset)
        {
            if ((offset & 3L) == 0L)
            {
                return this.getInt(o, offset);
            }
            else
            {
                return (offset & 1L) == 0L
                    ? makeInt(this.getShort(o, offset), this.getShort(o, offset + 2L))
                    : makeInt(this.getByte(o, offset),  this.getByte(o, offset + 1L), this.getByte(o, offset + 2L), this.getByte(o, offset + 3L));
            }
        }

        public int getIntUnaligned(Object o, long offset, bool bigEndian)
        {
            return convEndian(bigEndian, this.getIntUnaligned(o, offset));
        }

        public char getCharUnaligned(Object o, long offset)
        {
            return (offset & 1L) == 0L ? this.getChar(o, offset) : (char) makeShort(this.getByte(o, offset), this.getByte(o, offset + 1L));
        }

        public char getCharUnaligned(Object o, long offset, bool bigEndian)
        {
            return convEndian(bigEndian, this.getCharUnaligned(o, offset));
        }

        private static int pickPos(int top, int pos)
        {
            return BIG_ENDIAN ? top - pos : pos;
        }


        private static int makeInt(short i0, short i1)
        {
            return toUnsignedInt(i0) << pickPos(16, 0) | toUnsignedInt(i1) << pickPos(16, 16);
        }

        private static int makeInt(byte i0, byte i1, byte i2, byte i3)
        {
            return toUnsignedInt(i0) << pickPos(24, 0) | toUnsignedInt(i1) << pickPos(24, 8) | toUnsignedInt(i2) << pickPos(24, 16) | toUnsignedInt(i3) << pickPos(24, 24);
        }

        private static short makeShort(byte i0, byte i1)
        {
            return (short) (toUnsignedInt(i0) << pickPos(8, 0) | toUnsignedInt(i1) << pickPos(8, 8));
        }

        private static int toUnsignedInt(byte n)
        {
            return n & 255;
        }

        private static int toUnsignedInt(short n)
        {
            return n & '\uffff';
        }

        private static char convEndian(bool big, char n)
        {
            return big == BIG_ENDIAN ? n : Character.reverseBytes(n);
        }

        private static int convEndian(bool big, int n)
        {
            return big == BIG_ENDIAN ? n : Integer.reverseBytes(n);
        }

        public int arrayBaseOffset(Type arrayClass)
        {
            if (arrayClass == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return this.arrayBaseOffset0(arrayClass);
            }
        }

        public int arrayIndexScale(Type arrayClass)
        {
            if (arrayClass == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return this.arrayIndexScale0(arrayClass);
            }
        }

        private int arrayBaseOffset0(Type var1)
        {
            return 0;
        }

        private int arrayIndexScale0(Type var1)
        {
            if (var1 == typeof(byte[]))
            {
                return 1;
            }

            throw new ArgumentException();
        }
    }
}