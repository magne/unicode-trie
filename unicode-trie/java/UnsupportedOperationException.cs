using System;

namespace CodeHive.unicode_trie.java
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