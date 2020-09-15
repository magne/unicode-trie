using System;
// ReSharper disable InconsistentNaming

namespace unicode_trie.java
{
    public static class Objects
    {
        public static int checkFromIndexSize(int fromIndex, int size, int length)
        {
            if ((length | fromIndex | size) >= 0 && size <= length - fromIndex)
            {
                return fromIndex;
            }
            else
            {
                throw new Exception();
            }
        }
    }
}