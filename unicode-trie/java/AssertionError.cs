using System;

namespace CodeHive.unicode_trie.java
{
    internal class AssertionError : Exception
    {
        public AssertionError()
        { }

        public AssertionError(string message)
            : base(message)
        { }
    }
}