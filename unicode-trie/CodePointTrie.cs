using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using CodeHive.unicode_trie.icu;
using CodeHive.unicode_trie.java;
using CodeHive.unicode_trie.util;

#pragma warning disable 612

namespace CodeHive.unicode_trie
{
    /// <summary>
    /// Immutable Unicode code point trie.
    /// Fast, reasonably compact, map from Unicode code points (U+0000..U+10FFFF) to integer values.
    /// For details see http://site.icu-project.org/design/struct/utrie
    ///
    /// <p/>This class is not intended for public subclassing.
    /// </summary>
    /// <seealso cref="MutableCodePointTrie"/>
    public abstract class CodePointTrie : CodePointMap
    {
        /// <summary>
        /// Selectors for the kind of a CodePointTrie.
        /// Different trade-offs for size vs. speed.
        ///
        /// <p/>Use null for <see cref="CodePointTrie.FromBinary"/> to accept any kind;
        /// <see cref="CodePointTrie.GetKind"/> will return the actual kind.
        /// </summary>
        /// <seealso cref="MutableCodePointTrie.BuildImmutable"/>
        /// <seealso cref="CodePointTrie.FromBinary"/>
        /// <seealso cref="CodePointTrie.GetKind"/>
        public enum Kind
        {
            /// <summary>
            /// Fast/simple/larger BMP data structure.
            /// The <see cref="CodePointTrie.Fast"/> subclasses have additional functions for lookup for BMP
            /// and supplementary code points.
            /// </summary>
            /// <seealso cref="CodePointTrie.Fast"/>
            Fast,

            /// <summary>
            /// Small/slower BMP data structure.
            /// </summary>
            /// <seealso cref="CodePointTrie.Small"/>
            Small
        }

        /// <summary>
        /// Selectors for the number of bits in a CodePointTrie data value.
        ///
        /// <p/>Use null for <see cref="CodePointTrie.FromBinary"/> to accept any data value width;
        /// <see cref="CodePointTrie.GetValueWidth"/> will return the actual data value width.
        /// </summary>
        public enum ValueWidth
        {
            /// <summary>
            /// The trie stores 16 bits per data value.
            /// It returns them as unsigned values 0..0xFFFF=65535.
            /// </summary>
            Bits16,

            /// <summary>
            /// The trie stores 32 bits per data value.
            /// </summary>
            Bits32,

            /// <summary>
            /// The trie stores 8 bits per data value.
            /// It returns them as unsigned values 0..0xff=255.
            /// </summary>
            Bits8
        }

        private const int MaxUnicode = 0x10ffff;

        private const int AsciiLimit = 0x80;

        private CodePointTrie(char[] index, Data data, int highStart,
                              int index3NullOffset, int dataNullOffset)
        {
            this.ascii = new int[AsciiLimit];
            this.index = index;
            this.data = data;
            this.dataLength = data.DataLength;
            this.highStart = highStart;
            this.index3NullOffset = index3NullOffset;
            this.dataNullOffset = dataNullOffset;

            for (var c = 0; c < AsciiLimit; ++c)
            {
                ascii[c] = data.GetFromIndex(c);
            }

            var nullValueOffset = dataNullOffset;
            if (nullValueOffset >= dataLength)
            {
                nullValueOffset = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            }

            nullValue = data.GetFromIndex(nullValueOffset);
        }

        /// <summary>
        /// Creates a trie from its binary form, stored in the Stream starting at the current position.
        /// Advances the stream position to just after the trie data.
        /// Inverse of <see cref="ToBinary"/>.
        ///
        /// <p/>The data is copied from the stream;
        /// later modification of the stream will not affect the trie.
        /// </summary>
        /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
        /// <param name="type">selects the trie kind; this method throws an exception
        ///     if the kind does not match the binary data;
        ///     use null to accept any kind</param>
        /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
        ///     if the valueWidth does not match the binary data;
        ///     use null to accept any data value width</param>
        /// <returns>the trie</returns>
        /// <seealso cref="MutableCodePointTrie"/>
        /// <seealso cref="MutableCodePointTrie.BuildImmutable"/>
        /// <seealso cref="ToBinary"/>
        public static CodePointTrie FromBinary(Stream stream, Kind? type, ValueWidth? valueWidth)
        {
            // TODO Don't rely on character encoding, use explicit type when writing char (ushort)
            using var reader = new BinaryEndianReader(stream, Encoding.Unicode, true);

            // TODO Enough data for a trie header?
            // if (bytes.remaining() < 16 /* sizeof(UCPTrieHeader) */)
            // {
            //     throw new ICUUncheckedIOException("Buffer too short for a CodePointTrie header");
            // }

            // struct UCPTrieHeader
            /* "Tri3" in big-endian US-ASCII (0x54726933) */
            var signature = reader.ReadInt32();

            // Check the signature.
            switch (signature)
            {
                case 0x54726933:
                    // The buffer is already set to the trie data byte order.
                    break;
                case 0x33697254:
                    // Temporarily reverse the byte order.
                    var isBigEndian = reader.Order() == ByteOrder.BigEndian;
                    reader.Order(isBigEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian);
                    break;
                default:
                    throw new ICUUncheckedIOException("Buffer does not contain a serialized CodePointTrie");
            }

            // struct UCPTrieHeader continued
            /*
             * Options bit field:
             * Bits 15..12: Data length bits 19..16.
             * Bits 11..8: Data null block offset bits 19..16.
             * Bits 7..6: UCPTrieType
             * Bits 5..3: Reserved (0).
             * Bits 2..0: UCPTrieValueWidth
             */
            int options = reader.ReadUInt16();

            /* Total length of the index tables. */
            int indexLength = reader.ReadUInt16();

            /* Data length bits 15..0. */
            int dataLength = reader.ReadUInt16();

            /* Index-3 null block offset, 0x7fff or 0xFFFF if none. */
            int index3NullOffset = reader.ReadUInt16();

            /* Data null block offset bits 15..0, 0xFFFFf if none. */
            int dataNullOffset = reader.ReadUInt16();

            /*
             * First code point of the single-value range ending with U+10ffff,
             * rounded up and then shifted right by SHIFT_2.
             */
            int shiftedHighStart = reader.ReadUInt16();
            // struct UCPTrieHeader end

            var typeInt = (options >> 6) & 3;
            var actualKind = typeInt switch
            {
                0 => Kind.Fast,
                1 => Kind.Small,
                _ => throw new ICUUncheckedIOException("CodePointTrie data header has an unsupported kind")
            };

            var valueWidthInt = options & OPTIONS_VALUE_BITS_MASK;
            var actualValueWidth = valueWidthInt switch
            {
                0 => ValueWidth.Bits16,
                1 => ValueWidth.Bits32,
                2 => ValueWidth.Bits8,
                _ => throw new ICUUncheckedIOException("CodePointTrie data header has an unsupported value width")
            };

            if ((options & OPTIONS_RESERVED_MASK) != 0)
            {
                throw new ICUUncheckedIOException("CodePointTrie data header has unsupported options");
            }

            type ??= actualKind;

            valueWidth ??= actualValueWidth;

            if (type != actualKind || valueWidth != actualValueWidth)
            {
                throw new ICUUncheckedIOException("CodePointTrie data header has a different kind or value width than required");
            }

            // Get the length values and offsets.
            dataLength |= ((options & OPTIONS_DATA_LENGTH_MASK) << 4);
            dataNullOffset |= ((options & OPTIONS_DATA_NULL_OFFSET_MASK) << 8);

            var highStart = shiftedHighStart << SHIFT_2;

            // Calculate the actual length, minus the header.
            // var actualLength = indexLength * 2;
            // actualLength += valueWidth switch
            // {
            //     ValueWidth.Bits16 => dataLength * 2,
            //     ValueWidth.Bits32 => dataLength * 4,
            //     _                 => dataLength
            // };

            // if (bytes.remaining() < actualLength)
            // {
            //     throw new ICUUncheckedIOException("Buffer too short for the CodePointTrie data");
            // }

            var index = ICUBinary.getChars(reader, indexLength, 0);
            switch (valueWidth)
            {
                case ValueWidth.Bits16:
                {
                    var data16 = ICUBinary.getChars(reader, dataLength, 0);
                    return type == Kind.Fast
                        ? (CodePointTrie) new Fast16(index, data16, highStart, index3NullOffset, dataNullOffset)
                        : new Small16(index, data16, highStart, index3NullOffset, dataNullOffset);
                }
                case ValueWidth.Bits32:
                {
                    var data32 = ICUBinary.getInts(reader, dataLength, 0);
                    return type == Kind.Fast
                        ? (CodePointTrie) new Fast32(index, data32, highStart, index3NullOffset, dataNullOffset)
                        : new Small32(index, data32, highStart, index3NullOffset, dataNullOffset);
                }
                case ValueWidth.Bits8:
                {
                    var data8 = ICUBinary.getBytes(reader, dataLength, 0);
                    return type == Kind.Fast
                        ? (CodePointTrie) new Fast8(index, data8, highStart, index3NullOffset, dataNullOffset)
                        : new Small8(index, data8, highStart, index3NullOffset, dataNullOffset);
                }
                default:
                    throw new InvalidOperationException("should be unreachable");
            }
        }

        /// <summary>
        /// Returns the trie kind.
        /// </summary>
        /// <returns>the trie kind</returns>
        public abstract Kind GetKind();

        /// <summary>
        /// Returns the number of bits in a trie data value.
        /// </summary>
        /// <returns>the number of bits in a trie data value</returns>
        public ValueWidth GetValueWidth()
        {
            return data.ValueWidth;
        }

        /// <inheritdoc />
        public override int Get(int c)
        {
            return data.GetFromIndex(cpIndex(c));
        }

        /// <summary>
        /// Returns a trie value for an ASCII code point, without range checking.
        /// </summary>
        /// <param name="c">the input code point; must be U+0000..U+007F</param>
        /// <returns>The ASCII code point's trie value</returns>
        public int AsciiGet(int c)
        {
            return ascii[c];
        }

        private static int MaybeFilterValue(int value, int trieNullValue, int nullValue, IValueFilter filter)
        {
            if (value == trieNullValue)
            {
                value = nullValue;
            }
            else if (filter != null)
            {
                value = filter.Apply(value);
            }

            return value;
        }

        /// <inheritdoc />
        public override bool GetRange(int start, IValueFilter filter, Range range)
        {
            if (start < 0 || MaxUnicode < start)
            {
                return false;
            }

            if (start >= highStart)
            {
                var _di = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
                var _value = data.GetFromIndex(_di);
                if (filter != null)
                {
                    _value = filter.Apply(_value);
                }

                range.Set(start, MaxUnicode, _value);
                return true;
            }

            // ReSharper disable once LocalVariableHidesMember
            var nullValue = this.nullValue;
            if (filter != null)
            {
                nullValue = filter.Apply(nullValue);
            }

            var kind = GetKind();

            var prevI3Block = -1;
            var prevBlock = -1;
            var c = start;
            // Initialize to make compiler happy. Real value when haveValue is true.
            int trieValue = 0, value = 0;
            var haveValue = false;
            do
            {
                int i3Block;
                int i3;
                int i3BlockLength;
                int dataBlockLength;
                if (c <= 0xFFFF && (kind == Kind.Fast || c <= SMALL_MAX))
                {
                    i3Block = 0;
                    i3 = c >> FAST_SHIFT;
                    i3BlockLength = kind == Kind.Fast ? BMP_INDEX_LENGTH : SMALL_INDEX_LENGTH;
                    dataBlockLength = FAST_DATA_BLOCK_LENGTH;
                }
                else
                {
                    // Use the multi-stage index.
                    var i1 = c >> SHIFT_1;
                    if (kind == Kind.Fast)
                    {
                        Debug.Assert(0xFFFF < c && c < highStart);
                        i1 += BMP_INDEX_LENGTH - OMITTED_BMP_INDEX_1_LENGTH;
                    }
                    else
                    {
                        Debug.Assert(c < highStart && highStart > SMALL_LIMIT);
                        i1 += SMALL_INDEX_LENGTH;
                    }

                    i3Block = index[index[i1] + ((c >> SHIFT_2) & INDEX_2_MASK)];
                    if (i3Block == prevI3Block && (c - start) >= CP_PRE_INDEX_2_ENTRY)
                    {
                        // The index-3 block is the same as the previous one, and filled with value.
                        Debug.Assert((c & (CP_PRE_INDEX_2_ENTRY - 1)) == 0);
                        c += CP_PRE_INDEX_2_ENTRY;
                        continue;
                    }

                    prevI3Block = i3Block;
                    if (i3Block == index3NullOffset)
                    {
                        // This is the index-3 null block.
                        if (haveValue)
                        {
                            if (nullValue != value)
                            {
                                range.Set(start, c - 1, value);
                                return true;
                            }
                        }
                        else
                        {
                            trieValue = this.nullValue;
                            value = nullValue;
                            haveValue = true;
                        }

                        prevBlock = dataNullOffset;
                        c = (c + CP_PRE_INDEX_2_ENTRY) & ~(CP_PRE_INDEX_2_ENTRY - 1);
                        continue;
                    }

                    i3 = (c >> SHIFT_3) & INDEX_3_MASK;
                    i3BlockLength = INDEX_3_BLOCK_LENGTH;
                    dataBlockLength = SMALL_DATA_BLOCK_LENGTH;
                }

                // Enumerate data blocks for one index-3 block.
                do
                {
                    int block;
                    if ((i3Block & 0x8000) == 0)
                    {
                        block = index[i3Block + i3];
                    }
                    else
                    {
                        // 18-bit indexes stored in groups of 9 entries per 8 indexes.
                        var group = (i3Block & 0x7fff) + (i3 & ~7) + (i3 >> 3);
                        var gi = i3 & 7;
                        block = (index[group++] << (2 + (2 * gi))) & 0x30000;
                        block |= index[group + gi];
                    }

                    if (block == prevBlock && (c - start) >= dataBlockLength)
                    {
                        // The block is the same as the previous one, and filled with value.
                        Debug.Assert((c & (dataBlockLength - 1)) == 0);
                        c += dataBlockLength;
                    }
                    else
                    {
                        var dataMask = dataBlockLength - 1;
                        prevBlock = block;
                        if (block == dataNullOffset)
                        {
                            // This is the data null block.
                            if (haveValue)
                            {
                                if (nullValue != value)
                                {
                                    range.Set(start, c - 1, value);
                                    return true;
                                }
                            }
                            else
                            {
                                trieValue = this.nullValue;
                                value = nullValue;
                                haveValue = true;
                            }

                            c = (c + dataBlockLength) & ~dataMask;
                        }
                        else
                        {
                            var _di = block + (c & dataMask);
                            var trieValue2 = data.GetFromIndex(_di);
                            if (haveValue)
                            {
                                if (trieValue2 != trieValue)
                                {
                                    if (filter == null ||
                                        MaybeFilterValue(trieValue2, this.nullValue, nullValue,
                                            filter) != value)
                                    {
                                        range.Set(start, c - 1, value);
                                        return true;
                                    }

                                    trieValue = trieValue2; // may or may not help
                                }
                            }
                            else
                            {
                                trieValue = trieValue2;
                                value = MaybeFilterValue(trieValue2, this.nullValue, nullValue, filter);
                                haveValue = true;
                            }

                            while ((++c & dataMask) != 0)
                            {
                                trieValue2 = data.GetFromIndex(++_di);
                                if (trieValue2 != trieValue)
                                {
                                    if (filter == null ||
                                        MaybeFilterValue(trieValue2, this.nullValue, nullValue,
                                            filter) != value)
                                    {
                                        range.Set(start, c - 1, value);
                                        return true;
                                    }

                                    trieValue = trieValue2; // may or may not help
                                }
                            }
                        }
                    }
                } while (++i3 < i3BlockLength);
            } while (c < highStart);

            Debug.Assert(haveValue);
            var di = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            var highValue = data.GetFromIndex(di);
            if (MaybeFilterValue(highValue, this.nullValue, nullValue, filter) != value)
            {
                --c;
            }
            else
            {
                c = MaxUnicode;
            }

            range.Set(start, c, value);
            return true;
        }

        /// <summary>
        /// Writes a representation of the trie to the output stream.
        /// * Inverse of <see cref="FromBinary"/>.
        /// </summary>
        /// <param name="os">the output stream</param>
        /// <param name="order">the byte order of the representation, defaults to <see cref="ByteOrder.BigEndian"/></param>
        /// <returns>the number of bytes written</returns>
        public int ToBinary(Stream os, ByteOrder order = null)
        {
            // TODO Don't rely on character encoding, use explicit type when writing char (ushort)
            using var bw = new BinaryEndianWriter(os, Encoding.Unicode, true, order ?? ByteOrder.BigEndian);
            // Write the UCPTrieHeader
            bw.Write(0x54726933); // signature="Tri3"
            bw.Write((ushort) // options
                (((dataLength & 0xf0000) >> 4) |
                 ((dataNullOffset & 0xf0000) >> 8) |
                 ((int) GetKind() << 6) |
                 (int) GetValueWidth()));
            bw.Write((ushort) index.Length);
            bw.Write((ushort) dataLength);
            bw.Write((ushort) index3NullOffset);
            bw.Write((ushort) dataNullOffset);
            bw.Write((ushort) (highStart >> SHIFT_2)); // shiftedHighStart
            var length = 16; // sizeof(UCPTrieHeader)

            foreach (var i in index)
            {
                bw.Write((ushort) i);
            }

            length += index.Length * 2;
            length += data.Write(bw);
            return length;
        }

        /** @internal */
        internal const int FAST_SHIFT = 6;

        /** Number of entries in a data block for code points below the fast limit. 64=0x40 @internal */
        internal const int FAST_DATA_BLOCK_LENGTH = 1 << FAST_SHIFT;

        /** Mask for getting the lower bits for the in-fast-data-block offset. @internal */
        private const int FAST_DATA_MASK = FAST_DATA_BLOCK_LENGTH - 1;

        /** @internal */
        private const int SMALL_MAX = 0xfff;

        /**
         * Offset from dataLength (to be subtracted) for fetching the
         * value returned for out-of-range code points and ill-formed UTF-8/16.
         * @internal
         */
        private const int ERROR_VALUE_NEG_DATA_OFFSET = 1;

        /**
         * Offset from dataLength (to be subtracted) for fetching the
         * value returned for code points highStart..U+10FFFF.
         * @internal
         */
        private const int HIGH_VALUE_NEG_DATA_OFFSET = 2;

        // ucptrie_impl.h

        /** The length of the BMP index table. 1024=0x400 */
        private const int BMP_INDEX_LENGTH = 0x10000 >> FAST_SHIFT;

        internal const int SMALL_LIMIT        = 0x1000;
        private const  int SMALL_INDEX_LENGTH = SMALL_LIMIT >> FAST_SHIFT;

        /** Shift size for getting the index-3 table offset. */
        internal const int SHIFT_3 = 4;

        /** Shift size for getting the index-2 table offset. */
        private const int SHIFT_2 = 5 + SHIFT_3;

        /** Shift size for getting the index-1 table offset. */
        private const int SHIFT_1 = 5 + SHIFT_2;

        /**
         * Difference between two shift sizes,
         * for getting an index-2 offset from an index-3 offset. 5=9-4
         */
        internal const int SHIFT_2_3 = SHIFT_2 - SHIFT_3;

        /**
         * Difference between two shift sizes,
         * for getting an index-1 offset from an index-2 offset. 5=14-9
         */
        internal const int SHIFT_1_2 = SHIFT_1 - SHIFT_2;

        /**
         * Number of index-1 entries for the BMP. (4)
         * This part of the index-1 table is omitted from the serialized form.
         */
        private const int OMITTED_BMP_INDEX_1_LENGTH = 0x10000 >> SHIFT_1;

        /** Number of entries in an index-2 block. 32=0x20 */
        internal const int INDEX_2_BLOCK_LENGTH = 1 << SHIFT_1_2;

        /** Mask for getting the lower bits for the in-index-2-block offset. */
        internal const int INDEX_2_MASK = INDEX_2_BLOCK_LENGTH - 1;

        /** Number of code points per index-2 table entry. 512=0x200 */
        internal const int CP_PRE_INDEX_2_ENTRY = 1 << SHIFT_2;

        /** Number of entries in an index-3 block. 32=0x20 */
        internal const int INDEX_3_BLOCK_LENGTH = 1 << SHIFT_2_3;

        /** Mask for getting the lower bits for the in-index-3-block offset. */
        private const int INDEX_3_MASK = INDEX_3_BLOCK_LENGTH - 1;

        /** Number of entries in a small data block. 16=0x10 */
        internal const int SMALL_DATA_BLOCK_LENGTH = 1 << SHIFT_3;

        /** Mask for getting the lower bits for the in-small-data-block offset. */
        internal const int SMALL_DATA_MASK = SMALL_DATA_BLOCK_LENGTH - 1;

        // ucptrie_impl.h: Constants for use with UCPTrieHeader.options.
        private const int OPTIONS_DATA_LENGTH_MASK      = 0xf000;
        private const int OPTIONS_DATA_NULL_OFFSET_MASK = 0xf00;
        private const int OPTIONS_RESERVED_MASK         = 0x38;
        private const int OPTIONS_VALUE_BITS_MASK       = 7;

        /**
         * Value for index3NullOffset which indicates that there is no index-3 null block.
         * Bit 15 is unused for this value because this bit is used if the index-3 contains
         * 18-bit indexes.
         */
        internal const int NO_INDEX_3_NULL_OFFSET = 0x7fff;

        internal const int NO_DATA_NULL_OFFSET = 0xFFFFf;

        internal abstract class Data
        {
            internal abstract ValueWidth ValueWidth { get; }
            internal abstract int DataLength { get; }
            internal abstract int GetFromIndex(int index);
            internal abstract int Write(BinaryWriter writer);
        }

        private class Data16 : Data
        {
            private readonly char[] array;

            internal Data16(char[] a)
            {
                array = a;
            }

            internal override ValueWidth ValueWidth => ValueWidth.Bits16;

            internal override int DataLength => array.Length;

            internal override int GetFromIndex(int index)
            {
                return array[index];
            }

            internal override int Write(BinaryWriter writer)
            {
                foreach (var v in array)
                {
                    writer.Write((ushort) v);
                }

                return array.Length * 2;
            }
        }

        private class Data32 : Data
        {
            private readonly int[] array;

            public Data32(int[] a)
            {
                array = a;
            }

            internal override ValueWidth ValueWidth => ValueWidth.Bits32;

            internal override int DataLength => array.Length;

            internal override int GetFromIndex(int index)
            {
                return array[index];
            }

            internal override int Write(BinaryWriter writer)
            {
                foreach (var v in array)
                {
                    writer.Write(v);
                }

                return array.Length * 4;
            }
        }

        private class Data8 : Data
        {
            private readonly byte[] array;

            internal Data8(byte[] a)
            {
                array = a;
            }

            internal override ValueWidth ValueWidth => ValueWidth.Bits8;

            internal override int DataLength => array.Length;

            internal override int GetFromIndex(int index)
            {
                return array[index] & 0xff;
            }

            internal override int Write(BinaryWriter writer)
            {
                foreach (var v in array)
                {
                    writer.Write(v);
                }

                return array.Length;
            }
        }

        private readonly int[] ascii;

        private readonly char[] index;

        private readonly Data data;

        private readonly int dataLength;

        /**
         * Start of the last range which ends at U+10FFFF.
         */
        private readonly int highStart;

        /**
         * Internal index-3 null block offset.
         * Set to an impossibly high value (e.g., 0xFFFF) if there is no dedicated index-3 null block.
         */
        private readonly int index3NullOffset;

        /**
         * Internal data null block offset, not shifted.
         * Set to an impossibly high value (e.g., 0xFFFFf) if there is no dedicated data null block.
         */
        private readonly int dataNullOffset;

        private readonly int nullValue;

        private int FastIndex(int c)
        {
            return index[c >> FAST_SHIFT] + (c & FAST_DATA_MASK);
        }

        private int SmallIndex(Kind kind, int c)
        {
            // Split into two methods to make this part inline-friendly.
            // In C, this part is a macro.
            if (c >= highStart)
            {
                return dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            }

            return InternalSmallIndex(kind, c);
        }

        private int InternalSmallIndex(Kind kind, int c)
        {
            var i1 = c >> SHIFT_1;
            if (kind == Kind.Fast)
            {
                Debug.Assert(0xFFFF < c && c < highStart);
                i1 += BMP_INDEX_LENGTH - OMITTED_BMP_INDEX_1_LENGTH;
            }
            else
            {
                Debug.Assert(0 <= c && c < highStart && highStart > SMALL_LIMIT);
                i1 += SMALL_INDEX_LENGTH;
            }

            int i3Block = index[index[i1] + ((c >> SHIFT_2) & INDEX_2_MASK)];
            var i3 = (c >> SHIFT_3) & INDEX_3_MASK;

            int dataBlock;
            if ((i3Block & 0x8000) == 0)
            {
                // 16-bit indexes
                dataBlock = index[i3Block + i3];
            }
            else
            {
                // 18-bit indexes stored in groups of 9 entries per 8 indexes.
                i3Block = (i3Block & 0x7fff) + (i3 & ~7) + (i3 >> 3);
                i3 &= 7;
                dataBlock = (index[i3Block++] << (2 + (2 * i3))) & 0x30000;
                dataBlock |= index[i3Block + i3];
            }

            return dataBlock + (c & SMALL_DATA_MASK);
        }

        internal abstract int cpIndex(int c);

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Fast"/>
        /// </summary>
        public abstract class Fast : CodePointTrie
        {
            internal Fast(char[] index, Data data, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, data, highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with kind <see cref="CodePointTrie.Kind.Fast"/>.
            /// </summary>
            /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
            ///                  if the valueWidth does not match the binary data;
            ///                  use null to accept any data value width</param>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Fast FromBinary(ValueWidth valueWidth, Stream stream)
            {
                return (Fast) CodePointTrie.FromBinary(stream, Kind.Fast, valueWidth);
            }

            /// <returns><see cref="CodePointTrie.Kind.Fast"/></returns>
            public override Kind GetKind()
            {
                return Kind.Fast;
            }

            /// <summary>
            /// Returns a trie value for a BMP code point (U+0000..U+FFFF), without range checking.
            /// Can be used to look up a value for a UTF-16 code unit if other parts of
            /// the string processing check for surrogates.
            /// </summary>
            /// <param name="c">the input code point, must be U+0000..U+FFFF</param>
            /// <returns>The BMP code point's trie value.</returns>
            public abstract int BmpGet(int c);

            /// <summary>
            /// Returns a trie value for a supplementary code point (U+10000..U+10FFFF),
            /// without range checking.
            /// </summary>
            /// <param name="c">the input code point, must be U+10000..U+10FFFF</param>
            /// <returns>The supplementary code point's trie value.</returns>
            public abstract int SuppGet(int c);

            internal override int cpIndex(int c)
            {
                if (c >= 0)
                {
                    if (c <= 0xFFFF)
                    {
                        return FastIndex(c);
                    }
                    else if (c <= 0x10ffff)
                    {
                        return SmallIndex(Kind.Fast, c);
                    }
                }

                return dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
            }

            /// <inheritdoc />
            public override StringIterator GetStringIterator(string str, int index)
            {
                return new FastStringIterator(this, str, index);
            }

            private class FastStringIterator : StringIterator
            {
                private readonly CodePointTrie trie;

                internal FastStringIterator(Fast fast, string str, int index)
                    : base(fast, str, index)
                {
                    trie = fast;
                }

                public override bool Next()
                {
                    if (Index >= Length)
                    {
                        return false;
                    }

                    var lead = CharAt(Index++);
                    CodePoint = lead;
                    int dataIndex;
                    if (!char.IsSurrogate(lead))
                    {
                        dataIndex = trie.FastIndex(CodePoint);
                    }
                    else
                    {
                        char trail;
                        if (Normalizer2Impl.UTF16Plus.isSurrogateLead(lead) && Index < Length &&
                            char.IsLowSurrogate(trail = CharAt(Index)))
                        {
                            ++Index;
                            CodePoint = Character.toCodePoint(lead, trail);
                            dataIndex = trie.SmallIndex(Kind.Fast, CodePoint);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    Value = trie.data.GetFromIndex(dataIndex);
                    return true;
                }

                public override bool Previous()
                {
                    if (Index <= 0)
                    {
                        return false;
                    }

                    var trail = CharAt(--Index);
                    CodePoint = trail;
                    int dataIndex;
                    if (!char.IsSurrogate(trail))
                    {
                        dataIndex = trie.FastIndex(CodePoint);
                    }
                    else
                    {
                        char lead;
                        if (!Normalizer2Impl.UTF16Plus.isSurrogateLead(trail) && Index > 0 &&
                            char.IsHighSurrogate(lead = CharAt(Index - 1)))
                        {
                            --Index;
                            CodePoint = Character.toCodePoint(lead, trail);
                            dataIndex = trie.SmallIndex(Kind.Fast, CodePoint);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    Value = trie.data.GetFromIndex(dataIndex);
                    return true;
                }
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Small"/>
        /// </summary>
        public abstract class Small : CodePointTrie
        {
            internal Small(char[] index, Data data, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, data, highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Small"/>.
            /// </summary>
            /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
            ///                  if the valueWidth does not match the binary data;
            ///                  use null to accept any data value width</param>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Small FromBinary(ValueWidth valueWidth, Stream stream)
            {
                return (Small) CodePointTrie.FromBinary(stream, Kind.Small, valueWidth);
            }

            /// <returns><see cref="CodePointTrie.Kind.Small"/></returns>
            public override Kind GetKind()
            {
                return Kind.Small;
            }

            internal override int cpIndex(int c)
            {
                if (c >= 0)
                {
                    if (c <= SMALL_MAX)
                    {
                        return FastIndex(c);
                    }

                    if (c <= 0x10ffff)
                    {
                        return SmallIndex(Kind.Small, c);
                    }
                }

                return dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
            }

            /// <inheritdoc />
            public override StringIterator GetStringIterator(string str, int index)
            {
                return new SmallStringIterator(this, str, index);
            }

            private class SmallStringIterator : StringIterator
            {
                private readonly CodePointTrie trie;

                internal SmallStringIterator(Small small, string str, int index)
                    : base(small, str, index)
                {
                    trie = small;
                }

                public override bool Next()
                {
                    if (Index >= Length)
                    {
                        return false;
                    }

                    var lead = CharAt(Index++);
                    CodePoint = lead;
                    int dataIndex;
                    if (!char.IsSurrogate(lead))
                    {
                        dataIndex = trie.cpIndex(CodePoint);
                    }
                    else
                    {
                        char trail;
                        if (Normalizer2Impl.UTF16Plus.isSurrogateLead(lead) && Index < Length &&
                            char.IsLowSurrogate(trail = CharAt(Index)))
                        {
                            ++Index;
                            CodePoint = Character.toCodePoint(lead, trail);
                            dataIndex = trie.SmallIndex(Kind.Small, CodePoint);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    Value = trie.data.GetFromIndex(dataIndex);
                    return true;
                }

                public override bool Previous()
                {
                    if (Index <= 0)
                    {
                        return false;
                    }

                    var trail = CharAt(--Index);
                    CodePoint = trail;
                    int dataIndex;
                    if (!char.IsSurrogate(trail))
                    {
                        dataIndex = trie.cpIndex(CodePoint);
                    }
                    else
                    {
                        char lead;
                        if (!Normalizer2Impl.UTF16Plus.isSurrogateLead(trail) && Index > 0 &&
                            char.IsHighSurrogate(lead = CharAt(Index - 1)))
                        {
                            --Index;
                            CodePoint = Character.toCodePoint(lead, trail);
                            dataIndex = trie.SmallIndex(Kind.Small, CodePoint);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    Value = trie.data.GetFromIndex(dataIndex);
                    return true;
                }
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Fast"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits16"/>.
        /// </summary>
        public class Fast16 : Fast
        {
            private readonly char[] dataArray;

            internal Fast16(char[] index, char[] data16, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, new Data16(data16), highStart, index3NullOffset, dataNullOffset)
            {
                dataArray = data16;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Fast"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits16"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Fast16 FromBinary(Stream stream)
            {
                return (Fast16) CodePointTrie.FromBinary(stream, Kind.Fast, ValueWidth.Bits16);
            }

            /// <inheritdoc />
            public override int Get(int c)
            {
                return dataArray[cpIndex(c)];
            }

            /// <inheritdoc />
            public override int BmpGet(int c)
            {
                Debug.Assert(0 <= c && c <= 0xFFFF);
                return dataArray[FastIndex(c)];
            }

            /// <inheritdoc />
            public override int SuppGet(int c)
            {
                Debug.Assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[SmallIndex(Kind.Fast, c)];
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Fast"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits32"/>.
        /// </summary>
        public class Fast32 : Fast
        {
            private readonly int[] dataArray;

            internal Fast32(char[] index, int[] data32, int highStart,
                            int index3NullOffset, int dataNullOffset)
                : base(index, new Data32(data32), highStart, index3NullOffset, dataNullOffset)
            {
                dataArray = data32;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Fast"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits32"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Fast32 FromBinary(Stream stream)
            {
                return (Fast32) CodePointTrie.FromBinary(stream, Kind.Fast, ValueWidth.Bits32);
            }

            /// <inheritdoc />
            public override int Get(int c)
            {
                return dataArray[cpIndex(c)];
            }

            /// <inheritdoc />
            public override int BmpGet(int c)
            {
                Debug.Assert(0 <= c && c <= 0xFFFF);
                return dataArray[FastIndex(c)];
            }

            /// <inheritdoc />
            public override int SuppGet(int c)
            {
                Debug.Assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[SmallIndex(Kind.Fast, c)];
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Fast"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits8"/>.
        /// </summary>
        public class Fast8 : Fast
        {
            private readonly byte[] dataArray;

            internal Fast8(char[] index, byte[] data8, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, new Data8(data8), highStart, index3NullOffset, dataNullOffset)
            {
                dataArray = data8;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Fast"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits8"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Fast8 FromBinary(Stream stream)
            {
                return (Fast8) CodePointTrie.FromBinary(stream, Kind.Fast, ValueWidth.Bits8);
            }

            /// <inheritdoc />
            public override int Get(int c)
            {
                return dataArray[cpIndex(c)] & 0xff;
            }

            /// <inheritdoc />
            public override int BmpGet(int c)
            {
                Debug.Assert(0 <= c && c <= 0xFFFF);
                return dataArray[FastIndex(c)] & 0xff;
            }

            /// <inheritdoc />
            public override int SuppGet(int c)
            {
                Debug.Assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[SmallIndex(Kind.Fast, c)] & 0xff;
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Small"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits16"/>.
        /// </summary>
        public class Small16 : Small
        {
            internal Small16(char[] index, char[] data16, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, new Data16(data16), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Small"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits16"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Small16 FromBinary(Stream stream)
            {
                return (Small16) CodePointTrie.FromBinary(stream, Kind.Small, ValueWidth.Bits16);
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Small"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits32"/>.
        /// </summary>
        public class Small32 : Small
        {
            internal Small32(char[] index, int[] data32, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, new Data32(data32), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Small"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits32"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Small32 FromBinary(Stream stream)
            {
                return (Small32) CodePointTrie.FromBinary(stream, Kind.Small, ValueWidth.Bits32);
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Kind.Small"/> and
        /// <see cref="CodePointTrie.ValueWidth.Bits8"/>.
        /// </summary>
        public class Small8 : Small
        {
            internal Small8(char[] index, byte[] data8, int highStart, int index3NullOffset, int dataNullOffset)
                : base(index, new Data8(data8), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.FromBinary"/> with <see cref="CodePointTrie.Kind.Small"/>
            /// and <see cref="CodePointTrie.ValueWidth.Bits8"/>.
            /// </summary>
            /// <param name="stream">a stream containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            public static Small8 FromBinary(Stream stream)
            {
                return (Small8) CodePointTrie.FromBinary(stream, Kind.Small, ValueWidth.Bits8);
            }
        }
    }
}