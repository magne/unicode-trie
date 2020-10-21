using System;

namespace CodeHive.unicode_trie.icu
{
    internal class ICUUncheckedIOException : Exception
    {
        internal ICUUncheckedIOException(string message)
            : base(message)
        { }
    }
}