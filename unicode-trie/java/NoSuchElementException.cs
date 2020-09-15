using System;

namespace unicode_trie.java
{
    internal class NoSuchElementException : Exception
    {
        public NoSuchElementException()
        { }

        public NoSuchElementException(string message)
            : base(message)
        { }
    }
}