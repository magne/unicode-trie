namespace CodeHive.unicode_trie.icu
{
    /**
     * Low-level implementation of the Unicode Normalization Algorithm.
     * For the data structure and details see the documentation at the end of
     * C++ normalizer2impl.h and in the design doc at
     * http://site.icu-project.org/design/normalization/custom
     */
    internal static class Normalizer2Impl
    {
        // TODO: Propose as public API on the UTF16 class.
        // TODO: Propose widening UTF16 methods that take char to take int.
        // TODO: Propose widening UTF16 methods that take String to take CharSequence.
        internal static class UTF16Plus
        {
            /**
             * Is this code point a lead surrogate (U+d800..U+dbff)?
             * @param c code unit or code point
             * @return true or false
             */
            public static bool isLeadSurrogate(int c)
            {
                return (c & 0xfffffc00) == 0xd800;
            }

            /**
             * Is this code point a trail surrogate (U+dc00..U+dfff)?
             * @param c code unit or code point
             * @return true or false
             */
            public static bool isTrailSurrogate(int c)
            {
                return (c & 0xfffffc00) == 0xdc00;
            }

            /**
             * Is this code point a surrogate (U+d800..U+dfff)?
             * @param c code unit or code point
             * @return true or false
             */
            public static bool isSurrogate(int c)
            {
                return (c & 0xfffff800) == 0xd800;
            }

            /**
             * Assuming c is a surrogate code point (UTF16.isSurrogate(c)),
             * is it a lead surrogate?
             * @param c code unit or code point
             * @return true or false
             */
            public static bool isSurrogateLead(int c)
            {
                return (c & 0x400) == 0;
            }
        }
    }
}