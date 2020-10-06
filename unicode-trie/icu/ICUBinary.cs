// ReSharper disable InconsistentNaming

using System.IO;

namespace CodeHive.unicode_trie.icu
{
    internal static class ICUBinary
    {
        private static void skipBytes(BinaryReader reader, int skipLength)
        {
            if (skipLength > 0)
            {
                var stream = reader.BaseStream;
                if (stream.CanSeek)
                {
                    stream.Seek(skipLength, SeekOrigin.Current);
                }
                else
                {
                    reader.ReadBytes(skipLength);
                }
            }
        }

        internal static byte[] getBytes(BinaryReader reader, in int length, int additionalSkipLength)
        {
            var dest = reader.ReadBytes(length);
            if (additionalSkipLength > 0)
            {
                skipBytes(reader, additionalSkipLength);
            }

            return dest;
        }

        internal static ushort[] getUShorts(BinaryReader reader, in int length, int additionalSkipLength)
        {
            var dest = new ushort[length];
            for (var i = 0; i < length; ++i)
            {
                dest[i] = reader.ReadUInt16();
            }

            skipBytes(reader, additionalSkipLength);
            return dest;
        }

        internal static int[] getInts(BinaryReader reader, in int length, int additionalSkipLength)
        {
            var dest = new int[length];
            for (var i = 0; i < length; ++i)
            {
                dest[i] = reader.ReadInt32();
            }

            skipBytes(reader, additionalSkipLength);
            return dest;
        }
    }
}