using System.IO;

namespace unicode_trie.java
{
    internal class FilterOutputStream
    {
        internal readonly Stream os;

        protected FilterOutputStream(Stream os)
        {
            this.os = os;
        }
    }
}