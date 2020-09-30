// ReSharper disable InconsistentNaming

using CodeHive.unicode_trie.java;

namespace CodeHive.unicode_trie.tests.java
{
    internal static class StringLatin1
    {
        internal static bool canEncode(int cp)
        {
            return (int) ((uint) cp >> 8) == 0;
        }
    }
}