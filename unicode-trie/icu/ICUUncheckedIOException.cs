using System;

// ReSharper disable InconsistentNaming

namespace CodeHive.unicode_trie.icu
{
    internal class ICUUncheckedIOException : Exception
    {
        public ICUUncheckedIOException(string message)
            : base(message)
        { }

        public ICUUncheckedIOException(Exception exception)
            : base(null, exception)
        { }
    }
}