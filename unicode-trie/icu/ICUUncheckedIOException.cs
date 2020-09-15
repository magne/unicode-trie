using System;

// ReSharper disable InconsistentNaming

namespace unicode_trie.icu
{
    internal class ICUUncheckedIOException : Exception
    {
        public ICUUncheckedIOException()
        { }

        public ICUUncheckedIOException(string message)
            : base(message)
        { }

        public ICUUncheckedIOException(Exception exception)
            : base(null, exception)
        { }
    }
}