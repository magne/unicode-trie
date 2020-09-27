using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CodeHive.unicode_trie.tests;

namespace CodeHive.unicode_trie.bench
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    public class UnitTests
    {
        private readonly CodePointTrieTest unitTests;

        public UnitTests()
        {
            unitTests = new CodePointTrieTest();
        }

        [Benchmark]
        public void TrieTestSet1() => unitTests.TrieTestSet1();

        [Benchmark]
        public void TrieTestSet2Overlap() => unitTests.TrieTestSet2Overlap();

        [Benchmark]
        public void TrieTestSet3Initial9() => unitTests.TrieTestSet3Initial9();

        [Benchmark]
        public void TrieTestSetEmpty() => unitTests.TrieTestSetEmpty();

        [Benchmark]
        public void TrieTestSetSingleValue() => unitTests.TrieTestSetSingleValue();

        [Benchmark]
        public void TrieTestSet2OverlapWithClone() => unitTests.TrieTestSet2OverlapWithClone();

        [Benchmark]
        public void FreeBlocksTest() => unitTests.FreeBlocksTest();

        [Benchmark]
        public void GrowDataArrayTest() => unitTests.GrowDataArrayTest();

        [Benchmark]
        public void ManyAllSameBlocksTest() => unitTests.ManyAllSameBlocksTest();

        [Benchmark]
        public void MuchDataTest() => unitTests.MuchDataTest();

        [Benchmark]
        public void TrieTestGetRangesFixedSurr() => unitTests.TrieTestGetRangesFixedSurr();

        [Benchmark]
        public void TestSmallNullBlockMatchesFast() => unitTests.TestSmallNullBlockMatchesFast();

        [Benchmark]
        public void ShortAllSameBlocksTest() => unitTests.ShortAllSameBlocksTest();
    }
}