namespace CodeHive.unicode_trie.tests.java
{
    internal static class StringLatin1
    {
        internal static bool CanEncode(int cp)
        {
            return (int) ((uint) cp >> 8) == 0;
        }
    }
}