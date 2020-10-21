using System;
using System.Collections;
using System.Collections.Generic;
using CodeHive.unicode_trie.java;
using CodeHive.unicode_trie.util;

namespace CodeHive.unicode_trie
{
    /// <summary>
    /// Abstract map from Unicode code points (U+0000..U+10FFFF) to integer values.
    /// This does not implement <see cref="System.Collections.IDictionary"/>.
    /// </summary>
    public abstract class CodePointMap : IEnumerable<CodePointMap.Range>
    {
        /// <summary>
        /// Selectors for how getRange() should report value ranges overlapping with surrogates.
        /// Most users should use NORMAL.
        ///
        /// <seealso cref="CodePointMap.GetRange(int,Func{int,int},Range)"/>
        /// </summary>
        public enum RangeOption
        {
            /// <summary>
            /// <see cref="CodePointMap.GetRange(int,Func{int,int},Range)"/>
            /// enumerates all same-value ranges as stored in the map. Most users should use this option.
            /// </summary>
            Normal,

            /// <summary>
            /// <see cref="CodePointMap.GetRange(int,Func{int,int},Range)"/>
            /// enumerates all same-value ranges as stored in the map, except that lead surrogates
            /// (U+D800..U+DBFF) are treated as having the surrogateValue, which is passed to getRange()
            /// as a separate parameter. The surrogateValue is not transformed via filter().
            ///
            /// <p/>Most users should use <see cref="Normal"/> instead.
            ///
            /// <p/>This option is useful for maps that map surrogate code *units* to special values
            /// optimized for UTF-16 string processing or for special error behavior for unpaired
            /// surrogates, but those values are not to be associated with the lead surrogate code
            /// *points*.
            ///
            /// <seealso cref="char.IsHighSurrogate(char)"/>
            /// </summary>
            FixedLeadSurrogates,

            /// <summary>
            /// <see cref="CodePointMap.GetRange(int,Func{int,int},Range)"/>
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
            /// <seealso cref="char.IsSurrogate(char)"/>
            /// </summary>
            FixedAllSurrogates,
        }

        /// <summary>
        /// Range iteration result data.
        /// Code points from start to end map to the same value.
        /// The value may have been modified by a filter,
        /// or it may be the surrogateValue if a RangeOption other than "normal" was used.
        ///
        /// <seealso cref="CodePointMap.GetRange(int,Func{int,int},Range)"/>
        /// <seealso cref="CodePointMap.GetEnumerator()"/>
        /// </summary>
        public class Range
        {
            /// <summary>
            /// Constructor. Sets start and end to -1 and value to 0.
            /// </summary>
            public Range()
            {
                Start = End = -1;
                Value = 0;
            }

            /// <returns>the start code point</returns>
            public int Start { get; internal set; }

            /// <returns>the (inclusive) end code point</returns>
            public int End { get; internal set; }

            /// <returns>the range value</returns>
            public int Value { get; internal set; }

            /// <summary>
            /// Sets the range. When using <see cref="CodePointMap.GetEnumerator()"/>,
            /// iteration will resume after the newly set end.
            /// </summary>
            /// <param name="start">new start code point</param>
            /// <param name="end">new end code point</param>
            /// <param name="value">new value</param>
            internal void Set(int start, int end, int value)
            {
                Start = start;
                End = end;
                Value = value;
            }
        }

        /// <summary>
        ///  Iterates over code points of a string and fetches map values.
        /// This does not implement <see cref="IEnumerable{T}"/>.
        /// <code>
        /// void onString(CodePointMap map, CharSequence s, int start) {
        ///     CodePointMap.StringIterator iter = map.GetStringIterator(s, start);
        ///     while (iter.next()) {
        ///         int end = iter.getIndex();  // code point from between start and end
        ///         useValue(s, start, end, iter.getCodePoint(), iter.getValue());
        ///         start = end;
        ///     }
        /// }
        /// </code>
        /// <p/>This class is not intended for public subclassing.
        /// </summary>
        public class StringIterator
        {
            private readonly CodePointMap codePointMap;

            private ICharSequence sequence;

            internal StringIterator(CodePointMap codePointMap, string str, int index)
            {
                this.codePointMap = codePointMap;
                Reset(str, index);
            }

            /// <summary>
            /// Resets the iterator to a new string and/or a new string index.
            /// </summary>
            /// <param name="str">string to iterate over</param>
            /// <param name="index">string index where the iteration will start</param>
            public void Reset(string str, int index)
            {
                sequence = new StringCharSequence(str);
                Index = index;
                CodePoint = -1;
                Value = 0;
            }

            /// <summary>
            /// Reads the next code point, post-increments the string index,
            /// and gets a value from the map.
            /// Sets an implementation-defined error value if the code point is an unpaired surrogate.
            /// </summary>
            /// <returns>true if the string index was not yet at the end of the string;
            ///          otherwise the iterator did not advance</returns>
            public virtual bool Next()
            {
                if (Index >= sequence.Length)
                {
                    return false;
                }

                CodePoint = Character.CodePointAt(sequence, Index);
                Index += Character.CharCount(CodePoint);
                Value = codePointMap.Get(CodePoint);
                return true;
            }

            /// <summary>
            /// Reads the previous code point, pre-decrements the string index,
            /// and gets a value from the map.
            /// Sets an implementation-defined error value if the code point is an unpaired surrogate.
            /// </summary>
            /// <returns>true if the string index was not yet at the start of the string;
            ///          otherwise the iterator did not advance</returns>
            public virtual bool Previous()
            {
                if (Index <= 0)
                {
                    return false;
                }

                CodePoint = Character.CodePointBefore(sequence, Index);
                Index -= Character.CharCount(CodePoint);
                Value = codePointMap.Get(CodePoint);
                return true;
            }

            /// <summary>
            /// The string index.
            /// </summary>
            public int Index { get; internal set; }

            /// <summary>
            /// The code point.
            /// </summary>
            public int CodePoint { get; internal set; }

            /// <summary>
            /// The map value, or an implementation-defined error value if the
            /// code point is an unpaired surrogate.
            /// </summary>
            public int Value { get; internal set; }

            internal int Length => sequence.Length;

            internal char CharAt(in int index) => sequence.CharAt(index);
        }

        /// <summary>
        /// Protected no-args constructor.
        /// </summary>
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
        public abstract int Get(int c);

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
        /// <p/>If the <see cref="Func{TResult,TValue}"/> parameter is not null, then
        /// the value to be delivered is passed through that filter, and the return value is the end
        /// of the range where all values are modified to the same actual value.
        /// The value is unchanged if that parameter is null.
        ///
        /// <p/>Example:
        /// <code>
        /// int start = 0;
        /// Range range = new CodePointMap.Range();
        /// while (map.getRange(start, null, range)) {
        ///     int end = range.getEnd();
        ///     int value = range.getValue();
        ///     // Work with the range start..end and its value.
        ///     start = end + 1;
        /// }
        /// </code>
        /// </summary>
        /// <param name="start">range start</param>
        /// <param name="filter">a func that may modify the map data value,
        ///      or null if the values from the map are to be used unmodified</param>
        /// <param name="range">the range object that will be set to the code point range and value</param>
        /// <returns>true if start is 0..U+10FFFF; otherwise no new range is fetched</returns>
        /// <remarks>
        /// The filter parameter modifies a map value.
        /// <p/>Can be used to ignore some of the value bits,
        /// make a filter for one of several values,
        /// return a value index computed from the map value, etc.
        /// </remarks>
        public abstract bool GetRange(int start, Func<int, int> filter, Range range);

        /// <summary>
        /// Sets the range object to a range of code points beginning with the start parameter.
        /// The range start is the same as the start input parameter
        /// (even if there are preceding code points that have the same value).
        /// The range end is the last code point such that
        /// all those from start to there have the same value.
        /// Returns false if start is not 0..U+10FFFF.
        ///
        /// <p/>Same as the simpler <see cref="GetRange(int,Func{int,int},Range)"/> but optionally
        /// modifies the range if it overlaps with surrogate code points.
        /// </summary>
        /// <param name="start">range start</param>
        /// <param name="option">defines whether surrogates are treated normally,
        ///               or as having the surrogateValue; usually <see cref="RangeOption.Normal"/></param>
        /// <param name="surrogateValue">value for surrogates; ignored if option==<see cref="RangeOption.Normal"/></param>
        /// <param name="filter">a func that may modify the map data value,
        ///     or null if the values from the map are to be used unmodified</param>
        /// <param name="range">the range object that will be set to the code point range and value</param>
        /// <returns>true if start is 0..U+10FFFF; otherwise no new range is fetched</returns>
        /// <remarks>
        /// The filter parameter modifies a map value.
        /// <p/>Can be used to ignore some of the value bits,
        /// make a filter for one of several values,
        /// return a value index computed from the map value, etc.
        /// </remarks>
        public bool GetRange(int start, RangeOption option, int surrogateValue, Func<int, int> filter, Range range)
        {
            if (!GetRange(start, filter, range))
            {
                return false;
            }

            if (option == RangeOption.Normal)
            {
                return true;
            }

            var surrEnd = option == RangeOption.FixedAllSurrogates ? 0xdfff : 0xdbff;

            var end = range.End;
            if (end < 0xd7ff || start > surrEnd)
            {
                return true;
            }

            // The range overlaps with surrogates, or ends just before the first one.
            if (range.Value == surrogateValue)
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
                    range.End = 0xd7ff; // Non-surrValue range ends before surrValue surrogates.
                    return true;
                }

                // Start is a surrogate with a non-surrValue code *unit* value.
                // Return a surrValue code *point* range.
                range.Value = surrogateValue;
                if (end > surrEnd)
                {
                    range.End = surrEnd; // Surrogate range ends before non-surrValue rest of range.
                    return true;
                }
            }

            // See if the surrValue surrogate range can be merged with
            // an immediately following range.
            if (GetRange(surrEnd + 1, filter, range) && range.Value == surrogateValue)
            {
                range.Start = start;
                return true;
            }

            range.Set(start, surrEnd, surrogateValue);
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
        /// Same as looping over all ranges with <see cref="GetRange(int,Func{int,int},Range)"/>
        /// without filtering.
        /// Adjacent ranges have different map values.
        ///
        /// <p/>The enumerator always returns te same range object.
        /// </summary>
        /// <returns>An enumerator over <see cref="Range"/></returns>
        public IEnumerator<Range> GetEnumerator()
        {
            var range = new Range();

            while (-1 <= range.End && range.End < 0x10ffff && GetRange(range.End + 1, null, range))
            {
                yield return range;
            }
        }

        /// <summary>
        /// Returns an iterator (not a <see cref="IEnumerable{T}"/>) over code points of a string
        /// for fetching map values.
        /// </summary>
        /// <param name="str">string to iterate over</param>
        /// <param name="index">string index where the iteration will start</param>
        /// <returns>the iterator</returns>
        public virtual StringIterator GetStringIterator(string str, int index)
        {
            return new StringIterator(this, str, index);
        }
    }
}