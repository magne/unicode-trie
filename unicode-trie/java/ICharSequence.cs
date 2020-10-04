namespace CodeHive.unicode_trie.java
{
    internal interface ICharSequence
    {
        int Length { get; }

        char CharAt(in int index);
    }
}