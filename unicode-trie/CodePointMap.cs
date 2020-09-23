using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CodeHive.unicode_trie.java;

#pragma warning disable 612

// ReSharper disable InconsistentNaming
namespace CodeHive.unicode_trie
{
    /// <summary>
    /// Abstract map from Unicode code points (U+0000..U+10FFFF) to integer values.
    /// This does not implement <see cref="System.Collections.IDictionary"/>.
    /// </summary>
    /// <p/> @stable ICU 63
    public abstract class CodePointMap : IEnumerable<CodePointMap.Range>
    {
        /// <summary>
        /// Selectors for how getRange() should report value ranges overlapping with surrogates.
        /// Most users should use NORMAL.
        ///
        /// <seealso cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
        /// </summary>
        /// <p/> @stable ICU 63
        public enum RangeOption
        {
            /// <summary>
            /// <see cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
            /// enumerates all same-value ranges as stored in the map. Most users should use this option.
            /// </summary>
            /// <p/> @stable ICU 63
            NORMAL,

            /// <summary>
            /// <see cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
            /// enumerates all same-value ranges as stored in the map, except that lead surrogates
            /// (U+D800..U+DBFF) are treated as having the surrogateValue, which is passed to getRange()
            /// as a separate parameter. The surrogateValue is not transformed via filter().
            ///
            /// <p/>Most users should use <see cref="NORMAL"/> instead.
            ///
            /// <p/>This option is useful for maps that map surrogate code *units* to special values
            /// optimized for UTF-16 string processing or for special error behavior for unpaired
            /// surrogates, but those values are not to be associated with the lead surrogate code
            /// *points*.
            ///
            /// <seealso cref="Character.isHighSurrogate"/>
            /// </summary>
            /// <p/> @stable ICU 63
            FIXED_LEAD_SURROGATES,

            /// <summary>
            /// <see cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
            /// enumerates all same-value ranges as stored in the map, except that all surrogates
            /// (U+D800..U+DFFF) are treated as having the surrogateValue, which is passed to getRange()
            /// as a separate parameter. The surrogateValue is not transformed via filter().
            ///
            /// <p/>Most users should use NORMAL instead.
            ///
            /// <p/>This option is useful for maps that map surrogate code *units* to special values
            /// optimized for UTF-16 string processing or for special error behavior for unpaired
            /// surrogates, but those values are not to be associated with the lead surrogate code
            /// *points*.
            ///
            /// <seealso cref="Character.isSurrogate"/>
            /// </summary>
            /// <p/> @stable ICU 63
            FIXED_ALL_SURROGATES,
        }

        /// <summary>
        /// Callback function interface: Modifies a map value.
        /// Optionally called by getRange().
        /// The modified value will be returned by the getRange() function.
        ///
        /// <p/>Can be used to ignore some of the value bits,
        /// make a filter for one of several values,
        /// return a value index computed from the map value, etc.
        ///
        /// <seealso cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
        /// <seealso cref="CodePointMap.GetEnumerator()"/>
        /// </summary>
        /// <p/> @stable ICU 63
        public interface ValueFilter
        {
            /// <summary>
            /// Modifies the map value.
            /// </summary>
            /// <param name="value">map value</param>
            /// <returns>modified value</returns>
            /// <p/> @stable ICU 63
            public int apply(int value);
        }

        /// <summary>
        /// Range iteration result data.
        /// Code points from start to end map to the same value.
        /// The value may have been modified by <see cref="ValueFilter.apply"/>,
        /// or it may be the surrogateValue if a RangeOption other than "normal" was used.
        ///
        /// <seealso cref="CodePointMap.getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
        /// <seealso cref="CodePointMap.GetEnumerator()"/>
        /// </summary>
        /// <p/> @stable ICU 63
        public class Range
        {
            internal int start;
            internal int end;
            internal int value;

            /// <summary>
            /// Constructor. Sets start and end to -1 and value to 0.
            /// </summary>
            /// <p/> @stable ICU 63
            public Range()
            {
                start = end = -1;
                value = 0;
            }

            /// <returns>the start code point</returns>
            /// <p/> @stable ICU 63
            public int getStart()
            {
                return start;
            }

            /// <returns>the (inclusive) end code point</returns>
            /// <p/> @stable ICU 63
            public int getEnd()
            {
                return end;
            }

            /// <returns>the range value</returns>
            /// <p/> @stable ICU 63
            public int getValue()
            {
                return value;
            }

            /// <summary>
            /// Sets the range. When using <see cref="CodePointMap.GetEnumerator()"/>,
            /// iteration will resume after the newly set end.
            /// </summary>
            /// <param name="start">new start code point</param>
            /// <param name="end">new end code point</param>
            /// <param name="value">new value</param>
            /// <p/> @stable ICU 63
            [SuppressMessage("ReSharper", "ParameterHidesMember")]
            public void set(int start, int end, int value)
            {
                this.start = start;
                this.end = end;
                this.value = value;
            }
        }

        /// <summary>
        ///  Iterates over code points of a string and fetches map values.
        /// This does not implement <see cref="IEnumerable{T}"/>.
        /// <code>
        /// void onString(CodePointMap map, CharSequence s, int start) {
        ///     CodePointMap.StringIterator iter = map.stringIterator(s, start);
        ///     while (iter.next()) {
        ///         int end = iter.getIndex();  // code point from between start and end
        ///         useValue(s, start, end, iter.getCodePoint(), iter.getValue());
        ///         start = end;
        ///     }
        /// }
        /// </code>
        /// <p/>This class is not intended for public subclassing.
        /// </summary>
        /// <p/> @stable ICU 63
        public class StringIterator
        {
            private readonly CodePointMap codePointMap;

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete] protected CharSequence s;

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete] protected int sIndex;

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete] protected int c;

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete] protected int value;

            /// <remarks>@internal This API is ICU internal only.</remarks>
            [Obsolete]
            protected internal StringIterator(CodePointMap codePointMap, CharSequence s, int sIndex)
            {
                this.codePointMap = codePointMap;
                this.s = s;
                this.sIndex = sIndex;
                c = -1;
                value = 0;
            }

            /// <summary>
            /// Resets the iterator to a new string and/or a new string index.
            /// </summary>
            /// <param name="s">string to iterate over</param>
            /// <param name="sIndex">string index where the iteration will start</param>
            /// <p/> @stable ICU 63
            [SuppressMessage("ReSharper", "ParameterHidesMember")]
            public void reset(CharSequence s, int sIndex)
            {
                this.s = s;
                this.sIndex = sIndex;
                c = -1;
                value = 0;
            }

            /// <summary>
            /// Reads the next code point, post-increments the string index,
            /// and gets a value from the map.
            /// Sets an implementation-defined error value if the code point is an unpaired surrogate.
            /// </summary>
            /// <returns>true if the string index was not yet at the end of the string;
            ///          otherwise the iterator did not advance</returns>
            /// <p/> @stable ICU 63
            public virtual bool next()
            {
                if (sIndex >= s.length())
                {
                    return false;
                }

                c = Character.codePointAt(s, sIndex);
                sIndex += Character.charCount(c);
                value = codePointMap.get(c);
                return true;
            }

            /// <summary>
            /// Reads the previous code point, pre-decrements the string index,
            /// and gets a value from the map.
            /// Sets an implementation-defined error value if the code point is an unpaired surrogate.
            /// </summary>
            /// <returns>true if the string index was not yet at the start of the string;
            ///          otherwise the iterator did not advance</returns>
            /// <p/> @stable ICU 63
            public virtual bool previous()
            {
                if (sIndex <= 0)
                {
                    return false;
                }

                c = Character.codePointBefore(s, sIndex);
                sIndex -= Character.charCount(c);
                value = codePointMap.get(c);
                return true;
            }

            /// <returns>the string index</returns>
            /// <p/> @stable ICU 63
            public int getIndex()
            {
                return sIndex;
            }

            /// <returns>the code point</returns>
            /// <p/> @stable ICU 63
            public int getCodePoint()
            {
                return c;
            }

            /// <returns>the map value,
            ///          or an implementation-defined error value if
            ///          the code point is an unpaired surrogate</returns>
            /// <p/> @stable ICU 63
            public int getValue()
            {
                return value;
            }
        }

        /// <summary>
        /// Protected no-args constructor.
        /// </summary>
        /// <p/> @stable ICU 63
        // ReSharper disable once EmptyConstructor
        protected CodePointMap()
        { }

        /// <summary>
        /// Returns the value for a code point as stored in the map, with range checking.
        /// Returns an implementation-defined error value if c is not in the range 0..U+10FFFF.
        /// </summary>
        /// <param name="c">the code point</param>
        /// <returns>the map value,
        ///          or an implementation-defined error value if
        ///          the code point is not in the range 0..U+10FFFF</returns>
        /// <p/> @stable ICU 63
        public abstract int get(int c);

        /// <summary>
        /// Sets the range object to a range of code points beginning with the start parameter.
        /// The range start is the same as the start input parameter
        /// (even if there are preceding code points that have the same value).
        /// The range end is the last code point such that
        /// all those from start to there have the same value.
        /// Returns false if start is not 0..U+10FFFF.
        /// Can be used to efficiently iterate over all same-value ranges in a map.
        /// (This is normally faster than iterating over code points and get()ting each value,
        /// but may be much slower than a data structure that stores ranges directly.)
        ///
        /// <p/>If the <see cref="ValueFilter"/> parameter is not null, then
        /// the value to be delivered is passed through that filter, and the return value is the end
        /// of the range where all values are modified to the same actual value.
        /// The value is unchanged if that parameter is null.
        ///
        /// <p/>Example:
        /// <code>
        /// int start = 0;
        /// CodePointMap.Range range = new CodePointMap.Range();
        /// while (map.getRange(start, null, range)) {
        ///     int end = range.getEnd();
        ///     int value = range.getValue();
        ///     // Work with the range start..end and its value.
        ///     start = end + 1;
        /// }
        /// </code>
        /// </summary>
        /// <param name="start">range start</param>
        /// <param name="filter">an object that may modify the map data value,
        ///      or null if the values from the map are to be used unmodified</param>
        /// <param name="range">the range object that will be set to the code point range and value</param>
        /// <returns>true if start is 0..U+10FFFF; otherwise no new range is fetched</returns>
        /// <p/> @stable ICU 63
        public abstract bool getRange(int start, ValueFilter filter, Range range);

        /// <summary>
        /// Sets the range object to a range of code points beginning with the start parameter.
        /// The range start is the same as the start input parameter
        /// (even if there are preceding code points that have the same value).
        /// The range end is the last code point such that
        /// all those from start to there have the same value.
        /// Returns false if start is not 0..U+10FFFF.
        ///
        /// <p/>Same as the simpler {@link #getRange(int, ValueFilter, Range)} but optionally
        /// modifies the range if it overlaps with surrogate code points.
        /// </summary>
        /// <param name="start">range start</param>
        /// <param name="option">defines whether surrogates are treated normally,
        ///               or as having the surrogateValue; usually <see cref="RangeOption.NORMAL"/></param>
        /// <param name="surrogateValue">value for surrogates; ignored if option==<see cref="RangeOption.NORMAL"/></param>
        /// <param name="filter">an object that may modify the map data value,
        ///     or null if the values from the map are to be used unmodified</param>
        /// <param name="range">the range object that will be set to the code point range and value</param>
        /// <returns>true if start is 0..U+10FFFF; otherwise no new range is fetched</returns>
        /// <p/> @stable ICU 63
        public bool getRange(int start, RangeOption option, int surrogateValue,
                             ValueFilter filter, Range range)
        {
            if (!getRange(start, filter, range))
            {
                return false;
            }

            if (option == RangeOption.NORMAL)
            {
                return true;
            }

            int surrEnd = option == RangeOption.FIXED_ALL_SURROGATES ? 0xdfff : 0xdbff;

            int end = range.end;
            if (end < 0xd7ff || start > surrEnd)
            {
                return true;
            }

            // The range overlaps with surrogates, or ends just before the first one.
            if (range.value == surrogateValue)
            {
                if (end >= surrEnd)
                {
                    // Surrogates followed by a non-surrValue range,
                    // or surrogates are part of a larger surrValue range.
                    return true;
                }
            }
            else
            {
                if (start <= 0xd7ff)
                {
                    range.end = 0xd7ff; // Non-surrValue range ends before surrValue surrogates.
                    return true;
                }

                // Start is a surrogate with a non-surrValue code *unit* value.
                // Return a surrValue code *point* range.
                range.value = surrogateValue;
                if (end > surrEnd)
                {
                    range.end = surrEnd; // Surrogate range ends before non-surrValue rest of range.
                    return true;
                }
            }

            // See if the surrValue surrogate range can be merged with
            // an immediately following range.
            if (getRange(surrEnd + 1, filter, range) && range.value == surrogateValue)
            {
                range.start = start;
                return true;
            }

            range.start = start;
            range.end = surrEnd;
            range.value = surrogateValue;
            return true;
        }

        /// <summary>
        /// Enumerator over <see cref="Range"/>.
        /// </summary>
        /// <returns>Enumerator over <see cref="Range"/>.</returns>
        /// <seealso cref="GetEnumerator()"/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Convenience enumerator over same-map-value code point ranges.
        /// Same as looping over all ranges with <see cref="getRange(int,CodePointMap.ValueFilter,CodePointMap.Range)"/>
        /// without filtering.
        /// Adjacent ranges have different map values.
        ///
        /// <p/>The enumerator always returns te same range object.
        /// </summary>
        /// <returns>An enumerator over <see cref="Range"/></returns>
        public IEnumerator<Range> GetEnumerator()
        {
            var range = new Range();

            while (-1 <= range.end && range.end < 0x10ffff && getRange(range.end + 1, null, range))
            {
                yield return range;
            }
        }

        /// <summary>
        /// Returns an iterator (not a <see cref="IEnumerable{T}"/>) over code points of a string
        /// for fetching map values.
        /// </summary>
        /// <param name="s">string to iterate over</param>
        /// <param name="sIndex">string index where the iteration will start</param>
        /// <returns>the iterator</returns>
        /// <p/> @stable ICU 63
        public virtual StringIterator stringIterator(CharSequence s, int sIndex)
        {
            return new StringIterator(this, s, sIndex);
        }

        internal static void assert(bool expression)
        {
            if (!expression)
            {
                throw new AssertionError();
            }
        }
    }
}