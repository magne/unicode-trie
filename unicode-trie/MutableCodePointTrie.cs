using System;
using System.Diagnostics;

namespace CodeHive.unicode_trie
{
    /// <summary>
    /// Mutable Unicode code point trie.
    /// Fast map from Unicode code points (U+0000..U+10FFFF) to 32-bit integer values.
    /// For details see http://site.icu-project.org/design/struct/utrie
    ///
    /// <p/>Setting values (especially ranges) and lookup is fast.
    /// The mutable trie is only somewhat space-efficient.
    /// It builds a compacted, immutable <see cref="CodePointTrie"/>.
    ///
    /// <p/>This trie can be modified while iterating over its contents.
    /// For example, it is possible to merge its values with those from another
    /// set of ranges (e.g., another <see cref="CodePointMap"/>:
    /// Iterate over those source ranges; for each of them iterate over this trie;
    /// add the source value into the value of each trie range.
    /// </summary>
    public sealed class MutableCodePointTrie : CodePointMap, ICloneable
    {
        /// <summary>
        /// Constructs a mutable trie that initially maps each Unicode code point to the same value.
        /// It uses 32-bit data values until <see cref="BuildImmutable"/>is called.
        ///
        /// <p/><see cref="BuildImmutable"/> takes a valueWidth parameter which determines the
        /// number of bits in the data value in the resulting <see cref="CodePointTrie"/>.
        /// </summary>
        /// <param name="initialValue">the initial value that is set for all code points</param>
        /// <param name="errorValue">the value for out-of-range code points and ill-formed UTF-8/16</param>
        public MutableCodePointTrie(int initialValue = 0, int errorValue = 0)
        {
            index = new int[BMP_I_LIMIT];
            index3NullOffset = -1;
            data = new int[INITIAL_DATA_LENGTH];
            dataNullOffset = -1;
            origInitialValue = initialValue;
            this.initialValue = initialValue;
            this.errorValue = errorValue;
            highValue = initialValue;
        }

        /// <summary>
        /// Clones this mutable trie.
        /// </summary>
        /// <returns>the clone</returns>
        public object Clone()
        {
            var builder = (MutableCodePointTrie) MemberwiseClone();
            var iCapacity = highStart <= BMP_LIMIT ? BMP_I_LIMIT : I_LIMIT;
            builder.index = new int[iCapacity];
            builder.flags = new byte[UNICODE_LIMIT >> CodePointTrie.SHIFT_3];
            for (int i = 0, iLimit = highStart >> CodePointTrie.SHIFT_3; i < iLimit; ++i)
            {
                builder.index[i] = index[i];
                builder.flags[i] = flags[i];
            }

            builder.index3NullOffset = index3NullOffset;
            builder.data = (int[]) data.Clone();
            builder.dataLength = dataLength;
            builder.dataNullOffset = dataNullOffset;
            builder.origInitialValue = origInitialValue;
            builder.initialValue = initialValue;
            builder.errorValue = errorValue;
            builder.highStart = highStart;
            builder.highValue = highValue;
            Debug.Assert(index16 == null);
            return builder;
        }

        /// <summary>
        /// Creates a mutable trie with the same contents as the <see cref="CodePointMap"/>.
        /// </summary>
        /// <param name="map">the source map or trie</param>
        /// <returns>the mutable trie</returns>
        public static MutableCodePointTrie FromCodePointMap(CodePointMap map)
        {
            // TODO: Consider special code branch for map instanceof CodePointTrie?
            // Use the highValue as the initialValue to reduce the highStart.
            var errorValue = map.Get(-1);
            var initialValue = map.Get(MAX_UNICODE);
            var mutableTrie = new MutableCodePointTrie(initialValue, errorValue);
            var range = new Range();
            var start = 0;
            while (map.GetRange(start, null, range))
            {
                var end = range.End;
                var value = range.Value;
                if (value != initialValue)
                {
                    if (start == end)
                    {
                        mutableTrie.Set(start, value);
                    }
                    else
                    {
                        mutableTrie.SetRange(start, end, value);
                    }
                }

                start = end + 1;
            }

            return mutableTrie;
        }

        private void Clear()
        {
            index3NullOffset = dataNullOffset = -1;
            dataLength = 0;
            highValue = initialValue = origInitialValue;
            highStart = 0;
            index16 = null;
        }

        /// <inheritdoc />
        public override int Get(int c)
        {
            if (c < 0 || MAX_UNICODE < c)
            {
                return errorValue;
            }

            if (c >= highStart)
            {
                return highValue;
            }

            var i = c >> CodePointTrie.SHIFT_3;
            if (flags[i] == ALL_SAME)
            {
                return index[i];
            }

            return data[index[i] + (c & CodePointTrie.SMALL_DATA_MASK)];
        }

        private static int MaybeFilterValue(int value, int initialValue, int nullValue, Func<int, int> filter)
        {
            if (value == initialValue)
            {
                value = nullValue;
            }
            else if (filter != null)
            {
                value = filter(value);
            }

            return value;
        }

        /// <inheritdoc />
        /// <remarks>The trie can be modified between calls to this function.</remarks>
        public override bool GetRange(int start, Func<int, int> filter, Range range)
        {
            if (start < 0 || MAX_UNICODE < start)
            {
                return false;
            }

            var value = 0;
            if (start >= highStart)
            {
                value = highValue;
                if (filter != null)
                {
                    value = filter(value);
                }

                range.Set(start, MAX_UNICODE, value);
                return true;
            }

            var nullValue = initialValue;
            if (filter != null)
            {
                nullValue = filter(nullValue);
            }

            var c = start;
            // Initialize to make compiler happy. Real value when haveValue is true.
            int trieValue = 0;
            var haveValue = false;
            var i = c >> CodePointTrie.SHIFT_3;
            do
            {
                if (flags[i] == ALL_SAME)
                {
                    var trieValue2 = index[i];
                    if (haveValue)
                    {
                        if (trieValue2 != trieValue)
                        {
                            if (filter == null ||
                                MaybeFilterValue(trieValue2, initialValue, nullValue, filter) != value)
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
                        value = MaybeFilterValue(trieValue2, initialValue, nullValue, filter);
                        haveValue = true;
                    }

                    c = (c + CodePointTrie.SMALL_DATA_BLOCK_LENGTH) & ~CodePointTrie.SMALL_DATA_MASK;
                }
                else /* MIXED */
                {
                    var di = index[i] + (c & CodePointTrie.SMALL_DATA_MASK);
                    var trieValue2 = data[di];
                    if (haveValue)
                    {
                        if (trieValue2 != trieValue)
                        {
                            if (filter == null ||
                                MaybeFilterValue(trieValue2, initialValue, nullValue,
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
                        value = MaybeFilterValue(trieValue2, initialValue, nullValue, filter);
                        haveValue = true;
                    }

                    while ((++c & CodePointTrie.SMALL_DATA_MASK) != 0)
                    {
                        trieValue2 = data[++di];
                        if (trieValue2 != trieValue)
                        {
                            if (filter == null ||
                                MaybeFilterValue(trieValue2, initialValue, nullValue,
                                    filter) != value)
                            {
                                range.Set(start, c - 1, value);
                                return true;
                            }

                            trieValue = trieValue2; // may or may not help
                        }
                    }
                }

                ++i;
            } while (c < highStart);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            Debug.Assert(haveValue);
            if (MaybeFilterValue(highValue, initialValue, nullValue, filter) != value)
            {
                range.Set(start, c - 1, value);
            }
            else
            {
                range.Set(start, MAX_UNICODE, value);
            }

            return true;
        }

        private void WriteBlock(int block, int value)
        {
            var limit = block + CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
            Array.Fill(data, value, block, limit - block);
        }

        /// <summary>
        /// Sets a value for a code point.
        /// </summary>
        /// <param name="c">the code point</param>
        /// <param name="value"> the value</param>
        public void Set(int c, int value)
        {
            if (c < 0 || MAX_UNICODE < c)
            {
                throw new ArgumentException("invalid code point");
            }

            EnsureHighStart(c);
            var block = GetDataBlock(c >> CodePointTrie.SHIFT_3);
            data[block + (c & CodePointTrie.SMALL_DATA_MASK)] = value;
        }

        private void FillBlock(int block, int start, int limit, int value)
        {
            Array.Fill(data, value, block + start, limit - start);
        }

        /// <summary>
        /// Sets a value for each code point [start..end].
        /// Faster and more space-efficient than setting the value for each code point separately.
        /// </summary>
        /// <param name="start">the first code point to get the value</param>
        /// <param name="end">the last code point to get the value (inclusive)</param>
        /// <param name="value">the value</param>
        public void SetRange(int start, int end, int value)
        {
            if (start < 0 || MAX_UNICODE < start || end < 0 || MAX_UNICODE < end || start > end)
            {
                throw new ArgumentException("invalid code point range");
            }

            EnsureHighStart(end);

            var limit = end + 1;
            if ((start & CodePointTrie.SMALL_DATA_MASK) != 0)
            {
                // Set partial block at [start..following block boundary].
                var block = GetDataBlock(start >> CodePointTrie.SHIFT_3);
                var nextStart = (start + CodePointTrie.SMALL_DATA_MASK) & ~CodePointTrie.SMALL_DATA_MASK;
                if (nextStart <= limit)
                {
                    FillBlock(block, start & CodePointTrie.SMALL_DATA_MASK, CodePointTrie.SMALL_DATA_BLOCK_LENGTH, value);
                    start = nextStart;
                }
                else
                {
                    FillBlock(block, start & CodePointTrie.SMALL_DATA_MASK, limit & CodePointTrie.SMALL_DATA_MASK, value);
                    return;
                }
            }

            // Number of positions in the last, partial block.
            var rest = limit & CodePointTrie.SMALL_DATA_MASK;

            // Round down limit to a block boundary.
            limit &= ~CodePointTrie.SMALL_DATA_MASK;

            // Iterate over all-value blocks.
            while (start < limit)
            {
                var i = start >> CodePointTrie.SHIFT_3;
                if (flags[i] == ALL_SAME)
                {
                    index[i] = value;
                }
                else /* MIXED */
                {
                    FillBlock(index[i], 0, CodePointTrie.SMALL_DATA_BLOCK_LENGTH, value);
                }

                start += CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
            }

            if (rest > 0)
            {
                // Set partial block at [last block boundary..limit[.
                var block = GetDataBlock(start >> CodePointTrie.SHIFT_3);
                FillBlock(block, 0, rest, value);
            }
        }

        /// <summary>
        /// Compacts the data and builds an immutable <see cref="CodePointTrie"/> according to the parameters.
        /// After this, the mutable trie will be empty.
        ///
        /// <p/>The mutable trie stores 32-bit values until <code>buildImmutable()</code> is called.
        /// If values shorter than 32 bits are to be stored in the immutable trie,
        /// then the upper bits are discarded.
        /// For example, when the mutable trie contains values 0x81, -0x7f, and 0xa581,
        /// and the value width is 8 bits, then each of these is stored as 0x81
        /// and the immutable trie will return that as an unsigned value.
        /// (Some implementations may want to make productive temporary use of the upper bits
        /// until <code>buildImmutable()</code> discards them.)
        ///
        /// <p/>Not every possible set of mappings can be built into a <see cref="CodePointTrie"/>,
        /// because of limitations resulting from speed and space optimizations.
        /// Every Unicode assigned character can be mapped to a unique value.
        /// Typical data yields data structures far smaller than the limitations.
        ///
        /// <p/>It is possible to construct extremely unusual mappings that exceed the
        /// data structure limits.
        /// In such a case this function will throw an exception.
        /// </summary>
        /// <param name="type">selects the trie type</param>
        /// <param name="valueWidth">selects the number of bits in a trie data value; if smaller than 32 bits,
        ///                   then the values stored in the trie will be truncated first</param>
        /// <returns>The immutable trie</returns>
        /// <seealso cref="FromCodePointMap"/>
        public CodePointTrie BuildImmutable(CodePointTrie.Kind type, CodePointTrie.ValueWidth valueWidth)
        {
            try
            {
                return Build(type, valueWidth);
            }
            finally
            {
                Clear();
            }
        }

        private const int MAX_UNICODE = 0x10ffff;

        private const int UNICODE_LIMIT = 0x110000;
        private const int BMP_LIMIT     = 0x10000;
        private const int ASCII_LIMIT   = 0x80;

        private const int I_LIMIT       = UNICODE_LIMIT >> CodePointTrie.SHIFT_3;
        private const int BMP_I_LIMIT   = BMP_LIMIT >> CodePointTrie.SHIFT_3;
        private const int ASCII_I_LIMIT = ASCII_LIMIT >> CodePointTrie.SHIFT_3;

        private const int SMALL_DATA_BLOCKS_PER_BMP_BLOCK = (1 << (CodePointTrie.FAST_SHIFT - CodePointTrie.SHIFT_3));

        // Flag values for data blocks.
        private const byte ALL_SAME = 0;
        private const byte MIXED    = 1;
        private const byte SAME_AS  = 2;

        /** Start with allocation of 16k data entries. */
        private const int INITIAL_DATA_LENGTH = (1 << 14);

        /** Grow about 8x each time. */
        private const int MEDIUM_DATA_LENGTH = (1 << 17);

        /**
         * Maximum length of the build-time data array.
         * One entry per 0x110000 code points.
         */
        private const int MAX_DATA_LENGTH = UNICODE_LIMIT;

        // Flag values for index-3 blocks while compacting/building.
        private const byte I3_NULL = 0;
        private const byte I3_BMP  = 1;
        private const byte I3_16   = 2;
        private const byte I3_18   = 3;

        private const int INDEX_3_18BIT_BLOCK_LENGTH = CodePointTrie.INDEX_3_BLOCK_LENGTH + CodePointTrie.INDEX_3_BLOCK_LENGTH / 8;

        private int[] index;
        private int   index3NullOffset;
        private int[] data;
        private int   dataLength;
        private int   dataNullOffset;

        private int origInitialValue;
        private int initialValue;
        private int errorValue;
        private int highStart;
        private int highValue;

        /** Temporary array while building the final data. */
        private ushort[] index16;

        private byte[] flags = new byte[UNICODE_LIMIT >> CodePointTrie.SHIFT_3];

        private void EnsureHighStart(int c)
        {
            if (c >= highStart)
            {
                // Round up to a CodePointTrie.CP_PER_INDEX_2_ENTRY boundary to simplify compaction.
                c = (c + CodePointTrie.CP_PRE_INDEX_2_ENTRY) & ~(CodePointTrie.CP_PRE_INDEX_2_ENTRY - 1);
                var i = highStart >> CodePointTrie.SHIFT_3;
                var iLimit = c >> CodePointTrie.SHIFT_3;
                if (iLimit > index.Length)
                {
                    var newIndex = new int[I_LIMIT];
                    for (var j = 0; j < i; ++j)
                    {
                        newIndex[j] = index[j];
                    }

                    index = newIndex;
                }

                do
                {
                    flags[i] = ALL_SAME;
                    index[i] = initialValue;
                } while (++i < iLimit);

                highStart = c;
            }
        }

        private int AllocDataBlock(int blockLength)
        {
            var newBlock = dataLength;
            var newTop = newBlock + blockLength;
            if (newTop > data.Length)
            {
                int capacity;
                if (data.Length < MEDIUM_DATA_LENGTH)
                {
                    capacity = MEDIUM_DATA_LENGTH;
                }
                else if (data.Length < MAX_DATA_LENGTH)
                {
                    capacity = MAX_DATA_LENGTH;
                }
                else
                {
                    // Should never occur.
                    // Either MAX_DATA_LENGTH is incorrect,
                    // or the code writes more values than should be possible.
                    throw new InvalidOperationException("should be unreachable");
                }

                var newData = new int[capacity];
                for (var j = 0; j < dataLength; ++j)
                {
                    newData[j] = data[j];
                }

                data = newData;
            }

            dataLength = newTop;
            return newBlock;
        }

        /**
         * No error checking for illegal arguments.
         * The Java version always returns non-negative values.
         */
        private int GetDataBlock(int i)
        {
            if (flags[i] == MIXED)
            {
                return index[i];
            }

            if (i < BMP_I_LIMIT)
            {
                var newBlock = AllocDataBlock(CodePointTrie.FAST_DATA_BLOCK_LENGTH);
                var iStart = i & ~(SMALL_DATA_BLOCKS_PER_BMP_BLOCK - 1);
                var iLimit = iStart + SMALL_DATA_BLOCKS_PER_BMP_BLOCK;
                do
                {
                    Debug.Assert(flags[iStart] == ALL_SAME);
                    WriteBlock(newBlock, index[iStart]);
                    flags[iStart] = MIXED;
                    index[iStart++] = newBlock;
                    newBlock += CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
                } while (iStart < iLimit);

                return index[i];
            }
            else
            {
                var newBlock = AllocDataBlock(CodePointTrie.SMALL_DATA_BLOCK_LENGTH);
                if (newBlock < 0)
                {
                    return newBlock;
                }

                WriteBlock(newBlock, index[i]);
                flags[i] = MIXED;
                index[i] = newBlock;
                return newBlock;
            }
        }

        // compaction --------------------------------------------------------------

        private void MaskValues(int mask)
        {
            initialValue &= mask;
            errorValue &= mask;
            highValue &= mask;
            var iLimit = highStart >> CodePointTrie.SHIFT_3;
            for (var i = 0; i < iLimit; ++i)
            {
                if (flags[i] == ALL_SAME)
                {
                    index[i] &= mask;
                }
            }

            for (var i = 0; i < dataLength; ++i)
            {
                data[i] &= mask;
            }
        }

        private static bool EqualBlocks(int[] s, int si, int[] t, int ti, int length)
        {
            while (length > 0 && s[si] == t[ti])
            {
                ++si;
                ++ti;
                --length;
            }

            return length == 0;
        }

        private static bool EqualBlocks(ushort[] s, int si, int[] t, int ti, int length)
        {
            while (length > 0 && s[si] == t[ti])
            {
                ++si;
                ++ti;
                --length;
            }

            return length == 0;
        }

        private static bool EqualBlocks(ushort[] s, int si, ushort[] t, int ti, int length)
        {
            while (length > 0 && s[si] == t[ti])
            {
                ++si;
                ++ti;
                --length;
            }

            return length == 0;
        }

        private static bool AllValuesSameAs(int[] p, int pi, int length, int value)
        {
            var pLimit = pi + length;
            while (pi < pLimit && p[pi] == value)
            {
                ++pi;
            }

            return pi == pLimit;
        }

        /** Search for an identical block. */
        private static int FindSameBlock(ushort[] p, int pStart, int length,
                                         ushort[] q, int qStart, int blockLength)
        {
            // Ensure that we do not even partially get past length.
            length -= blockLength;

            while (pStart <= length)
            {
                if (EqualBlocks(p, pStart, q, qStart, blockLength))
                {
                    return pStart;
                }

                ++pStart;
            }

            return -1;
        }

        private static int FindAllSameBlock(int[] p, int start, int limit,
                                            int value, int blockLength)
        {
            // Ensure that we do not even partially get past limit.
            limit -= blockLength;

            for (var block = start; block <= limit; ++block)
            {
                if (p[block] == value)
                {
                    for (var i = 1;; ++i)
                    {
                        if (i == blockLength)
                        {
                            return block;
                        }

                        if (p[block + i] != value)
                        {
                            block += i;
                            break;
                        }
                    }
                }
            }

            return -1;
        }

        /**
         * Look for maximum overlap of the beginning of the other block
         * with the previous, adjacent block.
         */
        private static int GetOverlap(int[] p, int length, int[] q, int qStart, int blockLength)
        {
            var overlap = blockLength - 1;
            Debug.Assert(overlap <= length);
            while (overlap > 0 && !EqualBlocks(p, length - overlap, q, qStart, overlap))
            {
                --overlap;
            }

            return overlap;
        }

        private static int GetOverlap(ushort[] p, int length, int[] q, int qStart, int blockLength)
        {
            var overlap = blockLength - 1;
            Debug.Assert(overlap <= length);
            while (overlap > 0 && !EqualBlocks(p, length - overlap, q, qStart, overlap))
            {
                --overlap;
            }

            return overlap;
        }

        private static int GetOverlap(ushort[] p, int length, ushort[] q, int qStart, int blockLength)
        {
            var overlap = blockLength - 1;
            Debug.Assert(overlap <= length);
            while (overlap > 0 && !EqualBlocks(p, length - overlap, q, qStart, overlap))
            {
                --overlap;
            }

            return overlap;
        }

        private static int GetAllSameOverlap(int[] p, int length, int value, int blockLength)
        {
            var min = length - (blockLength - 1);
            var i = length;
            while (min < i && p[i - 1] == value)
            {
                --i;
            }

            return length - i;
        }

        private static bool IsStartOfSomeFastBlock(int dataOffset, int[] index, int fastILimit)
        {
            for (var i = 0; i < fastILimit; i += SMALL_DATA_BLOCKS_PER_BMP_BLOCK)
            {
                if (index[i] == dataOffset)
                {
                    return true;
                }
            }

            return false;
        }

        /**
         * Finds the start of the last range in the trie by enumerating backward.
         * Indexes for code points higher than this will be omitted.
         */
        private int FindHighStart()
        {
            var i = highStart >> CodePointTrie.SHIFT_3;
            while (i > 0)
            {
                bool match;
                if (flags[--i] == ALL_SAME)
                {
                    match = index[i] == highValue;
                }
                else /* MIXED */
                {
                    var p = index[i];
                    for (var j = 0;; ++j)
                    {
                        if (j == CodePointTrie.SMALL_DATA_BLOCK_LENGTH)
                        {
                            match = true;
                            break;
                        }

                        if (data[p + j] != highValue)
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (!match)
                {
                    return (i + 1) << CodePointTrie.SHIFT_3;
                }
            }

            return 0;
        }

        private class AllSameBlocks
        {
            private const  int NewUnique = -1;
            internal const int Overflow  = -2;

            internal AllSameBlocks()
            {
                mostRecent = -1;
            }

            internal int FindOrAdd(int index, int count, int value)
            {
                if (mostRecent >= 0 && values[mostRecent] == value)
                {
                    refCounts[mostRecent] += count;
                    return indexes[mostRecent];
                }

                for (var i = 0; i < length; ++i)
                {
                    if (values[i] == value)
                    {
                        mostRecent = i;
                        refCounts[i] += count;
                        return indexes[i];
                    }
                }

                if (length == Capacity)
                {
                    return Overflow;
                }

                mostRecent = length;
                indexes[length] = index;
                values[length] = value;
                refCounts[length++] = count;
                return NewUnique;
            }

            /** Replaces the block which has the lowest reference count. */
            internal void Add(int index, int count, int value)
            {
                Debug.Assert(length == Capacity);
                var least = -1;
                var leastCount = I_LIMIT;
                for (var i = 0; i < length; ++i)
                {
                    Debug.Assert(values[i] != value);
                    if (refCounts[i] < leastCount)
                    {
                        least = i;
                        leastCount = refCounts[i];
                    }
                }

                Debug.Assert(least >= 0);
                mostRecent = least;
                indexes[least] = index;
                values[least] = value;
                refCounts[least] = count;
            }

            internal int FindMostUsed()
            {
                if (length == 0)
                {
                    return -1;
                }

                var max = -1;
                var maxCount = 0;
                for (var i = 0; i < length; ++i)
                {
                    if (refCounts[i] > maxCount)
                    {
                        max = i;
                        maxCount = refCounts[i];
                    }
                }

                return indexes[max];
            }

            private const int Capacity = 32;

            private int length;
            private int mostRecent;

            private readonly int[] indexes   = new int[Capacity];
            private readonly int[] values    = new int[Capacity];
            private readonly int[] refCounts = new int[Capacity];
        }

        // Custom hash table for mixed-value blocks to be found anywhere in the
        // compacted data or index so far.
        private class MixedBlocks
        {
            internal void Init(int maxLength, int newBlockLength)
            {
                // We store actual data indexes + 1 to reserve 0 for empty entries.
                var maxDataIndex = maxLength - newBlockLength + 1;
                int newLength;
                if (maxDataIndex <= 0xfff)
                {
                    // 4k
                    newLength = 6007;
                    shift = 12;
                    mask = 0xfff;
                }
                else if (maxDataIndex <= 0x7fff)
                {
                    // 32k
                    newLength = 50021;
                    shift = 15;
                    mask = 0x7fff;
                }
                else if (maxDataIndex <= 0x1ffff)
                {
                    // 128k
                    newLength = 200003;
                    shift = 17;
                    mask = 0x1ffff;
                }
                else
                {
                    // maxDataIndex up to around MAX_DATA_LENGTH, ca. 1.1M
                    newLength = 1500007;
                    shift = 21;
                    mask = 0x1fffff;
                }

                if (table == null || newLength > table.Length)
                {
                    table = new int[newLength];
                }
                else
                {
                    Array.Fill(table, 0, 0, newLength);
                }

                length = newLength;

                blockLength = newBlockLength;
            }

            internal void Extend(int[] data, int minStart, int prevDataLength, int newDataLength)
            {
                var start = prevDataLength - blockLength;
                if (start >= minStart)
                {
                    ++start; // Skip the last block that we added last time.
                }
                else
                {
                    start = minStart; // Begin with the first full block.
                }

                for (var end = newDataLength - blockLength; start <= end; ++start)
                {
                    var hashCode = MakeHashCode(data, start);
                    AddEntry(data, null, start, hashCode, start);
                }
            }

            internal void Extend(ushort[] data, int minStart, int prevDataLength, int newDataLength)
            {
                var start = prevDataLength - blockLength;
                if (start >= minStart)
                {
                    ++start; // Skip the last block that we added last time.
                }
                else
                {
                    start = minStart; // Begin with the first full block.
                }

                for (var end = newDataLength - blockLength; start <= end; ++start)
                {
                    var hashCode = MakeHashCode(data, start);
                    AddEntry(null, data, start, hashCode, start);
                }
            }

            internal int FindBlock(int[] data, int[] blockData, int blockStart)
            {
                var hashCode = MakeHashCode(blockData, blockStart);
                var entryIndex = FindEntry(data, null, blockData, null, blockStart, hashCode);
                if (entryIndex >= 0)
                {
                    return (table[entryIndex] & mask) - 1;
                }

                return -1;
            }

            internal int FindBlock(ushort[] data, int[] blockData, int blockStart)
            {
                var hashCode = MakeHashCode(blockData, blockStart);
                var entryIndex = FindEntry(null, data, blockData, null, blockStart, hashCode);
                if (entryIndex >= 0)
                {
                    return (table[entryIndex] & mask) - 1;
                }

                return -1;
            }

            internal int FindBlock(ushort[] data, ushort[] blockData, int blockStart)
            {
                var hashCode = MakeHashCode(blockData, blockStart);
                var entryIndex = FindEntry(null, data, null, blockData, blockStart, hashCode);
                if (entryIndex >= 0)
                {
                    return (table[entryIndex] & mask) - 1;
                }

                return -1;
            }

            internal int FindAllSameBlock(int[] data, int blockValue)
            {
                var hashCode = MakeHashCode(blockValue);
                var entryIndex = FindEntry(data, blockValue, hashCode);
                if (entryIndex >= 0)
                {
                    return (table[entryIndex] & mask) - 1;
                }

                return -1;
            }

            private int MakeHashCode(int[] blockData, int blockStart)
            {
                var blockLimit = blockStart + blockLength;
                var hashCode = blockData[blockStart++];
                do
                {
                    hashCode = 37 * hashCode + blockData[blockStart++];
                } while (blockStart < blockLimit);

                return hashCode;
            }

            private int MakeHashCode(ushort[] blockData, int blockStart)
            {
                var blockLimit = blockStart + blockLength;
                int hashCode = blockData[blockStart++];
                do
                {
                    hashCode = 37 * hashCode + blockData[blockStart++];
                } while (blockStart < blockLimit);

                return hashCode;
            }

            private int MakeHashCode(int blockValue)
            {
                var hashCode = blockValue;
                for (var i = 1; i < blockLength; ++i)
                {
                    hashCode = 37 * hashCode + blockValue;
                }

                return hashCode;
            }

            private void AddEntry(int[] data32, ushort[] data16, int blockStart, int hashCode, int dataIndex)
            {
                Debug.Assert(0 <= dataIndex && dataIndex < mask);
                var entryIndex = FindEntry(data32, data16, data32, data16, blockStart, hashCode);
                if (entryIndex < 0)
                {
                    table[~entryIndex] = (hashCode << shift) | (dataIndex + 1);
                }
            }

            private int FindEntry(int[] data32, ushort[] data16,
                                  int[] blockData32, ushort[] blockData16, int blockStart, int hashCode)
            {
                var shiftedHashCode = hashCode << shift;
                var initialEntryIndex = modulo(hashCode, length - 1) + 1; // 1..length-1
                for (var entryIndex = initialEntryIndex;;)
                {
                    var entry = table[entryIndex];
                    if (entry == 0)
                    {
                        return ~entryIndex;
                    }

                    if ((entry & ~mask) == shiftedHashCode)
                    {
                        var dataIndex = (entry & mask) - 1;
                        if (data32 != null ? EqualBlocks(data32,      dataIndex, blockData32, blockStart, blockLength) :
                            blockData32 != null ? EqualBlocks(data16, dataIndex, blockData32, blockStart, blockLength) :
                            EqualBlocks(data16,                       dataIndex, blockData16, blockStart, blockLength))
                        {
                            return entryIndex;
                        }
                    }

                    entryIndex = NextIndex(initialEntryIndex, entryIndex);
                }
            }

            private int FindEntry(int[] data, int blockValue, int hashCode)
            {
                var shiftedHashCode = hashCode << shift;
                var initialEntryIndex = modulo(hashCode, length - 1) + 1; // 1..length-1
                for (var entryIndex = initialEntryIndex;;)
                {
                    var entry = table[entryIndex];
                    if (entry == 0)
                    {
                        return ~entryIndex;
                    }

                    if ((entry & ~mask) == shiftedHashCode)
                    {
                        var dataIndex = (entry & mask) - 1;
                        if (AllValuesSameAs(data, dataIndex, blockLength, blockValue))
                        {
                            return entryIndex;
                        }
                    }

                    entryIndex = NextIndex(initialEntryIndex, entryIndex);
                }
            }

            private int NextIndex(int initialEntryIndex, int entryIndex)
            {
                // U_ASSERT(0 < initialEntryIndex && initialEntryIndex < length);
                return (entryIndex + initialEntryIndex) % length;
            }

            /** Ensures non-negative n % m (that is 0..m-1). */
            private int modulo(int n, int m)
            {
                var i = n % m;
                if (i < 0)
                {
                    i += m;
                }

                return i;
            }

            // Hash table.
            // The length is a prime number, larger than the maximum data length.
            // The "shift" lower bits store a data index + 1.
            // The remaining upper bits store a partial hashCode of the block data values.
            private int[] table;
            private int   length;
            private int   shift;
            private int   mask;

            private int blockLength;
        }

        private int CompactWholeDataBlocks(int fastILimit, AllSameBlocks allSameBlocks)
        {
            // ASCII data will be stored as a linear table, even if the following code
            // does not yet count it that way.
            var newDataCapacity = ASCII_LIMIT;
            // Add room for a small data null block in case it would match the start of
            // a fast data block where dataNullOffset must not be set in that case.
            newDataCapacity += CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
            // Add room for special values (errorValue, highValue) and padding.
            newDataCapacity += 4;
            var iLimit = highStart >> CodePointTrie.SHIFT_3;
            var blockLength = CodePointTrie.FAST_DATA_BLOCK_LENGTH;
            var inc = SMALL_DATA_BLOCKS_PER_BMP_BLOCK;
            for (var i = 0; i < iLimit; i += inc)
            {
                if (i == fastILimit)
                {
                    blockLength = CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
                    inc = 1;
                }

                var value = index[i];
                if (flags[i] == MIXED)
                {
                    // Really mixed?
                    var p = value;
                    value = data[p];
                    if (AllValuesSameAs(data, p + 1, blockLength - 1, value))
                    {
                        flags[i] = ALL_SAME;
                        index[i] = value;
                        // Fall through to ALL_SAME handling.
                    }
                    else
                    {
                        newDataCapacity += blockLength;
                        continue;
                    }
                }
                else
                {
                    Debug.Assert(flags[i] == ALL_SAME);
                    if (inc > 1)
                    {
                        // Do all of the fast-range data block's ALL_SAME parts have the same value?
                        var allSame = true;
                        var nextI = i + inc;
                        for (var j = i + 1; j < nextI; ++j)
                        {
                            Debug.Assert(flags[j] == ALL_SAME);
                            if (index[j] != value)
                            {
                                allSame = false;
                                break;
                            }
                        }

                        if (!allSame)
                        {
                            // Turn it into a MIXED block.
                            if (GetDataBlock(i) < 0)
                            {
                                return -1;
                            }

                            newDataCapacity += blockLength;
                            continue;
                        }
                    }
                }

                // Is there another ALL_SAME block with the same value?
                var other = allSameBlocks.FindOrAdd(i, inc, value);
                if (other == AllSameBlocks.Overflow)
                {
                    // The fixed-size array overflowed. Slow check for a duplicate block.
                    var jInc = SMALL_DATA_BLOCKS_PER_BMP_BLOCK;
                    for (var j = 0;; j += jInc)
                    {
                        if (j == i)
                        {
                            allSameBlocks.Add(i, inc, value);
                            break;
                        }

                        if (j == fastILimit)
                        {
                            jInc = 1;
                        }

                        if (flags[j] == ALL_SAME && index[j] == value)
                        {
                            allSameBlocks.Add(j, jInc + inc, value);
                            other = j;
                            break;
                            // We could keep counting blocks with the same value
                            // before we add the first one, which may improve compaction in rare cases,
                            // but it would make it slower.
                        }
                    }
                }

                if (other >= 0)
                {
                    flags[i] = SAME_AS;
                    index[i] = other;
                }
                else
                {
                    // New unique same-value block.
                    newDataCapacity += blockLength;
                }
            }

            return newDataCapacity;
        }

        /**
         * Compacts a build-time trie.
         *
         * The compaction
         * - removes blocks that are identical with earlier ones
         * - overlaps each new non-duplicate block as much as possible with the previously-written one
         * - works with fast-range data blocks whose length is a multiple of that of
         *   higher-code-point data blocks
         *
         * It does not try to find an optimal order of writing, deduplicating, and overlapping blocks.
         */
        private int CompactData(int fastILimit, int[] newData, int dataNullIndex, MixedBlocks mixedBlocks)
        {
            // The linear ASCII data has been copied into newData already.
            var newDataLength = 0;
            for (var i = 0;
                newDataLength < ASCII_LIMIT;
                newDataLength += CodePointTrie.FAST_DATA_BLOCK_LENGTH, i += SMALL_DATA_BLOCKS_PER_BMP_BLOCK)
            {
                index[i] = newDataLength;
            }

            var blockLength = CodePointTrie.FAST_DATA_BLOCK_LENGTH;
            mixedBlocks.Init(newData.Length, blockLength);
            mixedBlocks.Extend(newData, 0, 0, newDataLength);

            var iLimit = highStart >> CodePointTrie.SHIFT_3;
            var inc = SMALL_DATA_BLOCKS_PER_BMP_BLOCK;
            var fastLength = 0;
            for (var i = ASCII_I_LIMIT; i < iLimit; i += inc)
            {
                if (i == fastILimit)
                {
                    blockLength = CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
                    inc = 1;
                    fastLength = newDataLength;
                    mixedBlocks.Init(newData.Length, blockLength);
                    mixedBlocks.Extend(newData, 0, 0, newDataLength);
                }

                if (flags[i] == ALL_SAME)
                {
                    var value = index[i];
                    // Find an earlier part of the data array of length blockLength
                    // that is filled with this value.
                    var n = mixedBlocks.FindAllSameBlock(newData, value);
                    // If we find a match, and the current block is the data null block,
                    // and it is not a fast block but matches the start of a fast block,
                    // then we need to continue looking.
                    // This is because this small block is shorter than the fast block,
                    // and not all of the rest of the fast block is filled with this value.
                    // Otherwise trie.getRange() would detect that the fast block starts at
                    // dataNullOffset and assume incorrectly that it is filled with the null value.
                    while (n >= 0 && i == dataNullIndex && i >= fastILimit && n < fastLength &&
                           IsStartOfSomeFastBlock(n, index, fastILimit))
                    {
                        n = FindAllSameBlock(newData, n + 1, newDataLength, value, blockLength);
                    }

                    if (n >= 0)
                    {
                        index[i] = n;
                    }
                    else
                    {
                        n = GetAllSameOverlap(newData, newDataLength, value, blockLength);
                        index[i] = newDataLength - n;
                        var prevDataLength = newDataLength;
                        while (n < blockLength)
                        {
                            newData[newDataLength++] = value;
                            ++n;
                        }

                        mixedBlocks.Extend(newData, 0, prevDataLength, newDataLength);
                    }
                }
                else if (flags[i] == MIXED)
                {
                    var block = index[i];
                    var n = mixedBlocks.FindBlock(newData, data, block);
                    if (n >= 0)
                    {
                        index[i] = n;
                    }
                    else
                    {
                        n = GetOverlap(newData, newDataLength, data, block, blockLength);
                        index[i] = newDataLength - n;
                        var prevDataLength = newDataLength;
                        while (n < blockLength)
                        {
                            newData[newDataLength++] = data[block + n++];
                        }

                        mixedBlocks.Extend(newData, 0, prevDataLength, newDataLength);
                    }
                }
                else /* SAME_AS */
                {
                    var j = index[i];
                    index[i] = index[j];
                }
            }

            return newDataLength;
        }

        private int CompactIndex(int fastILimit, MixedBlocks mixedBlocks)
        {
            var fastIndexLength = fastILimit >> (CodePointTrie.FAST_SHIFT - CodePointTrie.SHIFT_3);
            if ((highStart >> CodePointTrie.FAST_SHIFT) <= fastIndexLength)
            {
                // Only the linear fast index, no multi-stage index tables.
                index3NullOffset = CodePointTrie.NO_INDEX_3_NULL_OFFSET;
                return fastIndexLength;
            }

            // Condense the fast index table.
            // Also, does it contain an index-3 block with all dataNullOffset?
            var fastIndex = new ushort[fastIndexLength];
            var i3FirstNull = -1;
            for (int i = 0, j = 0; i < fastILimit; ++j)
            {
                var i3 = index[i];
                fastIndex[j] = (ushort) i3;
                if (i3 == dataNullOffset)
                {
                    if (i3FirstNull < 0)
                    {
                        i3FirstNull = j;
                    }
                    else if (index3NullOffset < 0 &&
                             (j - i3FirstNull + 1) == CodePointTrie.INDEX_3_BLOCK_LENGTH)
                    {
                        index3NullOffset = i3FirstNull;
                    }
                }
                else
                {
                    i3FirstNull = -1;
                }

                // Set the index entries that compactData() skipped.
                // Needed when the multi-stage index covers the fast index range as well.
                var iNext = i + SMALL_DATA_BLOCKS_PER_BMP_BLOCK;
                while (++i < iNext)
                {
                    i3 += CodePointTrie.SMALL_DATA_BLOCK_LENGTH;
                    index[i] = i3;
                }
            }

            mixedBlocks.Init(fastIndexLength, CodePointTrie.INDEX_3_BLOCK_LENGTH);
            mixedBlocks.Extend(fastIndex, 0, 0, fastIndexLength);

            // Examine index-3 blocks. For each determine one of:
            // - same as the index-3 null block
            // - same as a fast-index block
            // - 16-bit indexes
            // - 18-bit indexes
            // We store this in the first flags entry for the index-3 block.
            //
            // Also determine an upper limit for the index-3 table length.
            var index3Capacity = 0;
            i3FirstNull = index3NullOffset;
            var hasLongI3Blocks = false;
            // If the fast index covers the whole BMP, then
            // the multi-stage index is only for supplementary code points.
            // Otherwise, the multi-stage index covers all of Unicode.
            var iStart = fastILimit < BMP_I_LIMIT ? 0 : BMP_I_LIMIT;
            var iLimit = highStart >> CodePointTrie.SHIFT_3;
            for (var i = iStart; i < iLimit;)
            {
                var j = i;
                var jLimit = i + CodePointTrie.INDEX_3_BLOCK_LENGTH;
                var oredI3 = 0;
                var isNull = true;
                do
                {
                    var i3 = index[j];
                    oredI3 |= i3;
                    if (i3 != dataNullOffset)
                    {
                        isNull = false;
                    }
                } while (++j < jLimit);

                if (isNull)
                {
                    flags[i] = I3_NULL;
                    if (i3FirstNull < 0)
                    {
                        if (oredI3 <= 0xFFFF)
                        {
                            index3Capacity += CodePointTrie.INDEX_3_BLOCK_LENGTH;
                        }
                        else
                        {
                            index3Capacity += INDEX_3_18BIT_BLOCK_LENGTH;
                            hasLongI3Blocks = true;
                        }

                        i3FirstNull = 0;
                    }
                }
                else
                {
                    if (oredI3 <= 0xFFFF)
                    {
                        var n = mixedBlocks.FindBlock(fastIndex, index, i);
                        if (n >= 0)
                        {
                            flags[i] = I3_BMP;
                            index[i] = n;
                        }
                        else
                        {
                            flags[i] = I3_16;
                            index3Capacity += CodePointTrie.INDEX_3_BLOCK_LENGTH;
                        }
                    }
                    else
                    {
                        flags[i] = I3_18;
                        index3Capacity += INDEX_3_18BIT_BLOCK_LENGTH;
                        hasLongI3Blocks = true;
                    }
                }

                i = j;
            }

            var index2Capacity = (iLimit - iStart) >> CodePointTrie.SHIFT_2_3;

            // Length of the index-1 table, rounded up.
            var index1Length = (index2Capacity + CodePointTrie.INDEX_2_MASK) >> CodePointTrie.SHIFT_1_2;

            // Index table: Fast index, index-1, index-3, index-2.
            // +1 for possible index table padding.
            var index16Capacity = fastIndexLength + index1Length + index3Capacity + index2Capacity + 1;
            index16 = new ushort[index16Capacity];
            Array.Copy(fastIndex, index16, Math.Min(fastIndex.Length, index16Capacity));

            mixedBlocks.Init(index16Capacity, CodePointTrie.INDEX_3_BLOCK_LENGTH);
            MixedBlocks longI3Blocks = null;
            if (hasLongI3Blocks)
            {
                longI3Blocks = new MixedBlocks();
                longI3Blocks.Init(index16Capacity, INDEX_3_18BIT_BLOCK_LENGTH);
            }

            // Compact the index-3 table and write an uncompacted version of the index-2 table.
            var index2 = new ushort[index2Capacity];
            var i2Length = 0;
            i3FirstNull = index3NullOffset;
            var index3Start = fastIndexLength + index1Length;
            var indexLength = index3Start;
            for (var i = iStart; i < iLimit; i += CodePointTrie.INDEX_3_BLOCK_LENGTH)
            {
                int i3;
                var f = flags[i];
                if (f == I3_NULL && i3FirstNull < 0)
                {
                    // First index-3 null block. Write & overlap it like a normal block, then remember it.
                    f = dataNullOffset <= 0xFFFF ? I3_16 : I3_18;
                    i3FirstNull = 0;
                }

                if (f == I3_NULL)
                {
                    i3 = index3NullOffset;
                }
                else if (f == I3_BMP)
                {
                    i3 = index[i];
                }
                else if (f == I3_16)
                {
                    var n = mixedBlocks.FindBlock(index16, index, i);
                    if (n >= 0)
                    {
                        i3 = n;
                    }
                    else
                    {
                        if (indexLength == index3Start)
                        {
                            // No overlap at the boundary between the index-1 and index-3 tables.
                            n = 0;
                        }
                        else
                        {
                            n = GetOverlap(index16, indexLength,
                                index,              i, CodePointTrie.INDEX_3_BLOCK_LENGTH);
                        }

                        i3 = indexLength - n;
                        var prevIndexLength = indexLength;
                        while (n < CodePointTrie.INDEX_3_BLOCK_LENGTH)
                        {
                            index16[indexLength++] = (ushort) index[i + n++];
                        }

                        mixedBlocks.Extend(index16, index3Start, prevIndexLength, indexLength);
                        if (hasLongI3Blocks)
                        {
                            longI3Blocks.Extend(index16, index3Start, prevIndexLength, indexLength);
                        }
                    }
                }
                else
                {
                    Debug.Assert(f == I3_18);
                    Debug.Assert(hasLongI3Blocks);
                    // Encode an index-3 block that contains one or more data indexes exceeding 16 bits.
                    var j = i;
                    var jLimit = i + CodePointTrie.INDEX_3_BLOCK_LENGTH;
                    var k = indexLength;
                    do
                    {
                        ++k;
                        var v = index[j++];
                        var upperBits = (v & 0x30000) >> 2;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 4;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 6;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 8;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 10;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 12;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 14;
                        index16[k++] = (ushort) v;
                        v = index[j++];
                        upperBits |= (v & 0x30000) >> 16;
                        index16[k++] = (ushort) v;
                        index16[k - 9] = (ushort) upperBits;
                    } while (j < jLimit);

                    var n = longI3Blocks.FindBlock(index16, index16, indexLength);
                    if (n >= 0)
                    {
                        i3 = n | 0x8000;
                    }
                    else
                    {
                        if (indexLength == index3Start)
                        {
                            // No overlap at the boundary between the index-1 and index-3 tables.
                            n = 0;
                        }
                        else
                        {
                            n = GetOverlap(index16, indexLength,
                                index16,            indexLength, INDEX_3_18BIT_BLOCK_LENGTH);
                        }

                        i3 = (indexLength - n) | 0x8000;
                        var prevIndexLength = indexLength;
                        if (n > 0)
                        {
                            var start = indexLength;
                            while (n < INDEX_3_18BIT_BLOCK_LENGTH)
                            {
                                index16[indexLength++] = index16[start + n++];
                            }
                        }
                        else
                        {
                            indexLength += INDEX_3_18BIT_BLOCK_LENGTH;
                        }

                        mixedBlocks.Extend(index16, index3Start, prevIndexLength, indexLength);
                        if (hasLongI3Blocks)
                        {
                            longI3Blocks.Extend(index16, index3Start, prevIndexLength, indexLength);
                        }
                    }
                }

                if (index3NullOffset < 0 && i3FirstNull >= 0)
                {
                    index3NullOffset = i3;
                }

                // Set the index-2 table entry.
                index2[i2Length++] = (ushort) i3;
            }

            Debug.Assert(i2Length == index2Capacity);
            Debug.Assert(indexLength <= index3Start + index3Capacity);

            if (index3NullOffset < 0)
            {
                index3NullOffset = CodePointTrie.NO_INDEX_3_NULL_OFFSET;
            }

            if (indexLength >= (CodePointTrie.NO_INDEX_3_NULL_OFFSET + CodePointTrie.INDEX_3_BLOCK_LENGTH))
            {
                // The index-3 offsets exceed 15 bits, or
                // the last one cannot be distinguished from the no-null-block value.
                throw new ArgumentException("The trie data exceeds limitations of the data structure.");
            }

            // Compact the index-2 table and write the index-1 table.
            // assert(CodePointTrie.INDEX_2_BLOCK_LENGTH == CodePointTrie.INDEX_3_BLOCK_LENGTH) :
            //     "must re-init mixedBlocks";
            var blockLength = CodePointTrie.INDEX_2_BLOCK_LENGTH;
            var i1 = fastIndexLength;
            for (var i = 0; i < i2Length; i += blockLength)
            {
                int n;
                if ((i2Length - i) >= blockLength)
                {
                    // normal block
                    Debug.Assert(blockLength == CodePointTrie.INDEX_2_BLOCK_LENGTH);
                    n = mixedBlocks.FindBlock(index16, index2, i);
                }
                else
                {
                    // highStart is inside the last index-2 block. Shorten it.
                    blockLength = i2Length - i;
                    n = FindSameBlock(index16, index3Start, indexLength,
                        index2,                i,           blockLength);
                }

                int i2;
                if (n >= 0)
                {
                    i2 = n;
                }
                else
                {
                    if (indexLength == index3Start)
                    {
                        // No overlap at the boundary between the index-1 and index-3/2 tables.
                        n = 0;
                    }
                    else
                    {
                        n = GetOverlap(index16, indexLength, index2, i, blockLength);
                    }

                    i2 = indexLength - n;
                    var prevIndexLength = indexLength;
                    while (n < blockLength)
                    {
                        index16[indexLength++] = index2[i + n++];
                    }

                    mixedBlocks.Extend(index16, index3Start, prevIndexLength, indexLength);
                }

                // Set the index-1 table entry.
                index16[i1++] = (ushort) i2;
            }

            Debug.Assert(i1 == index3Start);
            Debug.Assert(indexLength <= index16Capacity);

            return indexLength;
        }

        private int CompactTrie(int fastILimit)
        {
            // Find the real highStart and round it up.
            Debug.Assert((highStart & (CodePointTrie.CP_PRE_INDEX_2_ENTRY - 1)) == 0);
            highValue = Get(MAX_UNICODE);
            var realHighStart = FindHighStart();
            realHighStart = (realHighStart + (CodePointTrie.CP_PRE_INDEX_2_ENTRY - 1)) &
                            ~(CodePointTrie.CP_PRE_INDEX_2_ENTRY - 1);
            if (realHighStart == UNICODE_LIMIT)
            {
                highValue = initialValue;
            }

            // We always store indexes and data values for the fast range.
            // Pin highStart to the top of that range while building.
            var fastLimit = fastILimit << CodePointTrie.SHIFT_3;
            if (realHighStart < fastLimit)
            {
                for (var i = (realHighStart >> CodePointTrie.SHIFT_3); i < fastILimit; ++i)
                {
                    flags[i] = ALL_SAME;
                    index[i] = highValue;
                }

                highStart = fastLimit;
            }
            else
            {
                highStart = realHighStart;
            }

            var asciiData = new int[ASCII_LIMIT];
            for (var i = 0; i < ASCII_LIMIT; ++i)
            {
                asciiData[i] = Get(i);
            }

            // First we look for which data blocks have the same value repeated over the whole block,
            // deduplicate such blocks, find a good null data block (for faster enumeration),
            // and get an upper bound for the necessary data array length.
            var allSameBlocks = new AllSameBlocks();
            var newDataCapacity = CompactWholeDataBlocks(fastILimit, allSameBlocks);
            // int[] newData = Arrays.copyOf(asciiData, newDataCapacity);
            var newData = new int[newDataCapacity];
            Array.Copy(asciiData, newData, Math.Min(asciiData.Length, newDataCapacity));

            var dataNullIndex = allSameBlocks.FindMostUsed();

            var mixedBlocks = new MixedBlocks();
            var newDataLength = CompactData(fastILimit, newData, dataNullIndex, mixedBlocks);
            Debug.Assert(newDataLength <= newDataCapacity);
            data = newData;
            dataLength = newDataLength;
            if (dataLength > (0x3ffff + CodePointTrie.SMALL_DATA_BLOCK_LENGTH))
            {
                // The offset of the last data block is too high to be stored in the index table.
                throw new ArgumentException("The trie data exceeds limitations of the data structure.");
            }

            if (dataNullIndex >= 0)
            {
                dataNullOffset = index[dataNullIndex];
                initialValue = data[dataNullOffset];
            }
            else
            {
                dataNullOffset = CodePointTrie.NO_DATA_NULL_OFFSET;
            }

            var indexLength = CompactIndex(fastILimit, mixedBlocks);
            highStart = realHighStart;
            return indexLength;
        }

        private CodePointTrie Build(CodePointTrie.Kind type, CodePointTrie.ValueWidth valueWidth)
        {
            // The mutable trie always stores 32-bit values.
            // When we build a UCPTrie for a smaller value width, we first mask off unused bits
            // before compacting the data.
            switch (valueWidth)
            {
                case CodePointTrie.ValueWidth.Bits32:
                    break;
                case CodePointTrie.ValueWidth.Bits16:
                    MaskValues(0xFFFF);
                    break;
                case CodePointTrie.ValueWidth.Bits8:
                    MaskValues(0xff);
                    break;
                default:
                    // Should be unreachable.
                    throw new ArgumentException();
            }

            var fastLimit = type == CodePointTrie.Kind.Fast ? BMP_LIMIT : CodePointTrie.SMALL_LIMIT;
            var indexLength = CompactTrie(fastLimit >> CodePointTrie.SHIFT_3);

            // Ensure data table alignment: The index length must be even for uint32_t data.
            if (valueWidth == CodePointTrie.ValueWidth.Bits32 && (indexLength & 1) != 0)
            {
                index16[indexLength++] = 0xffee; // arbitrary value
            }

            // Make the total trie structure length a multiple of 4 bytes by padding the data table,
            // and store special values as the last two data values.
            var length = indexLength * 2;
            if (valueWidth == CodePointTrie.ValueWidth.Bits16)
            {
                if (((indexLength ^ dataLength) & 1) != 0)
                {
                    // padding
                    data[dataLength++] = errorValue;
                }

                if (data[dataLength - 1] != errorValue || data[dataLength - 2] != highValue)
                {
                    data[dataLength++] = highValue;
                    data[dataLength++] = errorValue;
                }

                length += dataLength * 2;
            }
            else if (valueWidth == CodePointTrie.ValueWidth.Bits32)
            {
                // 32-bit data words never need padding to a multiple of 4 bytes.
                if (data[dataLength - 1] != errorValue || data[dataLength - 2] != highValue)
                {
                    if (data[dataLength - 1] != highValue)
                    {
                        data[dataLength++] = highValue;
                    }

                    data[dataLength++] = errorValue;
                }

                length += dataLength * 4;
            }
            else
            {
                var and3 = (length + dataLength) & 3;
                if (and3 == 0 && data[dataLength - 1] == errorValue && data[dataLength - 2] == highValue)
                {
                    // all set
                }
                else if (and3 == 3 && data[dataLength - 1] == highValue)
                {
                    data[dataLength++] = errorValue;
                }
                else
                {
                    while (and3 != 2)
                    {
                        data[dataLength++] = highValue;
                        and3 = (and3 + 1) & 3;
                    }

                    data[dataLength++] = highValue;
                    data[dataLength++] = errorValue;
                }

                length += dataLength;
            }

            Debug.Assert((length & 3) == 0);

            // Fill the index and data arrays.
            ushort[] trieIndex;
            if (highStart <= fastLimit)
            {
                // Condense only the fast index from the mutable-trie index.
                trieIndex = new ushort[indexLength];
                for (int i = 0, j = 0; j < indexLength; i += SMALL_DATA_BLOCKS_PER_BMP_BLOCK, ++j)
                {
                    trieIndex[j] = (ushort) index[i];
                }
            }
            else
            {
                if (indexLength == index16.Length)
                {
                    trieIndex = index16;
                    index16 = null;
                }
                else
                {
                    trieIndex = new ushort[indexLength];
                    Array.Copy(index16, trieIndex, Math.Min(index16.Length, indexLength));
                }
            }

            // Write the data array.
            switch (valueWidth)
            {
                case CodePointTrie.ValueWidth.Bits16:
                {
                    // Write 16-bit data values.
                    var data16 = new ushort[dataLength];
                    for (var i = 0; i < dataLength; ++i)
                    {
                        data16[i] = (ushort) data[i];
                    }

                    return type == CodePointTrie.Kind.Fast
                        ? (CodePointTrie) new CodePointTrie.Fast16(trieIndex, data16, highStart,
                            index3NullOffset, dataNullOffset)
                        : new CodePointTrie.Small16(trieIndex, data16, highStart,
                            index3NullOffset, dataNullOffset);
                }
                case CodePointTrie.ValueWidth.Bits32:
                {
                    // Write 32-bit data values.
                    var data32 = new int[dataLength];
                    Array.Copy(data, data32, Math.Min(data.Length, dataLength));
                    return type == CodePointTrie.Kind.Fast
                        ? (CodePointTrie) new CodePointTrie.Fast32(trieIndex, data32, highStart,
                            index3NullOffset, dataNullOffset)
                        : new CodePointTrie.Small32(trieIndex, data32, highStart,
                            index3NullOffset, dataNullOffset);
                }
                case CodePointTrie.ValueWidth.Bits8:
                {
                    // Write 8-bit data values.
                    var data8 = new byte[dataLength];
                    for (var i = 0; i < dataLength; ++i)
                    {
                        data8[i] = (byte) data[i];
                    }

                    return type == CodePointTrie.Kind.Fast
                        ? (CodePointTrie) new CodePointTrie.Fast8(trieIndex, data8, highStart,
                            index3NullOffset, dataNullOffset)
                        : new CodePointTrie.Small8(trieIndex, data8, highStart,
                            index3NullOffset, dataNullOffset);
                }
                default:
                    // Should be unreachable.
                    throw new ArgumentException();
            }
        }
    }
}