using System;
using System.IO;
using CodeHive.unicode_trie.icu;
using CodeHive.unicode_trie.java;
using Xunit;

// ReSharper disable InconsistentNaming

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

            public override String ToString()
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

            public override String ToString()
            {
                return "≤" + Utility.hex(limit - 1) + ':' + Utility.hex(value);
            }

            internal readonly int limit;
            internal readonly int value;
        }

        private static int skipSpecialValues(CheckRange[] checkRanges)
        {
            int i;
            for (i = 0; i < checkRanges.Length && checkRanges[i].limit <= 0; ++i)
            { }

            return i;
        }

        private static SpecialValues getSpecialValues(CheckRange[] checkRanges)
        {
            int i = 0;
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
        private class TestValueFilter : CodePointMap.ValueFilter
        {
            public int apply(int value)
            {
                return value ^ 0x5555;
            }
        }

        private static readonly TestValueFilter testFilter = new TestValueFilter();

        private bool
            doCheckRange(String name, String variant,
                         int start, bool getRangeResult, CodePointMap.Range range,
                         int expEnd, int expValue)
        {
            if (!getRangeResult)
            {
                if (expEnd >= 0)
                {
                    fail($"error: {name} getRanges ({variant}) fails to deliver range [U+{start:04X}..U+{expEnd:04X}].0x{expValue:X}\n");
                }

                return false;
            }

            if (expEnd < 0)
            {
                fail($"error: {name} getRanges ({variant}) delivers unexpected range [U+{range.getStart():04X}..U+{range.getEnd():04X}].0x{range.getValue():X}\n");
                return false;
            }

            if (range.getStart() != start || range.getEnd() != expEnd || range.getValue() != expValue)
            {
                fail($"error: {name} getRanges ({variant}) delivers wrong range [U+{range.getStart():04X}..U+{range.getEnd():04X}].0x{range.getValue():X} " +
                     $"instead of [U+{start:04X}..U+{expEnd:04X}].0x{expValue:X}\n");
                return false;
            }

            return true;
        }

        // Test iteration starting from various UTF-8/16 and trie structure boundaries.
        // Also test starting partway through lead & trail surrogates for fixed-surrogate-value options,
        // and partway through supplementary code points.
        private static int[] iterStarts =
        {
            0, 0x7f, 0x80, 0x7ff, 0x800, 0xfff, 0x1000,
            0xd7ff, 0xd800, 0xd888, 0xdddd, 0xdfff, 0xe000,
            0xffff, 0x10000, 0x12345, 0x10ffff, 0x110000
        };

        private void
            testTrieGetRanges(String testName, CodePointMap trie,
                              CodePointMap.RangeOption option, int surrValue,
                              CheckRange[] checkRanges)
        {
            String typeName = (trie is MutableCodePointTrie) ? "mutableTrie" : "trie";
            CodePointMap.Range range = new CodePointMap.Range();
            for (int s = 0; s < iterStarts.Length; ++s)
            {
                int start = iterStarts[s];
                int i, i0;
                int expEnd;
                int expValue;
                bool getRangeResult;
                // No need to go from each iteration start to the very end.
                int innerLoopCount;

                String name = $"{typeName}/{option}({testName}) min=U+{start:04X}";

                // Skip over special values and low ranges.
                for (i = 0; i < checkRanges.Length && checkRanges[i].limit <= start; ++i)
                { }

                i0 = i;
                // without value handler
                for (innerLoopCount = 0;; ++i, start = range.getEnd() + 1)
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

                    getRangeResult = option != CodePointMap.RangeOption.NORMAL ? trie.getRange(start, option, surrValue, null, range) : trie.getRange(start, null, range);
                    if (!doCheckRange(name, "without value handler",
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
                for (i = i0, start = iterStarts[s], innerLoopCount = 0;;
                    ++i, start = range.getEnd() + 1)
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

                    getRangeResult = trie.getRange(start, option, surrValue ^ 0x5555, testFilter, range);
                    if (!doCheckRange(name, "with value handler",
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
        private void
            testTrieGetters(String testName, CodePointTrie trie,
                            CodePointTrie.Type type, CodePointTrie.ValueWidth valueWidth,
                            CheckRange[] checkRanges)
        {
            int value, value2;
            int start, limit;
            int i;
            int countErrors = 0;

            CodePointTrie.Fast fastTrie =
                type == CodePointTrie.Type.FAST ? (CodePointTrie.Fast) trie : null;
            String typeName = "trie";

            SpecialValues specials = getSpecialValues(checkRanges);

            start = 0;
            for (i = specials.i; i < checkRanges.Length; ++i)
            {
                limit = checkRanges[i].limit;
                value = checkRanges[i].value;

                while (start < limit)
                {
                    if (start <= 0x7f)
                    {
                        value2 = trie.asciiGet(start);
                        if (value != value2)
                        {
                            fail($"error: {typeName}({testName}).fromASCII(U+{start:04X})==0x{value2:X} instead of 0x{value:X}\n");
                            ++countErrors;
                        }
                    }

                    if (fastTrie != null)
                    {
                        if (start <= 0xffff)
                        {
                            value2 = fastTrie.bmpGet(start);
                            if (value != value2)
                            {
                                fail($"error: {typeName}({testName}).fromBMP(U+{start:04X})==0x{value2:X} instead of 0x{value:X}\n");
                                ++countErrors;
                            }
                        }
                        else
                        {
                            value2 = fastTrie.suppGet(start);
                            if (value != value2)
                            {
                                fail($"error: {typeName}({testName}).fromSupp(U+{start:04X})==0x{value2:X} instead of 0x{value:X}\n");
                                ++countErrors;
                            }
                        }
                    }

                    value2 = trie.get(start);
                    if (value != value2)
                    {
                        fail($"error: {typeName}({testName}).get(U+{start:04X})==0x{value2:X} instead of 0x{value:X}\n");
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
            value = trie.get(-1);
            value2 = trie.get(0x110000);
            if (value != specials.errorValue || value2 != specials.errorValue)
            {
                fail($"error: {typeName}({testName}).get(out of range) != errorValue\n");
            }
        }

        private void
            testBuilderGetters(String testName, MutableCodePointTrie mutableTrie, CheckRange[] checkRanges)
        {
            int value, value2;
            int start, limit;
            int i;
            int countErrors = 0;

            String typeName = "mutableTrie";

            SpecialValues specials = getSpecialValues(checkRanges);

            start = 0;
            for (i = specials.i; i < checkRanges.Length; ++i)
            {
                limit = checkRanges[i].limit;
                value = checkRanges[i].value;

                while (start < limit)
                {
                    value2 = mutableTrie.get(start);
                    if (value != value2)
                    {
                        fail($"error: {typeName}({testName}).get(U+{start:04X})==0x{value2:X} instead of 0x{value:X}\n");
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
            value = mutableTrie.get(-1);
            value2 = mutableTrie.get(0x110000);
            if (value != specials.errorValue || value2 != specials.errorValue)
            {
                fail($"error: {typeName}({testName}).get(out of range) != errorValue\n");
            }
        }

        private static bool ACCIDENTAL_SURROGATE_PAIR(CharSequence s, int cp)
        {
            return s.length() > 0 &&
                   Character.isHighSurrogate(s.charAt(s.length() - 1)) &&
                   Normalizer2Impl.UTF16Plus.isTrailSurrogate(cp);
        }

        private void
            testTrieUTF16(String testName,
                          CodePointTrie trie, CodePointTrie.ValueWidth valueWidth,
                          CheckRange[] checkRanges)
        {
            StringBuilder s = new StringBuilder();
            int[] values = new int[16000];

            int errorValue = trie.get(-1);
            int value, expected;
            int prevCP, c, c2;
            int i, sIndex, countValues;

            /* write a string */
            prevCP = 0;
            countValues = 0;
            for (i = skipSpecialValues(checkRanges); i < checkRanges.Length; ++i)
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

            CodePointMap.StringIterator si = trie.stringIterator(s, 0);

            /* try forward */
            sIndex = 0;
            i = 0;
            while (sIndex < s.length())
            {
                c2 = s.codePointAt(sIndex);
                sIndex += Character.charCount(c2);
                Assert.True(si.next(), "next() at " + si.getIndex());
                c = si.getCodePoint();
                value = si.getValue();
                expected = Normalizer2Impl.UTF16Plus.isSurrogate(c) ? errorValue : values[i];
                if (value != expected)
                {
                    fail($"error: wrong value from UCPTRIE_NEXT({testName})(U+{c:04X}): 0x{value:X} instead of 0x{expected:X}\n");
                }

                if (c != c2)
                {
                    fail($"error: wrong code point from UCPTRIE_NEXT({testName}): U+{c:04X} != U+{c2:04X}\n");
                    continue;
                }

                ++i;
            }

            Assert.False(si.next(), "next() at the end");

            /* try backward */
            sIndex = s.length();
            i = countValues;
            while (sIndex > 0)
            {
                --i;
                c2 = s.codePointBefore(sIndex);
                sIndex -= Character.charCount(c2);
                Assert.True(si.previous(), "previous() at " + si.getIndex());
                c = si.getCodePoint();
                value = si.getValue();
                expected = Normalizer2Impl.UTF16Plus.isSurrogate(c) ? errorValue : values[i];
                if (value != expected)
                {
                    fail($"error: wrong value from UCPTRIE_PREV({testName})(U+{c:04X}): 0x{value:X} instead of 0x{expected:X}\n");
                }

                if (c != c2)
                {
                    fail($"error: wrong code point from UCPTRIE_PREV({testName}): U+{c:04X} != U+{c2:04X}\n");
                }
            }

            Assert.False(si.previous(), "previous() at the start");
        }

        private void
            testTrie(String testName, CodePointTrie trie,
                     CodePointTrie.Type type, CodePointTrie.ValueWidth valueWidth,
                     CheckRange[] checkRanges)
        {
            testTrieGetters(testName, trie, type, valueWidth, checkRanges);
            testTrieGetRanges(testName, trie, CodePointMap.RangeOption.NORMAL, 0, checkRanges);
            if (type == CodePointTrie.Type.FAST)
            {
                testTrieUTF16(testName, trie, valueWidth, checkRanges);
                // Java: no testTrieUTF8(testName, trie, valueWidth, checkRanges);
            }
        }

        private void
            testBuilder(String testName, MutableCodePointTrie mutableTrie, CheckRange[] checkRanges)
        {
            testBuilderGetters(testName, mutableTrie, checkRanges);
            testTrieGetRanges(testName, mutableTrie, CodePointMap.RangeOption.NORMAL, 0, checkRanges);
        }

        private void
            testTrieSerialize(String testName, MutableCodePointTrie mutableTrie,
                              CodePointTrie.Type type, CodePointTrie.ValueWidth valueWidth, bool withSwap,
                              CheckRange[] checkRanges)
        {
            CodePointTrie trie;
            int length1;

            /* clone the trie so that the caller can reuse the original */
            mutableTrie = (MutableCodePointTrie) mutableTrie.Clone();

            /*
             * This is not a loop, but simply a block that we can exit with "break"
             * when something goes wrong.
             */
            do
            {
                trie = mutableTrie.buildImmutable(type, valueWidth);
                var os = new MemoryStream();
                length1 = trie.toBinary(os);
                // assertEquals(testName + ".toBinary() length", os.size(), length1);
                Assert.Equal(length1, os.Length);
                ByteBuffer storage = ByteBuffer.wrap(os.ToArray());
                // Java: no preflighting

                testTrie(testName, trie, type, valueWidth, checkRanges);
                trie = null;

                // Java: There is no code for "swapping" the endianness of data.
                // withSwap is unused.

                trie = CodePointTrie.fromBinary(type, valueWidth, storage);
                if (type != trie.getType())
                {
                    fail($"error: trie serialization ({testName}) did not preserve trie type\n");
                    break;
                }

                if (valueWidth != trie.getValueWidth())
                {
                    fail($"error: trie serialization ({testName}) did not preserve data value width\n");
                    break;
                }

                if (os.Length != storage.position())
                {
                    fail($"error: trie serialization ({testName}) lengths different: " +
                         "serialize vs. unserialize\n");
                    break;
                }

                {
                    storage.rewind();
                    CodePointTrie any = CodePointTrie.fromBinary(null, null, storage);
                    if (type != any.getType())
                    {
                        fail("error: ucptrie_openFromBinary(UCPTRIE_TYPE_ANY, UCPTRIE_VALUE_BITS_ANY).getType() wrong\n");
                    }

                    if (valueWidth != any.getValueWidth())
                    {
                        fail("error: ucptrie_openFromBinary(UCPTRIE_TYPE_ANY, UCPTRIE_VALUE_BITS_ANY).getValueWidth() wrong\n");
                    }
                }

                testTrie(testName, trie, type, valueWidth, checkRanges);
                {
                    /* make a mutable trie from an immutable one */
                    int value, value2;
                    MutableCodePointTrie mutable2 = MutableCodePointTrie.fromCodePointMap(trie);

                    value = mutable2.get(0xa1);
                    mutable2.set(0xa1, 789);
                    value2 = mutable2.get(0xa1);
                    mutable2.set(0xa1, value);
                    if (value2 != 789)
                    {
                        fail($"error: modifying a mutableTrie-from-UCPTrie ({testName}) failed\n");
                    }

                    testBuilder(testName, mutable2, checkRanges);
                }
            } while (false);
        }

        private MutableCodePointTrie
            testTrieSerializeAllValueWidth(String testName,
                                           MutableCodePointTrie mutableTrie, bool withClone,
                                           CheckRange[] checkRanges)
        {
            int oredValues = 0;
            int i;
            for (i = 0; i < checkRanges.Length; ++i)
            {
                oredValues |= checkRanges[i].value;
            }

            testBuilder(testName, mutableTrie, checkRanges);

            if (oredValues <= 0xffff)
            {
                String _name = testName + ".16";
                testTrieSerialize(_name,     mutableTrie,
                    CodePointTrie.Type.FAST, CodePointTrie.ValueWidth.BITS_16, withClone,
                    checkRanges);
            }

            String name = testName + ".32";
            testTrieSerialize(name,      mutableTrie,
                CodePointTrie.Type.FAST, CodePointTrie.ValueWidth.BITS_32, withClone,
                checkRanges);

            if (oredValues <= 0xff)
            {
                name = testName + ".8";
                testTrieSerialize(name,      mutableTrie,
                    CodePointTrie.Type.FAST, CodePointTrie.ValueWidth.BITS_8, withClone,
                    checkRanges);
            }

            if (oredValues <= 0xffff)
            {
                name = testName + ".small16";
                testTrieSerialize(name,       mutableTrie,
                    CodePointTrie.Type.SMALL, CodePointTrie.ValueWidth.BITS_16, withClone,
                    checkRanges);
            }

            return mutableTrie;
        }

        private MutableCodePointTrie
            makeTrieWithRanges(String testName, bool withClone,
                               SetRange[] setRanges, CheckRange[] checkRanges)
        {
            MutableCodePointTrie mutableTrie;
            int value;
            int start, limit;
            int i;

            Console.WriteLine("\ntesting Trie " + testName);
            SpecialValues specials = getSpecialValues(checkRanges);
            mutableTrie = new MutableCodePointTrie(specials.initialValue, specials.errorValue);

            /* set values from setRanges[] */
            for (i = 0; i < setRanges.Length; ++i)
            {
                if (withClone && i == setRanges.Length / 2)
                {
                    /* switch to a clone in the middle of setting values */
                    MutableCodePointTrie clone = (MutableCodePointTrie) mutableTrie.Clone();
                    mutableTrie = clone;
                }

                start = setRanges[i].start;
                limit = setRanges[i].limit;
                value = setRanges[i].value;
                if ((limit - start) == 1)
                {
                    mutableTrie.set(start, value);
                }
                else
                {
                    mutableTrie.setRange(start, limit - 1, value);
                }
            }

            return mutableTrie;
        }

        private void
            testTrieRanges(String testName, bool withClone, SetRange[] setRanges, CheckRange[] checkRanges)
        {
            MutableCodePointTrie mutableTrie = makeTrieWithRanges(
                testName, withClone, setRanges, checkRanges);
            if (mutableTrie != null)
            {
                mutableTrie = testTrieSerializeAllValueWidth(testName, mutableTrie, withClone, checkRanges);
            }
        }

        /* test data ----------------------------------------------------------------*/

        /* set consecutive ranges, even with value 0 */
        private static SetRange[] setRanges1 =
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

        private static readonly CheckRange[] checkRanges1 =
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
        private static readonly SetRange[] setRanges2 =
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

        private static readonly CheckRange[] checkRanges2 =
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
        private static readonly SetRange[] setRanges3 =
        {
            new SetRange(0x31,    0xa4,     1),
            new SetRange(0x3400,  0x6789,   2),
            new SetRange(0x8000,  0x89ab,   9),
            new SetRange(0x9000,  0xa000,   4),
            new SetRange(0xabcd,  0xbcde,   3),
            new SetRange(0x55555, 0x110000, 6), /* highStart<U+ffff with non-initialValue */
            new SetRange(0xcccc,  0x55555,  6)
        };

        private static readonly CheckRange[] checkRanges3 =
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
        private static readonly SetRange[] setRangesEmpty =
        {
            // new SetRange(0,        0,        0),  /* need some values for it to compile */
        };

        private static readonly CheckRange[] checkRangesEmpty =
        {
            new CheckRange(0,        3),
            new CheckRange(0x110000, 3)
        };

        private static readonly SetRange[] setRangesSingleValue =
        {
            new SetRange(0, 0x110000, 5),
        };

        private static readonly CheckRange[] checkRangesSingleValue =
        {
            new CheckRange(0,        3),
            new CheckRange(0x110000, 5)
        };

        [Fact]
        public void TrieTestSet1()
        {
            testTrieRanges("set1", false, setRanges1, checkRanges1);
        }

        [Fact]
        public void TrieTestSet2Overlap()
        {
            testTrieRanges("set2-overlap", false, setRanges2, checkRanges2);
        }

        [Fact]
        public void TrieTestSet3Initial9()
        {
            testTrieRanges("set3-initial-9", false, setRanges3, checkRanges3);
        }

        [Fact]
        public void TrieTestSetEmpty()
        {
            testTrieRanges("set-empty", false, setRangesEmpty, checkRangesEmpty);
        }

        [Fact]
        public void TrieTestSetSingleValue()
        {
            testTrieRanges("set-single-value", false, setRangesSingleValue, checkRangesSingleValue);
        }

        [Fact]
        public void TrieTestSet2OverlapWithClone()
        {
            testTrieRanges("set2-overlap.withClone", true, setRanges2, checkRanges2);
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
            String testName = "free-blocks";

            MutableCodePointTrie mutableTrie;
            int i;

            mutableTrie = new MutableCodePointTrie(1, 0xad);

            /*
             * Repeatedly set overlapping same-value ranges to stress the free-data-block management.
             * If it fails, it will overflow the data array.
             */
            for (i = 0; i < (0x120000 >> 4) / 2; ++i)
            {
                // 4=UCPTRIE_SHIFT_3
                mutableTrie.setRange(0x740, 0x840 - 1, 1);
                mutableTrie.setRange(0x780, 0x880 - 1, 1);
                mutableTrie.setRange(0x740, 0x840 - 1, 2);
                mutableTrie.setRange(0x780, 0x880 - 1, 3);
            }

            /* make blocks that will be free during compaction */
            mutableTrie.setRange(0x1000, 0x3000 - 1, 2);
            mutableTrie.setRange(0x2000, 0x4000 - 1, 3);
            mutableTrie.setRange(0x1000, 0x4000 - 1, 1);

            mutableTrie = testTrieSerializeAllValueWidth(testName, mutableTrie, false, checkRanges);
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
            String testName = "grow-data";

            MutableCodePointTrie mutableTrie;
            int i;

            mutableTrie = new MutableCodePointTrie(1, 0xad);

            /*
             * Use umutablecptrie_set() not umutablecptrie_setRange() to write non-initialValue-data.
             * Should grow/reallocate the data array to a sufficient length.
             */
            for (i = 0; i < 0x1000; ++i)
            {
                mutableTrie.set(i, 2);
            }

            for (i = 0x720; i < 0x1100; ++i)
            {
                /* some overlap */
                mutableTrie.set(i, 3);
            }

            for (i = 0x7a0; i < 0x900; ++i)
            {
                mutableTrie.set(i, 4);
            }

            for (i = 0x8a0; i < 0x110000; ++i)
            {
                mutableTrie.set(i, 5);
            }

            mutableTrie = testTrieSerializeAllValueWidth(testName, mutableTrie, false, checkRanges);
        }

        [Fact]
        public void ManyAllSameBlocksTest()
        {
            String testName = "many-all-same";

            MutableCodePointTrie mutableTrie;
            int i;
            CheckRange[] checkRanges = new CheckRange[(0x110000 >> 12) + 1];

            mutableTrie = new MutableCodePointTrie(0xff33, 0xad);
            checkRanges[0] = new CheckRange(0, 0xff33); // initialValue

            // Many all-same-value blocks.
            for (i = 0; i < 0x110000; i += 0x1000)
            {
                int value = i >> 12;
                mutableTrie.setRange(i, i + 0xfff, value);
                checkRanges[value + 1] = new CheckRange(i + 0x1000, value);
            }

            for (i = 0; i < 0x110000; i += 0x1000)
            {
                int expected = i >> 12;
                int v0 = mutableTrie.get(i);
                int vfff = mutableTrie.get(i + 0xfff);
                if (v0 != expected || vfff != expected)
                {
                    fail($"error: MutableCodePointTrie U+{i:04X} unexpected value\n");
                }
            }

            mutableTrie = testTrieSerializeAllValueWidth(testName, mutableTrie, false, checkRanges);
        }

        [Fact]
        public void MuchDataTest()
        {
            String testName = "much-data";

            MutableCodePointTrie mutableTrie;
            int r, c;
            CheckRange[] checkRanges = new CheckRange[(0x10000 >> 6) + (0x10240 >> 4) + 10];

            mutableTrie = new MutableCodePointTrie(0xff33, 0xad);
            checkRanges[0] = new CheckRange(0, 0xff33); // initialValue
            r = 1;

            // Add much data that does not compact well,
            // to get more than 128k data values after compaction.
            for (c = 0; c < 0x10000; c += 0x40)
            {
                int value = c >> 4;
                mutableTrie.setRange(c, c + 0x3f, value);
                checkRanges[r++] = new CheckRange(c + 0x40, value);
            }

            checkRanges[r++] = new CheckRange(0x20000, 0xff33);
            for (c = 0x20000; c < 0x30230; c += 0x10)
            {
                int value = c >> 4;
                mutableTrie.setRange(c, c + 0xf, value);
                checkRanges[r++] = new CheckRange(c + 0x10, value);
            }

            mutableTrie.setRange(0x30230, 0x30233, 0x3023);
            checkRanges[r++] = new CheckRange(0x30234, 0x3023);
            mutableTrie.setRange(0x30234, 0xdffff, 0x5005);
            checkRanges[r++] = new CheckRange(0xe0000, 0x5005);
            mutableTrie.setRange(0xe0000, 0x10ffff, 0x9009);
            checkRanges[r++] = new CheckRange(0x110000, 0x9009);

            var _checkRanges = new CheckRange[r];
            Array.Copy(checkRanges, _checkRanges, r);
            checkRanges = _checkRanges;
            testBuilder(testName, mutableTrie, checkRanges);
            testTrieSerialize("much-data.16", mutableTrie,
                CodePointTrie.Type.FAST,      CodePointTrie.ValueWidth.BITS_16, false,
                checkRanges);
        }

        private void testGetRangesFixedSurr(String testName, MutableCodePointTrie mutableTrie,
                                            CodePointMap.RangeOption option, CheckRange[] checkRanges)
        {
            testTrieGetRanges(testName, mutableTrie, option, 5, checkRanges);
            MutableCodePointTrie clone = (MutableCodePointTrie) mutableTrie.Clone();
            CodePointTrie trie =
                clone.buildImmutable(CodePointTrie.Type.FAST, CodePointTrie.ValueWidth.BITS_16);
            testTrieGetRanges(testName, trie, option, 5, checkRanges);
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

            MutableCodePointTrie mutableTrie = makeTrieWithRanges(
                "fixedSurr", false, setRangesFixedSurr, checkRangesFixedLeadSurr1);
            testGetRangesFixedSurr("fixedLeadSurr1",            mutableTrie,
                CodePointMap.RangeOption.FIXED_LEAD_SURROGATES, checkRangesFixedLeadSurr1);
            testGetRangesFixedSurr("fixedAllSurr1",            mutableTrie,
                CodePointMap.RangeOption.FIXED_ALL_SURROGATES, checkRangesFixedAllSurr1);
            // Setting a range in the middle of lead surrogates makes no difference.
            mutableTrie.setRange(0xd844, 0xd899, 5);
            testGetRangesFixedSurr("fixedLeadSurr2",            mutableTrie,
                CodePointMap.RangeOption.FIXED_LEAD_SURROGATES, checkRangesFixedLeadSurr1);
            // Bridge the gap before the lead surrogates.
            mutableTrie.set(0xd7ff, 5);
            testGetRangesFixedSurr("fixedLeadSurr3",            mutableTrie,
                CodePointMap.RangeOption.FIXED_LEAD_SURROGATES, checkRangesFixedLeadSurr3);
            testGetRangesFixedSurr("fixedAllSurr3",            mutableTrie,
                CodePointMap.RangeOption.FIXED_ALL_SURROGATES, checkRangesFixedAllSurr3);
            // Bridge the gap after the trail surrogates.
            mutableTrie.set(0xe000, 5);
            testGetRangesFixedSurr("fixedSurr4",               mutableTrie,
                CodePointMap.RangeOption.FIXED_ALL_SURROGATES, checkRangesFixedSurr4);
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
                // First small null data block in a small-type trie.
                // In a fast-type trie, it is ok to match a small null data block at U+1041
                // but not at U+1040.
                new SetRange(0x1051, 0x10000, 3),
                // No fast data block (block length 64) filled with 0 regardless of trie type.
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

            testTrieRanges("small0-in-fast", false, setRanges, checkRanges);
        }

        [Fact]
        public void ShortAllSameBlocksTest()
        {
            // Many all-same-value blocks but only of the small block length used in the mutable trie.
            // The builder code needs to turn a group of short ALL_SAME blocks below fastLimit
            // into a MIXED block, and reserve data array capacity for that.
            MutableCodePointTrie mutableTrie = new MutableCodePointTrie(0, 0xad);
            CheckRange[] checkRanges = new CheckRange[0x101];
            for (int i = 0; i < 0x1000; i += 0x10)
            {
                int value = i >> 4;
                mutableTrie.setRange(i, i + 0xf, value);
                checkRanges[value] = new CheckRange(i + 0x10, value);
            }

            checkRanges[0x100] = new CheckRange(0x110000, 0);

            mutableTrie = testTrieSerializeAllValueWidth(
                "short-all-same", mutableTrie, false, checkRanges);
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