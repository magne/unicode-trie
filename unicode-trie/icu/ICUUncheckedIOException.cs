using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.icu
{
    internal class ICUUncheckedIOException : Exception
    {
        internal ICUUncheckedIOException(string message)
            : base(message)
        { }
    }
}