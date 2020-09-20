// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CodeHive.unicode_trie.java
{
    public interface Iterator<E>
    {
        bool hasNext();

        E next();

        void remove()
        {
            throw new UnsupportedOperationException("remove");
        }
    }
}