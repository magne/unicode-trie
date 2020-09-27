using BenchmarkDotNet.Running;

namespace CodeHive.unicode_trie.bench
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}