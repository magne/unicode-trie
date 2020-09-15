using System;

namespace unicode_trie.java
{
    internal class UnsupportedOperationException : Exception
    {
        public UnsupportedOperationException()
        { }

        public UnsupportedOperationException(string message)
            : base(message)
        { }
    }
}