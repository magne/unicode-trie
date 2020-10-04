using System;
using System.IO;
using CodeHive.unicode_trie.icu;
using CodeHive.unicode_trie.java;
using CodeHive.unicode_trie.tests.java;
using Xunit;

// ReSharper disable InconsistentNaming
// ReSharper disable RedundantAssignment
// ReSharper disable SuggestBaseTypeForParameter

namespace CodeHive.unicode_trie.tests
{
    public class CodePointTrieTest : TestFmwk
    {
        /* Values for setting possibly overlapping, out-of-order ranges of values */
        private class SetRange
        {
            internal SetRange(int start, int limit, int value)
            {
                this.start = start;
                this.limit = limit;
                this.value = value;
            }

            public override string ToString()
            {
                return Utility.hex(start) + ".." + Utility.hex(limit - 1) + ':' + Utility.hex(value);
            }

            internal readonly int start;
            internal readonly int limit;
            internal readonly int value;
        }

        // Returned from getSpecialValues(). Values extracted from an array of CheckRange.
        private class SpecialValues
        {
            internal SpecialValues(int i, int initialValue, int errorValue)
            {
                this.i = i;
                this.initialValue = initialValue;
                this.errorValue = errorValue;
            }

            internal readonly int i;
            internal readonly int initialValue;
            internal readonly int errorValue;
        }

        /*
         * Values for testing:
         * value is set from the previous boundary's limit to before
         * this boundary's limit
         *
         * There must be an entry with limit 0 and the intialValue.
         * It may be preceded by an entry with negative limit and the errorValue.
         */
        private class CheckRange
        {
            internal CheckRange(int limit, int value)
            {
                this.limit = limit;
                this.value = value;
            }

            public override string ToString()
            {
                return "≤" + Utility.hex(limit - 1) + ':' + Utility.hex(value);
            }

            internal readonly int limit;
            internal readonly int value;
        }

        private static int SkipSpecialValues(CheckRange[] checkRanges)
        {
            int i;
            for (i = 0; i < checkRanges.Length && checkRanges[i].limit <= 0; ++i)
            { }

            return i;
        }

        private static SpecialValues GetSpecialValues(CheckRange[] checkRanges)
        {
            var i = 0;
            int initialValue, errorValue;
            if (i < checkRanges.Length && checkRanges[i].limit < 0)
            {
                errorValue = checkRanges[i++].value;
            }
            else
            {
                errorValue = 0xad;
            }

            if (i < checkRanges.Length && checkRanges[i].limit == 0)
            {
                initialValue = checkRanges[i++].value;
            }
            else
            {
                initialValue = 0;
            }

            return new SpecialValues(i, initialValue, errorValue);
        }

        /* ucptrie_enum() callback, modifies a value */
        private class TestValueFilter : CodePointMap.IValueFilter
        {
            public int Apply(int value)
            {
                return value ^ 0x5555;
            }
        }

        private static readonly TestValueFilter TestFilter = new TestValueFilter();

        private static bool DoCheckRange(string name, string variant,
                                         int start, bool getRangeResult, CodePointMap.Range range,
                                         int expEnd, int expValue)
        {
            if (!getRangeResult)
            {
                if (expEnd >= 0)
                {
                    Fail($"error: {name} getRanges ({variant}) fails to deliver range [U+{start:X4}..U+{expEnd:X4}].0x{expValue:X}\n");
                }

                return false;
            }

            if (expEnd < 0)
            {
                Fail($"error: {name} getRanges ({variant}) delivers unexpected range [U+{range.Start:X4}..U+{range.End:X4}].0x{range.Value:X}\n");
                return false;
            }

            if (range.Start != start || range.End != expEnd || range.Value != expValue)
            {
                Fail($"error: {name} getRanges ({variant}) delivers wrong range [U+{range.Start:X4}..U+{range.End:X4}].0x{range.Value:X} " +
                     $"instead of [U+{start:X4}..U+{expEnd:X4}].0x{expValue:X}\n");
                return false;
            }

            return true;
        }

        // Test iteration starting from various UTF-8/16 and trie structure boundaries.
        // Also test starting partway through lead & trail surrogates for fixed-surrogate-value options,
        // and partway through supplementary code points.
        private static readonly int[] IterStarts =
        {
            0, 0x7f, 0x80, 0x7ff, 0x800, 0xfff, 0x1000,
            0xd7ff, 0xd800, 0xd888, 0xdddd, 0xdfff, 0xe000,
            0xffff, 0x10000, 0x12345, 0x10ffff, 0x110000
        };

        private static void TestTrieGetRanges(string testName, CodePointMap trie,
                                              CodePointMap.RangeOption option, int surrValue,
                                              CheckRange[] checkRanges)
        {
            var typeName = (trie is MutableCodePointTrie) ? "mutableTrie" : "trie";
            var range = new CodePointMap.Range();
            for (var s = 0; s < IterStarts.Length; ++s)
            {
                var start = IterStarts[s];
                int i;
                int expEnd;
                int expValue;
                bool getRangeResult;
                // No need to go from each iteration start to the very end.
                int innerLoopCount;

                var name = $"{typeName}/{option}({testName}) min=U+{start:X4}";

                // Skip over special values and low ranges.
                for (i = 0; i < checkRanges.Length && checkRanges[i].limit <= start; ++i)
                { }

                var i0 = i;
                // without value handler
                for (innerLoopCount = 0;; ++i, start = range.End + 1)
                {
                    if (i < checkRanges.Length)
                    {
                        expEnd = checkRanges[i].limit - 1;
                        expValue = checkRanges[i].value;
                    }
                    else
                    {
                        expEnd = -1;
                        expValue = 0x5005;
                    }

                    getRangeResult = option != CodePointMap.RangeOption.Normal ? trie.GetRange(start, option, surrValue, null, range) : trie.GetRange(start, null, range);
                    if (!DoCheckRange(name, "without value handler",
                        start,              getRangeResult, range, expEnd, expValue))
                    {
                        break;
                    }

                    if (s != 0 && ++innerLoopCount == 5)
                    {
                        break;
                    }
                }

                // with value handler
                for (i = i0, start = IterStarts[s], innerLoopCount = 0;;
                    ++i, start = range.End + 1)
                {
                    if (i < checkRanges.Length)
                    {
                        expEnd = checkRanges[i].limit - 1;
                        expValue = checkRanges[i].value ^ 0x5555;
                    }
                    else
                    {
                        expEnd = -1;
                        expValue = 0x5005;
                    }

                    getRangeResult = trie.GetRange(start, option, surrValue ^ 0x5555, TestFilter, range);
                    if (!DoCheckRange(name, "with value handler",
                        start,              getRangeResult, range, expEnd, expValue))
                    {
                        break;
                    }

                    if (s != 0 && ++innerLoopCount == 5)
                    {
                        break;
                    }
                }

                // C also tests without value (with a NULL value pointer),
                // but that does not apply to Java.
            }
        }

        // Note: There is much less to do here in polymorphic Java than in C
        // where we have many specialized macros in addition to generic functions.
        private static void TestTrieGetters(string testName, CodePointTrie trie,
                                            CodePointTrie.Kind kind,
                                            CheckRange[] checkRanges)
        {
            int value, value2;
            int i;
            var countErrors = 0;

            var fastTrie =
                kind == CodePointTrie.Kind.Fast ? (CodePointTrie.Fast) trie : null;
            var typeName = "trie";

            var specials = GetSpecialValues(checkRanges);

            var start = 0;
            for (i = specials.i; i < checkRanges.Length; ++i)
            {
                var limit = checkRanges[i].limit;
                value = checkRanges[i].value;

                while (start < limit)
                {
                    if (start <= 0x7f)
                    {
                        value2 = trie.AsciiGet(start);
                        if (value != value2)
                        {
                            Fail($"error: {typeName}({testName}).fromASCII(U+{start:X4})==0x{value2:X} instead of 0x{value:X}\n");
                            ++countErrors;
                        }
                    }

                    if (fastTrie != null)
                    {
                        if (start <= 0xffff)
                        {
                            value2 = fastTrie.BmpGet(start);
                            if (value != value2)
                            {
                                Fail($"error: {typeName}({testName}).fromBMP(U+{start:X4})==0x{value2:X} instead of 0x{value:X}\n");
                                ++countErrors;
                            }
                        }
                        else
                        {
                            value2 = fastTrie.SuppGet(start);
                            if (value != value2)
                            {
                                Fail($"error: {typeName}({testName}).fromSupp(U+{start:X4})==0x{value2:X} instead of 0x{value:X}\n");
                                ++countErrors;
                            }
                        }
                    }

                    value2 = trie.Get(start);
                    if (value != value2)
                    {
                        Fail($"error: {typeName}({testName}).get(U+{start:X4})==0x{value2:X} instead of 0x{value:X}\n");
                        ++countErrors;
                    }

                    ++start;
                    if (countErrors > 10)
                    {
                        return;
                    }
                }
            }

            /* test errorValue */
            value = trie.Get(-1);
            value2 = trie.Get(0x110000);
            if (value != specials.errorValue || value2 != specials.errorValue)
            {
                Fail($"error: {typeName}({testName}).get(out of range) != errorValue\n");
            }
        }

        private static void TestBuilderGetters(string testName, MutableCodePointTrie mutableTrie, CheckRange[] checkRanges)
        {
            int value, value2;
            int i;
            var countErrors = 0;

            var typeName = "mutableTrie";

            var specials = GetSpecialValues(checkRanges);

            var start = 0;
            for (i = specials.i; i < checkRanges.Length; ++i)
            {
                var limit = checkRanges[i].limit;
                value = checkRanges[i].value;

                while (start < limit)
                {
                    value2 = mutableTrie.Get(start);
                    if (value != value2)
                    {
                        Fail($"error: {typeName}({testName}).get(U+{start:X4})==0x{value2:X} instead of 0x{value:X}\n");
                        ++countErrors;
                    }

                    ++start;
                    if (countErrors > 10)
                    {
                        return;
                    }
                }
            }

            /* test errorValue */
            value = mutableTrie.Get(-1);
            value2 = mutableTrie.Get(0x110000);
            if (value != specials.errorValue || value2 != specials.errorValue)
            {
                Fail($"error: {typeName}({testName}).get(out of range) != errorValue\n");
            }
        }

        private static bool ACCIDENTAL_SURROGATE_PAIR(ICharSequence s, int cp)
        {
            return s.Length > 0 &&
                   char.IsHighSurrogate(s.CharAt(s.Length - 1)) &&
                   char.IsLowSurrogate((char) cp);
        }

        private static void TestTrieUtf16(string testName, CodePointTrie trie, CheckRange[] checkRanges)
        {
            var s = new StringBuilder();
            var values = new int[16000];

            var errorValue = trie.Get(-1);
            int value, expected;
            int c, c2;
            int i;

            /* write a string */
            var prevCP = 0;
            var countValues = 0;
            for (i = SkipSpecialValues(checkRanges); i < checkRanges.Length; ++i)
            {
                value = checkRanges[i].value;
                /* write three code points */
                if (!ACCIDENTAL_SURROGATE_PAIR(s, prevCP))
                {
                    s.appendCodePoint(prevCP); /* start of the range */
                    values[countValues++] = value;
                }

                c = checkRanges[i].limit;
                prevCP = (prevCP + c) / 2; /* middle of the range */
                if (!ACCIDENTAL_SURROGATE_PAIR(s, prevCP))
                {
                    s.appendCodePoint(prevCP);
                    values[countValues++] = value;
                }

                prevCP = c;
                --c; /* end of the range */
                if (!ACCIDENTAL_SURROGATE_PAIR(s, c))
                {
                    s.appendCodePoint(c);
                    values[countValues++] = value;
                }
            }

            var si = trie.GetStringIterator(s.ToString(), 0);

            /* try forward */
            var sIndex = 0;
            i = 0;
            while (sIndex < s.Length)
            {
                c2 = s.codePointAt(sIndex);
                sIndex += Character.charCount(c2);
                Assert.True(si.Next(), "next() at " + si.Index);
                c = si.CodePoint;
                value = si.Value;
                expected = Normalizer2Impl.UTF16Plus.isSurrogate(c) ? errorValue : values[i];
                if (value != expected)
                {
                    Fail($"error: wrong value from UCPTRIE_NEXT({testName})(U+{c:X4}): 0x{value:X} instead of 0x{expected:X}\n");
                }

                if (c != c2)
                {
                    Fail($"error: wrong code point from UCPTRIE_NEXT({testName}): U+{c:X4} != U+{c2:X4}\n");
                    continue;
                }

                ++i;
            }

            Assert.False(si.Next(), "next() at the end");

            /* try backward */
            sIndex = s.Length;
            i = countValues;
            while (sIndex > 0)
            {
                --i;
                c2 = s.codePointBefore(sIndex);
                sIndex -= Character.charCount(c2);
                Assert.True(si.Previous(), "previous() at " + si.Index);
                c = si.CodePoint;
                value = si.Value;
                expected = Normalizer2Impl.UTF16Plus.isSurrogate(c) ? errorValue : values[i];
                if (value != expected)
                {
                    Fail($"error: wrong value from UCPTRIE_PREV({testName})(U+{c:X4}): 0x{value:X} instead of 0x{expected:X}\n");
                }

                if (c != c2)
                {
                    Fail($"error: wrong code point from UCPTRIE_PREV({testName}): U+{c:X4} != U+{c2:X4}\n");
                }
            }

            Assert.False(si.Previous(), "previous() at the start");
        }

        private static void TestTrie(string testName, CodePointTrie trie,
                                     CodePointTrie.Kind kind,
                                     CheckRange[] checkRanges)
        {
            TestTrieGetters(testName, trie, kind, checkRanges);
            TestTrieGetRanges(testName, trie, CodePointMap.RangeOption.Normal, 0, checkRanges);
            if (kind == CodePointTrie.Kind.Fast)
            {
                TestTrieUtf16(testName, trie, checkRanges);
                // Java: no testTrieUTF8(testName, trie, valueWidth, checkRanges);
            }
        }

        private static void TestBuilder(string testName, MutableCodePointTrie mutableTrie, CheckRange[] checkRanges)
        {
            TestBuilderGetters(testName, mutableTrie, checkRanges);
            TestTrieGetRanges(testName, mutableTrie, CodePointMap.RangeOption.Normal, 0, checkRanges);
        }

        private static void TestTrieSerialize(string testName, MutableCodePointTrie mutableTrie,
                                              CodePointTrie.Kind kind, CodePointTrie.ValueWidth valueWidth,
                                              CheckRange[] checkRanges)
        {
            /* clone the trie so that the caller can reuse the original */
            mutableTrie = (MutableCodePointTrie) mutableTrie.Clone();

            /*
             * This is not a loop, but simply a block that we can exit with "break"
             * when something goes wrong.
             */
            do
            {
                var trie = mutableTrie.BuildImmutable(kind, valueWidth);
                var stream = new MemoryStream();
                var length1 = trie.ToBinary(stream);
                // assertEquals(testName + ".toBinary() length", os.size(), length1);
                Assert.Equal(length1, stream.Length);
                var storage = new MemoryStream(stream.ToArray());
                // Java: no preflighting

                TestTrie(testName, trie, kind, checkRanges);

                // Java: There is no code for "swapping" the endianness of data.
                // withSwap is unused.

                trie = CodePointTrie.FromBinary(storage, kind, valueWidth);
                if (kind != trie.GetKind())
                {
                    Fail($"error: trie serialization ({testName}) did not preserve trie kind\n");
                    break;
                }

                if (valueWidth != trie.GetValueWidth())
                {
                    Fail($"error: trie serialization ({testName}) did not preserve data value width\n");
                    break;
                }

                if (stream.Length != storage.Position)
                {
                    Fail($"error: trie serialization ({testName}) lengths different: " +
                         "serialize vs. unserialize\n");
                    break;
                }

                {
                    storage.Position = 0;
                    var any = CodePointTrie.FromBinary(storage, null, null);
                    if (kind != any.GetKind())
                    {
                        Fail("error: ucptrie_openFromBinary(UCPTRIE_TYPE_ANY, UCPTRIE_VALUE_BITS_ANY).GetKind() wrong\n");
                    }

                    if (valueWidth != any.GetValueWidth())
                    {
                        Fail("error: ucptrie_openFromBinary(UCPTRIE_TYPE_ANY, UCPTRIE_VALUE_BITS_ANY).getValueWidth() wrong\n");
                    }
                }

                TestTrie(testName, trie, kind, checkRanges);
                {
                    /* make a mutable trie from an immutable one */
                    var mutable2 = MutableCodePointTrie.FromCodePointMap(trie);

                    var value = mutable2.Get(0xa1);
                    mutable2.Set(0xa1, 789);
                    var value2 = mutable2.Get(0xa1);
                    mutable2.Set(0xa1, value);
                    if (value2 != 789)
                    {
                        Fail($"error: modifying a mutableTrie-from-UCPTrie ({testName}) failed\n");
                    }

                    TestBuilder(testName, mutable2, checkRanges);
                }
            } while (false);
        }

        private static MutableCodePointTrie TestTrieSerializeAllValueWidth(string testName,
                                                                           MutableCodePointTrie mutableTrie,
                                                                           CheckRange[] checkRanges)
        {
            var oredValues = 0;
            int i;
            for (i = 0; i < checkRanges.Length; ++i)
            {
                oredValues |= checkRanges[i].value;
            }

            TestBuilder(testName, mutableTrie, checkRanges);

            if (oredValues <= 0xffff)
            {
                var _name = testName + ".16";
                TestTrieSerialize(_name,     mutableTrie,
                    CodePointTrie.Kind.Fast, CodePointTrie.ValueWidth.Bits16,
                    checkRanges);
            }

            var name = testName + ".32";
            TestTrieSerialize(name,      mutableTrie,
                CodePointTrie.Kind.Fast, CodePointTrie.ValueWidth.Bits32,
                checkRanges);

            if (oredValues <= 0xff)
            {
                name = testName + ".8";
                TestTrieSerialize(name,      mutableTrie,
                    CodePointTrie.Kind.Fast, CodePointTrie.ValueWidth.Bits8,
                    checkRanges);
            }

            if (oredValues <= 0xffff)
            {
                name = testName + ".small16";
                TestTrieSerialize(name,       mutableTrie,
                    CodePointTrie.Kind.Small, CodePointTrie.ValueWidth.Bits16,
                    checkRanges);
            }

            return mutableTrie;
        }

        private static MutableCodePointTrie MakeTrieWithRanges(string testName, bool withClone,
                                                               SetRange[] setRanges, CheckRange[] checkRanges)
        {
            int i;

            Console.WriteLine("\ntesting Trie " + testName);
            var specials = GetSpecialValues(checkRanges);
            var mutableTrie = new MutableCodePointTrie(specials.initialValue, specials.errorValue);

            /* set values from setRanges[] */
            for (i = 0; i < setRanges.Length; ++i)
            {
                if (withClone && i == setRanges.Length / 2)
                {
                    /* switch to a clone in the middle of setting values */
                    var clone = (MutableCodePointTrie) mutableTrie.Clone();
                    mutableTrie = clone;
                }

                var start = setRanges[i].start;
                var limit = setRanges[i].limit;
                var value = setRanges[i].value;
                if ((limit - start) == 1)
                {
                    mutableTrie.Set(start, value);
                }
                else
                {
                    mutableTrie.SetRange(start, limit - 1, value);
                }
            }

            return mutableTrie;
        }

        private static void TestTrieRanges(string testName, bool withClone, SetRange[] setRanges, CheckRange[] checkRanges)
        {
            var mutableTrie = MakeTrieWithRanges(
                testName, withClone, setRanges, checkRanges);
            if (mutableTrie != null)
            {
                mutableTrie = TestTrieSerializeAllValueWidth(testName, mutableTrie, checkRanges);
            }
        }

        /* test data ----------------------------------------------------------------*/

        /* set consecutive ranges, even with value 0 */
        private static readonly SetRange[] SetRanges1 =
        {
            new SetRange(0,       0x40,     0),
            new SetRange(0x40,    0xe7,     0x34),
            new SetRange(0xe7,    0x3400,   0),
            new SetRange(0x3400,  0x9fa6,   0x61),
            new SetRange(0x9fa6,  0xda9e,   0x31),
            new SetRange(0xdada,  0xeeee,   0xff),
            new SetRange(0xeeee,  0x11111,  1),
            new SetRange(0x11111, 0x44444,  0x61),
            new SetRange(0x44444, 0x60003,  0),
            new SetRange(0xf0003, 0xf0004,  0xf),
            new SetRange(0xf0004, 0xf0006,  0x10),
            new SetRange(0xf0006, 0xf0007,  0x11),
            new SetRange(0xf0007, 0xf0040,  0x12),
            new SetRange(0xf0040, 0x110000, 0)
        };

        private static readonly CheckRange[] CheckRanges1 =
        {
            new CheckRange(0,        0),
            new CheckRange(0x40,     0),
            new CheckRange(0xe7,     0x34),
            new CheckRange(0x3400,   0),
            new CheckRange(0x9fa6,   0x61),
            new CheckRange(0xda9e,   0x31),
            new CheckRange(0xdada,   0),
            new CheckRange(0xeeee,   0xff),
            new CheckRange(0x11111,  1),
            new CheckRange(0x44444,  0x61),
            new CheckRange(0xf0003,  0),
            new CheckRange(0xf0004,  0xf),
            new CheckRange(0xf0006,  0x10),
            new CheckRange(0xf0007,  0x11),
            new CheckRange(0xf0040,  0x12),
            new CheckRange(0x110000, 0)
        };

        /* set some interesting overlapping ranges */
        private static readonly SetRange[] SetRanges2 =
        {
            new SetRange(0x21,    0x7f,    0x5555),
            new SetRange(0x2f800, 0x2fedc, 0x7a),
            new SetRange(0x72,    0xdd,    3),
            new SetRange(0xdd,    0xde,    4),
            new SetRange(0x201,   0x240,   6), /* 3 consecutive blocks with the same pattern but */
            new SetRange(0x241,   0x280,   6), /* discontiguous value ranges, testing iteration */
            new SetRange(0x281,   0x2c0,   6),
            new SetRange(0x2f987, 0x2fa98, 5),
            new SetRange(0x2f777, 0x2f883, 0),
            new SetRange(0x2fedc, 0x2ffaa, 1),
            new SetRange(0x2ffaa, 0x2ffab, 2),
            new SetRange(0x2ffbb, 0x2ffc0, 7)
        };

        private static readonly CheckRange[] CheckRanges2 =
        {
            new CheckRange(0,        0),
            new CheckRange(0x21,     0),
            new CheckRange(0x72,     0x5555),
            new CheckRange(0xdd,     3),
            new CheckRange(0xde,     4),
            new CheckRange(0x201,    0),
            new CheckRange(0x240,    6),
            new CheckRange(0x241,    0),
            new CheckRange(0x280,    6),
            new CheckRange(0x281,    0),
            new CheckRange(0x2c0,    6),
            new CheckRange(0x2f883,  0),
            new CheckRange(0x2f987,  0x7a),
            new CheckRange(0x2fa98,  5),
            new CheckRange(0x2fedc,  0x7a),
            new CheckRange(0x2ffaa,  1),
            new CheckRange(0x2ffab,  2),
            new CheckRange(0x2ffbb,  0),
            new CheckRange(0x2ffc0,  7),
            new CheckRange(0x110000, 0)
        };

        /* use a non-zero initial value */
        private static readonly SetRange[] SetRanges3 =
        {
            new SetRange(0x31,    0xa4,     1),
            new SetRange(0x3400,  0x6789,   2),
            new SetRange(0x8000,  0x89ab,   9),
            new SetRange(0x9000,  0xa000,   4),
            new SetRange(0xabcd,  0xbcde,   3),
            new SetRange(0x55555, 0x110000, 6), /* highStart<U+ffff with non-initialValue */
            new SetRange(0xcccc,  0x55555,  6)
        };

        private static readonly CheckRange[] CheckRanges3 =
        {
            new CheckRange(0,        9), /* non-zero initialValue */
            new CheckRange(0x31,     9),
            new CheckRange(0xa4,     1),
            new CheckRange(0x3400,   9),
            new CheckRange(0x6789,   2),
            new CheckRange(0x9000,   9),
            new CheckRange(0xa000,   4),
            new CheckRange(0xabcd,   9),
            new CheckRange(0xbcde,   3),
            new CheckRange(0xcccc,   9),
            new CheckRange(0x110000, 6)
        };

        /* empty or single-value tries, testing highStart==0 */
        private static readonly SetRange[] SetRangesEmpty =
        {
            // new SetRange(0,        0,        0),  /* need some values for it to compile */
        };

        private static readonly CheckRange[] CheckRangesEmpty =
        {
            new CheckRange(0,        3),
            new CheckRange(0x110000, 3)
        };

        private static readonly SetRange[] SetRangesSingleValue =
        {
            new SetRange(0, 0x110000, 5),
        };

        private static readonly CheckRange[] CheckRangesSingleValue =
        {
            new CheckRange(0,        3),
            new CheckRange(0x110000, 5)
        };

        [Fact]
        public void TrieTestSet1()
        {
            TestTrieRanges("set1", false, SetRanges1, CheckRanges1);
        }

        [Fact]
        public void TrieTestSet2Overlap()
        {
            TestTrieRanges("set2-overlap", false, SetRanges2, CheckRanges2);
        }

        [Fact]
        public void TrieTestSet3Initial9()
        {
            TestTrieRanges("set3-initial-9", false, SetRanges3, CheckRanges3);
        }

        [Fact]
        public void TrieTestSetEmpty()
        {
            TestTrieRanges("set-empty", false, SetRangesEmpty, CheckRangesEmpty);
        }

        [Fact]
        public void TrieTestSetSingleValue()
        {
            TestTrieRanges("set-single-value", false, SetRangesSingleValue, CheckRangesSingleValue);
        }

        [Fact]
        public void TrieTestSet2OverlapWithClone()
        {
            TestTrieRanges("set2-overlap.withClone", true, SetRanges2, CheckRanges2);
        }


        /* test mutable-trie memory management -------------------------------------- */

        [Fact]
        public void FreeBlocksTest()
        {
            CheckRange[] checkRanges =
            {
                new CheckRange(0,        1),
                new CheckRange(0x740,    1),
                new CheckRange(0x780,    2),
                new CheckRange(0x880,    3),
                new CheckRange(0x110000, 1)
            };
            var testName = "free-blocks";

            int i;

            var mutableTrie = new MutableCodePointTrie(1, 0xad);

            /*
             * Repeatedly set overlapping same-value ranges to stress the free-data-block management.
             * If it fails, it will overflow the data array.
             */
            for (i = 0; i < (0x120000 >> 4) / 2; ++i)
            {
                // 4=UCPTRIE_SHIFT_3
                mutableTrie.SetRange(0x740, 0x840 - 1, 1);
                mutableTrie.SetRange(0x780, 0x880 - 1, 1);
                mutableTrie.SetRange(0x740, 0x840 - 1, 2);
                mutableTrie.SetRange(0x780, 0x880 - 1, 3);
            }

            /* make blocks that will be free during compaction */
            mutableTrie.SetRange(0x1000, 0x3000 - 1, 2);
            mutableTrie.SetRange(0x2000, 0x4000 - 1, 3);
            mutableTrie.SetRange(0x1000, 0x4000 - 1, 1);

            mutableTrie = TestTrieSerializeAllValueWidth(testName, mutableTrie, checkRanges);
        }

        [Fact]
        public void GrowDataArrayTest()
        {
            CheckRange[] checkRanges =
            {
                new CheckRange(0,        1),
                new CheckRange(0x720,    2),
                new CheckRange(0x7a0,    3),
                new CheckRange(0x8a0,    4),
                new CheckRange(0x110000, 5)
            };
            var testName = "grow-data";

            int i;

            var mutableTrie = new MutableCodePointTrie(1, 0xad);

            /*
             * Use umutablecptrie_set() not umutablecptrie_setRange() to write non-initialValue-data.
             * Should grow/reallocate the data array to a sufficient length.
             */
            for (i = 0; i < 0x1000; ++i)
            {
                mutableTrie.Set(i, 2);
            }

            for (i = 0x720; i < 0x1100; ++i)
            {
                /* some overlap */
                mutableTrie.Set(i, 3);
            }

            for (i = 0x7a0; i < 0x900; ++i)
            {
                mutableTrie.Set(i, 4);
            }

            for (i = 0x8a0; i < 0x110000; ++i)
            {
                mutableTrie.Set(i, 5);
            }

            mutableTrie = TestTrieSerializeAllValueWidth(testName, mutableTrie, checkRanges);
        }

        [Fact]
        public void ManyAllSameBlocksTest()
        {
            var testName = "many-all-same";

            int i;
            var checkRanges = new CheckRange[(0x110000 >> 12) + 1];

            var mutableTrie = new MutableCodePointTrie(0xff33, 0xad);
            checkRanges[0] = new CheckRange(0, 0xff33); // initialValue

            // Many all-same-value blocks.
            for (i = 0; i < 0x110000; i += 0x1000)
            {
                var value = i >> 12;
                mutableTrie.SetRange(i, i + 0xfff, value);
                checkRanges[value + 1] = new CheckRange(i + 0x1000, value);
            }

            for (i = 0; i < 0x110000; i += 0x1000)
            {
                var expected = i >> 12;
                var v0 = mutableTrie.Get(i);
                var vfff = mutableTrie.Get(i + 0xfff);
                if (v0 != expected || vfff != expected)
                {
                    Fail($"error: MutableCodePointTrie U+{i:X4} unexpected value\n");
                }
            }

            mutableTrie = TestTrieSerializeAllValueWidth(testName, mutableTrie, checkRanges);
        }

        [Fact]
        public void MuchDataTest()
        {
            var testName = "much-data";

            int c;
            var checkRanges = new CheckRange[(0x10000 >> 6) + (0x10240 >> 4) + 10];

            var mutableTrie = new MutableCodePointTrie(0xff33, 0xad);
            checkRanges[0] = new CheckRange(0, 0xff33); // initialValue
            var r = 1;

            // Add much data that does not compact well,
            // to get more than 128k data values after compaction.
            for (c = 0; c < 0x10000; c += 0x40)
            {
                var value = c >> 4;
                mutableTrie.SetRange(c, c + 0x3f, value);
                checkRanges[r++] = new CheckRange(c + 0x40, value);
            }

            checkRanges[r++] = new CheckRange(0x20000, 0xff33);
            for (c = 0x20000; c < 0x30230; c += 0x10)
            {
                var value = c >> 4;
                mutableTrie.SetRange(c, c + 0xf, value);
                checkRanges[r++] = new CheckRange(c + 0x10, value);
            }

            mutableTrie.SetRange(0x30230, 0x30233, 0x3023);
            checkRanges[r++] = new CheckRange(0x30234, 0x3023);
            mutableTrie.SetRange(0x30234, 0xdffff, 0x5005);
            checkRanges[r++] = new CheckRange(0xe0000, 0x5005);
            mutableTrie.SetRange(0xe0000, 0x10ffff, 0x9009);
            checkRanges[r++] = new CheckRange(0x110000, 0x9009);

            var _checkRanges = new CheckRange[r];
            Array.Copy(checkRanges, _checkRanges, r);
            checkRanges = _checkRanges;
            TestBuilder(testName, mutableTrie, checkRanges);
            TestTrieSerialize("much-data.16", mutableTrie,
                CodePointTrie.Kind.Fast,      CodePointTrie.ValueWidth.Bits16,
                checkRanges);
        }

        private void testGetRangesFixedSurr(string testName, MutableCodePointTrie mutableTrie,
                                            CodePointMap.RangeOption option, CheckRange[] checkRanges)
        {
            TestTrieGetRanges(testName, mutableTrie, option, 5, checkRanges);
            var clone = (MutableCodePointTrie) mutableTrie.Clone();
            var trie =
                clone.BuildImmutable(CodePointTrie.Kind.Fast, CodePointTrie.ValueWidth.Bits16);
            TestTrieGetRanges(testName, trie, option, 5, checkRanges);
        }

        [Fact]
        public void TrieTestGetRangesFixedSurr()
        {
            SetRange[] setRangesFixedSurr =
            {
                new SetRange(0xd000, 0xd7ff, 5),
                new SetRange(0xd7ff, 0xe001, 3),
                new SetRange(0xe001, 0xf900, 5),
            };

            CheckRange[] checkRangesFixedLeadSurr1 =
            {
                new CheckRange(0,        0),
                new CheckRange(0xd000,   0),
                new CheckRange(0xd7ff,   5),
                new CheckRange(0xd800,   3),
                new CheckRange(0xdc00,   5),
                new CheckRange(0xe001,   3),
                new CheckRange(0xf900,   5),
                new CheckRange(0x110000, 0)
            };

            CheckRange[] checkRangesFixedAllSurr1 =
            {
                new CheckRange(0,        0),
                new CheckRange(0xd000,   0),
                new CheckRange(0xd7ff,   5),
                new CheckRange(0xd800,   3),
                new CheckRange(0xe000,   5),
                new CheckRange(0xe001,   3),
                new CheckRange(0xf900,   5),
                new CheckRange(0x110000, 0)
            };

            CheckRange[] checkRangesFixedLeadSurr3 =
            {
                new CheckRange(0,        0),
                new CheckRange(0xd000,   0),
                new CheckRange(0xdc00,   5),
                new CheckRange(0xe001,   3),
                new CheckRange(0xf900,   5),
                new CheckRange(0x110000, 0)
            };

            CheckRange[] checkRangesFixedAllSurr3 =
            {
                new CheckRange(0,        0),
                new CheckRange(0xd000,   0),
                new CheckRange(0xe000,   5),
                new CheckRange(0xe001,   3),
                new CheckRange(0xf900,   5),
                new CheckRange(0x110000, 0)
            };

            CheckRange[] checkRangesFixedSurr4 =
            {
                new CheckRange(0,        0),
                new CheckRange(0xd000,   0),
                new CheckRange(0xf900,   5),
                new CheckRange(0x110000, 0)
            };

            var mutableTrie = MakeTrieWithRanges(
                "fixedSurr", false, setRangesFixedSurr, checkRangesFixedLeadSurr1);
            testGetRangesFixedSurr("fixedLeadSurr1",          mutableTrie,
                CodePointMap.RangeOption.FixedLeadSurrogates, checkRangesFixedLeadSurr1);
            testGetRangesFixedSurr("fixedAllSurr1",          mutableTrie,
                CodePointMap.RangeOption.FixedAllSurrogates, checkRangesFixedAllSurr1);
            // Setting a range in the middle of lead surrogates makes no difference.
            mutableTrie.SetRange(0xd844, 0xd899, 5);
            testGetRangesFixedSurr("fixedLeadSurr2",          mutableTrie,
                CodePointMap.RangeOption.FixedLeadSurrogates, checkRangesFixedLeadSurr1);
            // Bridge the gap before the lead surrogates.
            mutableTrie.Set(0xd7ff, 5);
            testGetRangesFixedSurr("fixedLeadSurr3",          mutableTrie,
                CodePointMap.RangeOption.FixedLeadSurrogates, checkRangesFixedLeadSurr3);
            testGetRangesFixedSurr("fixedAllSurr3",          mutableTrie,
                CodePointMap.RangeOption.FixedAllSurrogates, checkRangesFixedAllSurr3);
            // Bridge the gap after the trail surrogates.
            mutableTrie.Set(0xe000, 5);
            testGetRangesFixedSurr("fixedSurr4",             mutableTrie,
                CodePointMap.RangeOption.FixedAllSurrogates, checkRangesFixedSurr4);
        }

        [Fact]
        public void TestSmallNullBlockMatchesFast()
        {
            // The initial builder+getRange code had a bug:
            // When there is no null data block in the fast-index range,
            // but a fast-range data block starts with enough values to match a small data block,
            // then getRange() got confused.
            // The builder must prevent this.
            SetRange[] setRanges =
            {
                new SetRange(0, 0x880, 1),
                // U+0880..U+088F map to initial value 0, potential match for small null data block.
                new SetRange(0x890, 0x1040, 2),
                // U+1040..U+1050 map to 0.
                // First small null data block in a small-kind trie.
                // In a fast-kind trie, it is ok to match a small null data block at U+1041
                // but not at U+1040.
                new SetRange(0x1051, 0x10000, 3),
                // No fast data block (block length 64) filled with 0 regardless of trie kind.
                // Need more blocks filled with 0 than the largest range above,
                // and need a highStart above that so that it actually counts.
                new SetRange(0x20000, 0x110000, 9)
            };

            CheckRange[] checkRanges =
            {
                new CheckRange(0x0880,   1),
                new CheckRange(0x0890,   0),
                new CheckRange(0x1040,   2),
                new CheckRange(0x1051,   0),
                new CheckRange(0x10000,  3),
                new CheckRange(0x20000,  0),
                new CheckRange(0x110000, 9)
            };

            TestTrieRanges("small0-in-fast", false, setRanges, checkRanges);
        }

        [Fact]
        public void ShortAllSameBlocksTest()
        {
            // Many all-same-value blocks but only of the small block length used in the mutable trie.
            // The builder code needs to turn a group of short ALL_SAME blocks below fastLimit
            // into a MIXED block, and reserve data array capacity for that.
            var mutableTrie = new MutableCodePointTrie(0, 0xad);
            var checkRanges = new CheckRange[0x101];
            for (var i = 0; i < 0x1000; i += 0x10)
            {
                var value = i >> 4;
                mutableTrie.SetRange(i, i + 0xf, value);
                checkRanges[value] = new CheckRange(i + 0x10, value);
            }

            checkRanges[0x100] = new CheckRange(0x110000, 0);

            mutableTrie = TestTrieSerializeAllValueWidth(
                "short-all-same", mutableTrie, checkRanges);
        }

        /*
        private void testIntProperty(String testName, String baseSetPattern, int property)
        {
            UnicodeSet uni11 = new UnicodeSet(baseSetPattern);
            MutableCodePointTrie mutableTrie = new MutableCodePointTrie(0, 0xad);
            List<CheckRange> checkRanges = new List<CheckRange>();
            int start = 0;
            int limit = 0;
            int prevValue = 0;
            foreach (UnicodeSet.EntryRange range in uni11.ranges())
            {
                // Ranges are in ascending order, each range is non-empty,
                // and there is a gap from one to the next.
                // Each code point in a range could have a different value.
                // Any of them can be 0.
                // We keep initial value 0 between ranges.
                if (prevValue != 0)
                {
                    mutableTrie.setRange(start, limit - 1, prevValue);
                    checkRanges.Add(new CheckRange(limit, prevValue));
                    start = limit;
                    prevValue = 0;
                }

                int c = limit = range.codepoint;
                do
                {
                    int value;
                    if (property == UProperty.AGE)
                    {
                        VersionInfo version = UCharacter.getAge(c);
                        value = (version.getMajor() << 4) | version.getMinor();
                    }
                    else
                    {
                        value = UCharacter.getIntPropertyValue(c, property);
                    }

                    if (value != prevValue)
                    {
                        if (start < limit)
                        {
                            if (prevValue != 0)
                            {
                                mutableTrie.setRange(start, limit - 1, prevValue);
                            }

                            checkRanges.Add(new CheckRange(limit, prevValue));
                        }

                        start = c;
                        prevValue = value;
                    }

                    limit = ++c;
                } while (c <= range.codepointEnd);
            }

            if (prevValue != 0)
            {
                mutableTrie.setRange(start, limit - 1, prevValue);
                checkRanges.Add(new CheckRange(limit, prevValue));
            }

            if (limit < 0x110000)
            {
                checkRanges.Add(new CheckRange(0x110000, 0));
            }

            testTrieSerializeAllValueWidth(testName, mutableTrie, false, checkRanges.ToArray());
        }

        [Fact]
        public void AgePropertyTest()
        {
            testIntProperty("age", "[:age=11:]", UProperty.AGE);
        }

        [Fact]
        public void BlockPropertyTest()
        {
            testIntProperty("block", "[:^blk=No_Block:]", UProperty.BLOCK);
        }
        */
    }
}