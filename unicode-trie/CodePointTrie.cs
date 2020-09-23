﻿using System;
using System.IO;
using CodeHive.unicode_trie.icu;
using CodeHive.unicode_trie.java;

// ReSharper disable InconsistentNaming
// ReSharper disable InvalidXmlDocComment
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
    /// <p/> @stable ICU 63
    public abstract class CodePointTrie : CodePointMap
    {
        /// <summary>
        /// Selectors for the type of a CodePointTrie.
        /// Different trade-offs for size vs. speed.
        ///
        /// <p/>Use null for <see cref="CodePointTrie.fromBinary"/> to accept any type;
        /// <see cref="CodePointTrie.getType"/> will return the actual type.
        /// </summary>
        /// <seealso cref="MutableCodePointTrie.buildImmutable"/>
        /// <seealso cref="CodePointTrie.fromBinary"/>
        /// <seealso cref="CodePointTrie.getType"/>
        /// <p/> @stable ICU 63
        public enum Type
        {
            /// <summary>
            /// Fast/simple/larger BMP data structure.
            /// The <see cref="CodePointTrie.Fast"/> subclasses have additional functions for lookup for BMP
            /// and supplementary code points.
            /// </summary>
            /// <seealso cref="CodePointTrie.Fast"/>
            /// <p/> @stable ICU 63
            FAST,

            /// <summary>
            /// Small/slower BMP data structure.
            /// </summary>
            /// <seealso cref="CodePointTrie.Small"/>
            /// <p/> @stable ICU 63
            SMALL
        }

        /// <summary>
        /// Selectors for the number of bits in a CodePointTrie data value.
        ///
        /// <p/>Use null for <see cref="CodePointTrie.fromBinary"/> to accept any data value width;
        /// <see cref="CodePointTrie.getValueWidth"/> will return the actual data value width.
        /// </summary>
        /// <p/> @stable ICU 63
        public enum ValueWidth
        {
            /// <summary>
            /// The trie stores 16 bits per data value.
            /// It returns them as unsigned values 0..0xffff=65535.
            /// </summary>
            /// <p/> @stable ICU 63
            BITS_16,

            /// <summary>
            /// The trie stores 32 bits per data value.
            /// </summary>
            /// <p/> @stable ICU 63
            BITS_32,

            /// <summary>
            /// The trie stores 8 bits per data value.
            /// It returns them as unsigned values 0..0xff=255.
            /// </summary>
            /// <p/> @stable ICU 63
            BITS_8
        }

        private CodePointTrie(char[] index, Data data, int highStart,
                              int index3NullOffset, int dataNullOffset)
        {
            this.ascii = new int[ASCII_LIMIT];
            this.index = index;
            this.data = data;
            this.dataLength = data.getDataLength();
            this.highStart = highStart;
            this.index3NullOffset = index3NullOffset;
            this.dataNullOffset = dataNullOffset;

            for (int c = 0; c < ASCII_LIMIT; ++c)
            {
                ascii[c] = data.getFromIndex(c);
            }

            int nullValueOffset = dataNullOffset;
            if (nullValueOffset >= dataLength)
            {
                nullValueOffset = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            }

            nullValue = data.getFromIndex(nullValueOffset);
        }

        /// <summary>
        /// Creates a trie from its binary form,
        /// stored in the ByteBuffer starting at the current position.
        /// Advances the buffer position to just after the trie data.
        /// Inverse of <see cref="toBinary"/>.
        ///
        /// <p>The data is copied from the buffer;
        /// later modification of the buffer will not affect the trie.
        /// </summary>
        /// <param name="type">selects the trie type; this method throws an exception
        ///             if the type does not match the binary data;
        ///             use null to accept any type</param>
        /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
        ///                  if the valueWidth does not match the binary data;
        ///                  use null to accept any data value width</param>
        /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
        /// <returns>the trie</returns>
        /// <seealso cref="MutableCodePointTrie"/>
        /// <seealso cref="MutableCodePointTrie.buildImmutable"/>
        /// <seealso cref="toBinary"/>
        /// <p/> @stable ICU 63
        public static CodePointTrie fromBinary(Type? type, ValueWidth? valueWidth, ByteBuffer bytes)
        {
            ByteOrder outerByteOrder = bytes.order();
            try
            {
                // Enough data for a trie header?
                if (bytes.remaining() < 16 /* sizeof(UCPTrieHeader) */)
                {
                    throw new ICUUncheckedIOException("Buffer too short for a CodePointTrie header");
                }

                // struct UCPTrieHeader
                /** "Tri3" in big-endian US-ASCII (0x54726933) */
                int signature = bytes.getInt();

                // Check the signature.
                switch (signature)
                {
                    case 0x54726933:
                        // The buffer is already set to the trie data byte order.
                        break;
                    case 0x33697254:
                        // Temporarily reverse the byte order.
                        bool isBigEndian = outerByteOrder == ByteOrder.BIG_ENDIAN;
                        bytes.order(isBigEndian ? ByteOrder.LITTLE_ENDIAN : ByteOrder.BIG_ENDIAN);
                        // ReSharper disable once RedundantAssignment
                        signature = 0x54726933;
                        break;
                    default:
                        throw new ICUUncheckedIOException("Buffer does not contain a serialized CodePointTrie");
                }

                // struct UCPTrieHeader continued
                /**
                 * Options bit field:
                 * Bits 15..12: Data length bits 19..16.
                 * Bits 11..8: Data null block offset bits 19..16.
                 * Bits 7..6: UCPTrieType
                 * Bits 5..3: Reserved (0).
                 * Bits 2..0: UCPTrieValueWidth
                 */
                int options = bytes.getChar();

                /** Total length of the index tables. */
                int indexLength = bytes.getChar();

                /** Data length bits 15..0. */
                int dataLength = bytes.getChar();

                /** Index-3 null block offset, 0x7fff or 0xffff if none. */
                int index3NullOffset = bytes.getChar();

                /** Data null block offset bits 15..0, 0xfffff if none. */
                int dataNullOffset = bytes.getChar();

                /**
                 * First code point of the single-value range ending with U+10ffff,
                 * rounded up and then shifted right by SHIFT_2.
                 */
                int shiftedHighStart = bytes.getChar();
                // struct UCPTrieHeader end

                int typeInt = (options >> 6) & 3;
                Type actualType;
                switch (typeInt)
                {
                    case 0:
                        actualType = Type.FAST;
                        break;
                    case 1:
                        actualType = Type.SMALL;
                        break;
                    default:
                        throw new ICUUncheckedIOException("CodePointTrie data header has an unsupported type");
                }

                int valueWidthInt = options & OPTIONS_VALUE_BITS_MASK;
                ValueWidth actualValueWidth;
                switch (valueWidthInt)
                {
                    case 0:
                        actualValueWidth = ValueWidth.BITS_16;
                        break;
                    case 1:
                        actualValueWidth = ValueWidth.BITS_32;
                        break;
                    case 2:
                        actualValueWidth = ValueWidth.BITS_8;
                        break;
                    default:
                        throw new ICUUncheckedIOException("CodePointTrie data header has an unsupported value width");
                }

                if ((options & OPTIONS_RESERVED_MASK) != 0)
                {
                    throw new ICUUncheckedIOException("CodePointTrie data header has unsupported options");
                }

                if (type == null)
                {
                    type = actualType;
                }

                if (valueWidth == null)
                {
                    valueWidth = actualValueWidth;
                }

                if (type != actualType || valueWidth != actualValueWidth)
                {
                    throw new ICUUncheckedIOException("CodePointTrie data header has a different type or value width than required");
                }

                // Get the length values and offsets.
                dataLength |= ((options & OPTIONS_DATA_LENGTH_MASK) << 4);
                dataNullOffset |= ((options & OPTIONS_DATA_NULL_OFFSET_MASK) << 8);

                int highStart = shiftedHighStart << SHIFT_2;

                // Calculate the actual length, minus the header.
                int actualLength = indexLength * 2;
                if (valueWidth == ValueWidth.BITS_16)
                {
                    actualLength += dataLength * 2;
                }
                else if (valueWidth == ValueWidth.BITS_32)
                {
                    actualLength += dataLength * 4;
                }
                else
                {
                    actualLength += dataLength;
                }

                if (bytes.remaining() < actualLength)
                {
                    throw new ICUUncheckedIOException("Buffer too short for the CodePointTrie data");
                }

                char[] index = ICUBinary.getChars(bytes, indexLength, 0);
                switch (valueWidth)
                {
                    case ValueWidth.BITS_16:
                    {
                        char[] data16 = ICUBinary.getChars(bytes, dataLength, 0);
                        return type == Type.FAST
                            ? (CodePointTrie) new Fast16(index, data16, highStart, index3NullOffset, dataNullOffset)
                            : new Small16(index, data16, highStart, index3NullOffset, dataNullOffset);
                    }
                    case ValueWidth.BITS_32:
                    {
                        int[] data32 = ICUBinary.getInts(bytes, dataLength, 0);
                        return type == Type.FAST
                            ? (CodePointTrie) new Fast32(index, data32, highStart, index3NullOffset, dataNullOffset)
                            : new Small32(index, data32, highStart, index3NullOffset, dataNullOffset);
                    }
                    case ValueWidth.BITS_8:
                    {
                        byte[] data8 = ICUBinary.getBytes(bytes, dataLength, 0);
                        return type == Type.FAST
                            ? (CodePointTrie) new Fast8(index, data8, highStart, index3NullOffset, dataNullOffset)
                            : new Small8(index, data8, highStart, index3NullOffset, dataNullOffset);
                    }
                    default:
                        throw new AssertionError("should be unreachable");
                }
            }
            finally
            {
                bytes.order(outerByteOrder);
            }
        }

        /// <summary>
        /// Returns the trie type.
        /// </summary>
        /// <returns>the trie type</returns>
        /// <p/> @stable ICU 63
        public abstract Type getType();

        /// <summary>
        /// Returns the number of bits in a trie data value.
        /// </summary>
        /// <returns>the number of bits in a trie data value</returns>
        /// <p/> @stable ICU 63
        public ValueWidth getValueWidth()
        {
            return data.getValueWidth();
        }

        /// <inheritdoc />
        /// <p/> @stable ICU 63
        public override int get(int c)
        {
            return data.getFromIndex(cpIndex(c));
        }

        /// <summary>
        /// Returns a trie value for an ASCII code point, without range checking.
        /// </summary>
        /// <param name="c">the input code point; must be U+0000..U+007F</param>
        /// <returns>The ASCII code point's trie value</returns>
        /// <p/> @stable ICU 63
        public int asciiGet(int c)
        {
            return ascii[c];
        }

        private static readonly int MAX_UNICODE = 0x10ffff;

        private static readonly int ASCII_LIMIT = 0x80;

        private static int maybeFilterValue(int value, int trieNullValue, int nullValue,
                                            ValueFilter filter)
        {
            if (value == trieNullValue)
            {
                value = nullValue;
            }
            else if (filter != null)
            {
                value = filter.apply(value);
            }

            return value;
        }

        /// <inheritdoc />
        /// <p/> @stable ICU 63
        public override bool getRange(int start, ValueFilter filter, Range range)
        {
            if (start < 0 || MAX_UNICODE < start)
            {
                return false;
            }

            if (start >= highStart)
            {
                int _di = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
                int _value = data.getFromIndex(_di);
                if (filter != null)
                {
                    _value = filter.apply(_value);
                }

                range.set(start, MAX_UNICODE, _value);
                return true;
            }

            // ReSharper disable once LocalVariableHidesMember
            int nullValue = this.nullValue;
            if (filter != null)
            {
                nullValue = filter.apply(nullValue);
            }

            Type type = getType();

            int prevI3Block = -1;
            int prevBlock = -1;
            int c = start;
            // Initialize to make compiler happy. Real value when haveValue is true.
            int trieValue = 0, value = 0;
            bool haveValue = false;
            do
            {
                int i3Block;
                int i3;
                int i3BlockLength;
                int dataBlockLength;
                if (c <= 0xffff && (type == Type.FAST || c <= SMALL_MAX))
                {
                    i3Block = 0;
                    i3 = c >> FAST_SHIFT;
                    i3BlockLength = type == Type.FAST ? BMP_INDEX_LENGTH : SMALL_INDEX_LENGTH;
                    dataBlockLength = FAST_DATA_BLOCK_LENGTH;
                }
                else
                {
                    // Use the multi-stage index.
                    int i1 = c >> SHIFT_1;
                    if (type == Type.FAST)
                    {
                        assert(0xffff < c && c < highStart);
                        i1 += BMP_INDEX_LENGTH - OMITTED_BMP_INDEX_1_LENGTH;
                    }
                    else
                    {
                        assert(c < highStart && highStart > SMALL_LIMIT);
                        i1 += SMALL_INDEX_LENGTH;
                    }

                    i3Block = index[index[i1] + ((c >> SHIFT_2) & INDEX_2_MASK)];
                    if (i3Block == prevI3Block && (c - start) >= CP_PER_INDEX_2_ENTRY)
                    {
                        // The index-3 block is the same as the previous one, and filled with value.
                        assert((c & (CP_PER_INDEX_2_ENTRY - 1)) == 0);
                        c += CP_PER_INDEX_2_ENTRY;
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
                                range.set(start, c - 1, value);
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
                        c = (c + CP_PER_INDEX_2_ENTRY) & ~(CP_PER_INDEX_2_ENTRY - 1);
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
                        int group = (i3Block & 0x7fff) + (i3 & ~7) + (i3 >> 3);
                        int gi = i3 & 7;
                        block = (index[group++] << (2 + (2 * gi))) & 0x30000;
                        block |= index[group + gi];
                    }

                    if (block == prevBlock && (c - start) >= dataBlockLength)
                    {
                        // The block is the same as the previous one, and filled with value.
                        assert((c & (dataBlockLength - 1)) == 0);
                        c += dataBlockLength;
                    }
                    else
                    {
                        int dataMask = dataBlockLength - 1;
                        prevBlock = block;
                        if (block == dataNullOffset)
                        {
                            // This is the data null block.
                            if (haveValue)
                            {
                                if (nullValue != value)
                                {
                                    range.set(start, c - 1, value);
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
                            int _di = block + (c & dataMask);
                            int trieValue2 = data.getFromIndex(_di);
                            if (haveValue)
                            {
                                if (trieValue2 != trieValue)
                                {
                                    if (filter == null ||
                                        maybeFilterValue(trieValue2, this.nullValue, nullValue,
                                            filter) != value)
                                    {
                                        range.set(start, c - 1, value);
                                        return true;
                                    }

                                    trieValue = trieValue2; // may or may not help
                                }
                            }
                            else
                            {
                                trieValue = trieValue2;
                                value = maybeFilterValue(trieValue2, this.nullValue, nullValue, filter);
                                haveValue = true;
                            }

                            while ((++c & dataMask) != 0)
                            {
                                trieValue2 = data.getFromIndex(++_di);
                                if (trieValue2 != trieValue)
                                {
                                    if (filter == null ||
                                        maybeFilterValue(trieValue2, this.nullValue, nullValue,
                                            filter) != value)
                                    {
                                        range.set(start, c - 1, value);
                                        return true;
                                    }

                                    trieValue = trieValue2; // may or may not help
                                }
                            }
                        }
                    }
                } while (++i3 < i3BlockLength);
            } while (c < highStart);

            assert(haveValue);
            int di = dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            int highValue = data.getFromIndex(di);
            if (maybeFilterValue(highValue, this.nullValue, nullValue, filter) != value)
            {
                --c;
            }
            else
            {
                c = MAX_UNICODE;
            }

            range.set(start, c, value);
            return true;
        }

        /// <summary>
        /// Writes a representation of the trie to the output stream.
        /// * Inverse of <see cref="fromBinary"/>.
        /// </summary>
        /// <param name="os">the output stream</param>
        /// <returns>the number of bytes written</returns>
        /// <p/> @stable ICU 63
        public int toBinary(Stream os)
        {
            try
            {
                DataOutputStream dos = new DataOutputStream(os);

                // Write the UCPTrieHeader
                dos.writeInt(0x54726933); // signature="Tri3"
                dos.writeChar( // options
                    ((dataLength & 0xf0000) >> 4) |
                    ((dataNullOffset & 0xf0000) >> 8) |
                    ((int) getType() << 6) |
                    (int) getValueWidth());
                dos.writeChar(index.Length);
                dos.writeChar(dataLength);
                dos.writeChar(index3NullOffset);
                dos.writeChar(dataNullOffset);
                dos.writeChar(highStart >> SHIFT_2); // shiftedHighStart
                int length = 16; // sizeof(UCPTrieHeader)

                foreach (char i in index)
                {
                    dos.writeChar(i);
                }

                length += index.Length * 2;
                length += data.write(dos);
                return length;
            }
            catch (IOException e)
            {
                throw new ICUUncheckedIOException(e);
            }
        }

        /** @internal */
        internal static readonly int FAST_SHIFT = 6;

        /** Number of entries in a data block for code points below the fast limit. 64=0x40 @internal */
        internal static readonly int FAST_DATA_BLOCK_LENGTH = 1 << FAST_SHIFT;

        /** Mask for getting the lower bits for the in-fast-data-block offset. @internal */
        private static readonly int FAST_DATA_MASK = FAST_DATA_BLOCK_LENGTH - 1;

        /** @internal */
        private static readonly int SMALL_MAX = 0xfff;

        /**
         * Offset from dataLength (to be subtracted) for fetching the
         * value returned for out-of-range code points and ill-formed UTF-8/16.
         * @internal
         */
        private static readonly int ERROR_VALUE_NEG_DATA_OFFSET = 1;

        /**
         * Offset from dataLength (to be subtracted) for fetching the
         * value returned for code points highStart..U+10FFFF.
         * @internal
         */
        private static readonly int HIGH_VALUE_NEG_DATA_OFFSET = 2;

        // ucptrie_impl.h

        /** The length of the BMP index table. 1024=0x400 */
        private static readonly int BMP_INDEX_LENGTH = 0x10000 >> FAST_SHIFT;

        internal static readonly         int SMALL_LIMIT        = 0x1000;
        private static readonly int SMALL_INDEX_LENGTH = SMALL_LIMIT >> FAST_SHIFT;

        /** Shift size for getting the index-3 table offset. */
        internal static readonly int SHIFT_3 = 4;

        /** Shift size for getting the index-2 table offset. */
        private static readonly int SHIFT_2 = 5 + SHIFT_3;

        /** Shift size for getting the index-1 table offset. */
        private static readonly int SHIFT_1 = 5 + SHIFT_2;

        /**
         * Difference between two shift sizes,
         * for getting an index-2 offset from an index-3 offset. 5=9-4
         */
        internal static readonly int SHIFT_2_3 = SHIFT_2 - SHIFT_3;

        /**
         * Difference between two shift sizes,
         * for getting an index-1 offset from an index-2 offset. 5=14-9
         */
        internal static readonly int SHIFT_1_2 = SHIFT_1 - SHIFT_2;

        /**
         * Number of index-1 entries for the BMP. (4)
         * This part of the index-1 table is omitted from the serialized form.
         */
        private static readonly int OMITTED_BMP_INDEX_1_LENGTH = 0x10000 >> SHIFT_1;

        /** Number of entries in an index-2 block. 32=0x20 */
        internal static readonly int INDEX_2_BLOCK_LENGTH = 1 << SHIFT_1_2;

        /** Mask for getting the lower bits for the in-index-2-block offset. */
        internal static readonly int INDEX_2_MASK = INDEX_2_BLOCK_LENGTH - 1;

        /** Number of code points per index-2 table entry. 512=0x200 */
        internal static readonly int CP_PER_INDEX_2_ENTRY = 1 << SHIFT_2;

        /** Number of entries in an index-3 block. 32=0x20 */
        internal static readonly int INDEX_3_BLOCK_LENGTH = 1 << SHIFT_2_3;

        /** Mask for getting the lower bits for the in-index-3-block offset. */
        private static readonly int INDEX_3_MASK = INDEX_3_BLOCK_LENGTH - 1;

        /** Number of entries in a small data block. 16=0x10 */
        internal static readonly int SMALL_DATA_BLOCK_LENGTH = 1 << SHIFT_3;

        /** Mask for getting the lower bits for the in-small-data-block offset. */
        internal static readonly int SMALL_DATA_MASK = SMALL_DATA_BLOCK_LENGTH - 1;

        // ucptrie_impl.h: Constants for use with UCPTrieHeader.options.
        private static readonly int OPTIONS_DATA_LENGTH_MASK      = 0xf000;
        private static readonly int OPTIONS_DATA_NULL_OFFSET_MASK = 0xf00;
        private static readonly int OPTIONS_RESERVED_MASK         = 0x38;
        private static readonly int OPTIONS_VALUE_BITS_MASK       = 7;

        /**
         * Value for index3NullOffset which indicates that there is no index-3 null block.
         * Bit 15 is unused for this value because this bit is used if the index-3 contains
         * 18-bit indexes.
         */
        internal static readonly int NO_INDEX3_NULL_OFFSET = 0x7fff;

        internal static readonly int NO_DATA_NULL_OFFSET = 0xfffff;

        public abstract class Data
        {
            internal abstract ValueWidth getValueWidth();
            internal abstract int getDataLength();
            internal abstract int getFromIndex(int index);
            internal abstract int write(DataOutputStream dos);
        }

        private class Data16 : Data
        {
            char[] array;

            internal Data16(char[] a)
            {
                array = a;
            }

            internal override ValueWidth getValueWidth()
            {
                return ValueWidth.BITS_16;
            }

            internal override int getDataLength()
            {
                return array.Length;
            }

            internal override int getFromIndex(int index)
            {
                return array[index];
            }

            internal override int write(DataOutputStream dos)
            {
                foreach (char v in array)
                {
                    dos.writeChar(v);
                }

                return array.Length * 2;
            }
        }

        private class Data32 : Data
        {
            int[] array;

            public Data32(int[] a)
            {
                array = a;
            }

            internal override ValueWidth getValueWidth()
            {
                return ValueWidth.BITS_32;
            }

            internal override int getDataLength()
            {
                return array.Length;
            }

            internal override int getFromIndex(int index)
            {
                return array[index];
            }

            internal override int write(DataOutputStream dos)
            {
                foreach (int v in array)
                {
                    dos.writeInt(v);
                }

                return array.Length * 4;
            }
        }

        private class Data8 : Data
        {
            byte[] array;

            internal Data8(byte[] a)
            {
                array = a;
            }

            internal override ValueWidth getValueWidth()
            {
                return ValueWidth.BITS_8;
            }

            internal override int getDataLength()
            {
                return array.Length;
            }

            internal override int getFromIndex(int index)
            {
                return array[index] & 0xff;
            }

            internal override int write(DataOutputStream dos)
            {
                foreach (byte v in array)
                {
                    dos.writeByte(v);
                }

                return array.Length;
            }
        }

        /** @internal */
        private readonly int[] ascii;

        /** @internal */
        private readonly char[] index;

        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete] protected readonly Data data;

        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete] protected readonly int dataLength;

        /**
         * Start of the last range which ends at U+10FFFF.
         */
        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete] protected readonly int highStart;

        /**
         * Internal index-3 null block offset.
         * Set to an impossibly high value (e.g., 0xffff) if there is no dedicated index-3 null block.
         * @internal
         */
        private readonly int index3NullOffset;

        /**
         * Internal data null block offset, not shifted.
         * Set to an impossibly high value (e.g., 0xfffff) if there is no dedicated data null block.
         * @internal
         */
        private readonly int dataNullOffset;

        /** @internal */
        private readonly int nullValue;

        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete]
        protected int fastIndex(int c)
        {
            return index[c >> FAST_SHIFT] + (c & FAST_DATA_MASK);
        }

        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete]
        protected int smallIndex(Type type, int c)
        {
            // Split into two methods to make this part inline-friendly.
            // In C, this part is a macro.
            if (c >= highStart)
            {
                return dataLength - HIGH_VALUE_NEG_DATA_OFFSET;
            }

            return internalSmallIndex(type, c);
        }

        private int internalSmallIndex(Type type, int c)
        {
            int i1 = c >> SHIFT_1;
            if (type == Type.FAST)
            {
                assert(0xffff < c && c < highStart);
                i1 += BMP_INDEX_LENGTH - OMITTED_BMP_INDEX_1_LENGTH;
            }
            else
            {
                assert(0 <= c && c < highStart && highStart > SMALL_LIMIT);
                i1 += SMALL_INDEX_LENGTH;
            }

            int i3Block = index[index[i1] + ((c >> SHIFT_2) & INDEX_2_MASK)];
            int i3 = (c >> SHIFT_3) & INDEX_3_MASK;

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

        /// <remarks>@internal This API is ICU internal only.</remarks>
        [Obsolete]
        protected abstract int cpIndex(int c);

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointMap.Type.FAST"/>
        /// </summary>
        /// <p/> @stable ICU 63
        public abstract class Fast : CodePointTrie
        {
            internal Fast(char[] index, Data data, int highStart,
                          int index3NullOffset, int dataNullOffset)
                : base(index, data, highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with type <see cref="CodePointTrie.Type.FAST"/>.
            /// </summary>
            /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
            ///                  if the valueWidth does not match the binary data;
            ///                  use null to accept any data value width</param>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Fast fromBinary(ValueWidth valueWidth, ByteBuffer bytes)
            {
                return (Fast) CodePointTrie.fromBinary(Type.FAST, valueWidth, bytes);
            }

            /// <returns><see cref="Type.FAST"/></returns>
            /// <p/> @stable ICU 63
            public override Type getType()
            {
                return Type.FAST;
            }

            /// <summary>
            /// Returns a trie value for a BMP code point (U+0000..U+FFFF), without range checking.
            /// Can be used to look up a value for a UTF-16 code unit if other parts of
            /// the string processing check for surrogates.
            /// </summary>
            /// <param name="c">the input code point, must be U+0000..U+FFFF</param>
            /// <returns>The BMP code point's trie value.</returns>
            /// <p/> @stable ICU 63
            public abstract int bmpGet(int c);

            /// <summary>
            /// Returns a trie value for a supplementary code point (U+10000..U+10FFFF),
            /// without range checking.
            /// </summary>
            /// <param name="c">the input code point, must be U+10000..U+10FFFF</param>
            /// <returns>The supplementary code point's trie value.</returns>
            /// <p/> @stable ICU 63
            public abstract int suppGet(int c);

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete]
            protected override int cpIndex(int c)
            {
                if (c >= 0)
                {
                    if (c <= 0xffff)
                    {
                        return fastIndex(c);
                    }
                    else if (c <= 0x10ffff)
                    {
                        return smallIndex(Type.FAST, c);
                    }
                }

                return dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override StringIterator stringIterator(CharSequence s, int sIndex)
            {
                return new FastStringIterator(this, s, sIndex);
            }

            private class FastStringIterator : StringIterator
            {
                private readonly CodePointTrie trie;

                internal FastStringIterator(Fast fast, CharSequence s, int sIndex)
                    : base(fast, s, sIndex)
                {
                    trie = fast;
                }

                public override bool next()
                {
                    if (sIndex >= s.length())
                    {
                        return false;
                    }

                    char lead = s.charAt(sIndex++);
                    c = lead;
                    int dataIndex;
                    if (!Character.isSurrogate(lead))
                    {
                        dataIndex = trie.fastIndex(c);
                    }
                    else
                    {
                        char trail;
                        if (Normalizer2Impl.UTF16Plus.isSurrogateLead(lead) && sIndex < s.length() &&
                            Character.isLowSurrogate(trail = s.charAt(sIndex)))
                        {
                            ++sIndex;
                            c = Character.toCodePoint(lead, trail);
                            dataIndex = trie.smallIndex(Type.FAST, c);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    value = trie.data.getFromIndex(dataIndex);
                    return true;
                }

                public override bool previous()
                {
                    if (sIndex <= 0)
                    {
                        return false;
                    }

                    char trail = s.charAt(--sIndex);
                    c = trail;
                    int dataIndex;
                    if (!Character.isSurrogate(trail))
                    {
                        dataIndex = trie.fastIndex(c);
                    }
                    else
                    {
                        char lead;
                        if (!Normalizer2Impl.UTF16Plus.isSurrogateLead(trail) && sIndex > 0 &&
                            Character.isHighSurrogate(lead = s.charAt(sIndex - 1)))
                        {
                            --sIndex;
                            c = Character.toCodePoint(lead, trail);
                            dataIndex = trie.smallIndex(Type.FAST, c);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    value = trie.data.getFromIndex(dataIndex);
                    return true;
                }
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.SMALL"/>
        /// </summary>
        /// <p/> @stable ICU 63
        public abstract class Small : CodePointTrie
        {
            internal Small(char[] index, Data data, int highStart,
                           int index3NullOffset, int dataNullOffset)
                : base(index, data, highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.SMALL"/>.
            /// </summary>
            /// <param name="valueWidth">selects the number of bits in a data value; this method throws an exception
            ///                  if the valueWidth does not match the binary data;
            ///                  use null to accept any data value width</param>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Small fromBinary(ValueWidth valueWidth, ByteBuffer bytes)
            {
                return (Small) CodePointTrie.fromBinary(Type.SMALL, valueWidth, bytes);
            }

            /// <returns><see cref="CodePointTrie.Type.SMALL"/></returns>
            /// <p/> @stable ICU 63
            public override Type getType()
            {
                return Type.SMALL;
            }

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete]
            protected override int cpIndex(int c)
            {
                if (c >= 0)
                {
                    if (c <= SMALL_MAX)
                    {
                        return fastIndex(c);
                    }
                    else if (c <= 0x10ffff)
                    {
                        return smallIndex(Type.SMALL, c);
                    }
                }

                return dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override StringIterator stringIterator(CharSequence s, int sIndex)
            {
                return new SmallStringIterator(this, s, sIndex);
            }

            private class SmallStringIterator : StringIterator
            {
                private readonly CodePointTrie trie;

                internal SmallStringIterator(Small small, CharSequence s, int sIndex)
                    : base(small, s, sIndex)
                {
                    trie = small;
                }

                public override bool next()
                {
                    if (sIndex >= s.length())
                    {
                        return false;
                    }

                    char lead = s.charAt(sIndex++);
                    c = lead;
                    int dataIndex;
                    if (!Character.isSurrogate(lead))
                    {
                        dataIndex = trie.cpIndex(c);
                    }
                    else
                    {
                        char trail;
                        if (Normalizer2Impl.UTF16Plus.isSurrogateLead(lead) && sIndex < s.length() &&
                            Character.isLowSurrogate(trail = s.charAt(sIndex)))
                        {
                            ++sIndex;
                            c = Character.toCodePoint(lead, trail);
                            dataIndex = trie.smallIndex(Type.SMALL, c);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    value = trie.data.getFromIndex(dataIndex);
                    return true;
                }

                public override bool previous()
                {
                    if (sIndex <= 0)
                    {
                        return false;
                    }

                    char trail = s.charAt(--sIndex);
                    c = trail;
                    int dataIndex;
                    if (!Character.isSurrogate(trail))
                    {
                        dataIndex = trie.cpIndex(c);
                    }
                    else
                    {
                        char lead;
                        if (!Normalizer2Impl.UTF16Plus.isSurrogateLead(trail) && sIndex > 0 &&
                            Character.isHighSurrogate(lead = s.charAt(sIndex - 1)))
                        {
                            --sIndex;
                            c = Character.toCodePoint(lead, trail);
                            dataIndex = trie.smallIndex(Type.SMALL, c);
                        }
                        else
                        {
                            dataIndex = trie.dataLength - ERROR_VALUE_NEG_DATA_OFFSET;
                        }
                    }

                    value = trie.data.getFromIndex(dataIndex);
                    return true;
                }
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.FAST"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_16"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Fast16 : Fast
        {
            private char[] dataArray;

            internal Fast16(char[] index, char[] data16, int highStart,
                            int index3NullOffset, int dataNullOffset)
                : base(index, new Data16(data16), highStart, index3NullOffset, dataNullOffset)
            {
                this.dataArray = data16;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.FAST"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_16"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Fast16 fromBinary(ByteBuffer bytes)
            {
                return (Fast16) CodePointTrie.fromBinary(Type.FAST, ValueWidth.BITS_16, bytes);
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int get(int c)
            {
                return dataArray[cpIndex(c)];
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int bmpGet(int c)
            {
                assert(0 <= c && c <= 0xffff);
                return dataArray[fastIndex(c)];
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int suppGet(int c)
            {
                assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[smallIndex(Type.FAST, c)];
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.FAST"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_32"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Fast32 : Fast
        {
            private readonly int[] dataArray;

            internal Fast32(char[] index, int[] data32, int highStart,
                            int index3NullOffset, int dataNullOffset)
                : base(index, new Data32(data32), highStart, index3NullOffset, dataNullOffset)
            {
                this.dataArray = data32;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.FAST"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_32"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Fast32 fromBinary(ByteBuffer bytes)
            {
                return (Fast32) CodePointTrie.fromBinary(Type.FAST, ValueWidth.BITS_32, bytes);
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int get(int c)
            {
                return dataArray[cpIndex(c)];
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int bmpGet(int c)
            {
                assert(0 <= c && c <= 0xffff);
                return dataArray[fastIndex(c)];
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int suppGet(int c)
            {
                assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[smallIndex(Type.FAST, c)];
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.FAST"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_8"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Fast8 : Fast
        {
            private readonly byte[] dataArray;

            internal Fast8(char[] index, byte[] data8, int highStart,
                           int index3NullOffset, int dataNullOffset)
                : base(index, new Data8(data8), highStart, index3NullOffset, dataNullOffset)
            {
                this.dataArray = data8;
            }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.FAST"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_8"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Fast8 fromBinary(ByteBuffer bytes)
            {
                return (Fast8) CodePointTrie.fromBinary(Type.FAST, ValueWidth.BITS_8, bytes);
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int get(int c)
            {
                return dataArray[cpIndex(c)] & 0xff;
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int bmpGet(int c)
            {
                assert(0 <= c && c <= 0xffff);
                return dataArray[fastIndex(c)] & 0xff;
            }

            /// <inheritdoc />
            /// <p/> @stable ICU 63
            public override int suppGet(int c)
            {
                assert(0x10000 <= c && c <= 0x10ffff);
                return dataArray[smallIndex(Type.FAST, c)] & 0xff;
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.SMALL"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_16"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Small16 : Small
        {
            internal Small16(char[] index, char[] data16, int highStart,
                             int index3NullOffset, int dataNullOffset)
                : base(index, new Data16(data16), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.SMALL"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_16"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Small16 fromBinary(ByteBuffer bytes)
            {
                return (Small16) CodePointTrie.fromBinary(Type.SMALL, ValueWidth.BITS_16, bytes);
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.SMALL"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_32"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Small32 : Small
        {
            internal Small32(char[] index, int[] data32, int highStart,
                             int index3NullOffset, int dataNullOffset)
                : base(index, new Data32(data32), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.SMALL"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_32"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Small32 fromBinary(ByteBuffer bytes)
            {
                return (Small32) CodePointTrie.fromBinary(Type.SMALL, ValueWidth.BITS_32, bytes);
            }
        }

        /// <summary>
        /// A CodePointTrie with <see cref="CodePointTrie.Type.SMALL"/> and
        /// <see cref="CodePointTrie.getValueWidth.BITS_8"/>.
        /// </summary>
        /// <p/> @stable ICU 63
        public class Small8 : Small
        {
            internal Small8(char[] index, byte[] data8, int highStart,
                            int index3NullOffset, int dataNullOffset)
                : base(index, new Data8(data8), highStart, index3NullOffset, dataNullOffset)
            { }

            /// <summary>
            /// Creates a trie from its binary form.
            /// Same as <see cref="CodePointTrie.fromBinary"/> with <see cref="CodePointTrie.Type.SMALL"/>
            /// and <see cref="CodePointTrie.ValueWidth.BITS_8"/>.
            /// </summary>
            /// <param name="bytes">a buffer containing the binary data of a CodePointTrie</param>
            /// <returns>the trie</returns>
            /// <p/> @stable ICU 63
            public static Small8 fromBinary(ByteBuffer bytes)
            {
                return (Small8) CodePointTrie.fromBinary(Type.SMALL, ValueWidth.BITS_8, bytes);
            }
        }
    }
}