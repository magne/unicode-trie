namespace CodeHive.unicode_trie.icu
{
    internal static class UTF16
    {
        /// <summary>
        /// Is this code point a lead surrogate (U+d800..U+dbff)?
        /// </summary>
        /// <param name="c">code unit or code point</param>
        /// <returns>true or false</returns>
        internal static bool IsLeadSurrogate(int c)
        {
            return (c & 0xfffffc00) == 0xd800;
        }

        /// <summary>
        /// Is this code point a trail surrogate (U+dc00..U0dfff)?
        /// </summary>
        /// <param name="c">code unit or code point</param>
        /// <returns>true or false</returns>
        internal static bool IsTrailSurrogate(int c)
        {
            return (c & 0xfffffc00) == 0xdc00;
        }

        /// <summary>
        /// Is this code point a surrogate (U+d800..U+dfff)?
        /// </summary>
        /// <param name="c">code unit or code point</param>
        /// <returns>true or false</returns>
        internal static bool IsSurrogate(int c)
        {
            return (c & 0xfffff800) == 0xd800;
        }

        /// <summary>
        /// Assuming c is a surrogate code point (UTF16.isSurrogate(c)), is it a lead surrogate?
        /// </summary>
        /// <param name="c">code unit or code point</param>
        /// <returns>true or false</returns>
        internal static bool IsSurrogateLead(int c)
        {
            return (c & 0x400) == 0;
        }
    }
}