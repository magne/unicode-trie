using System.IO;

namespace CodeHive.unicode_trie.java
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