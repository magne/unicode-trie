namespace CodeHive.unicode_trie.tests.icu
{
    internal static class Utility
    {
        /**
         * Convert a char to 4 hex uppercase digits.  E.g., hex('a') =>
         * "0041".
         */
        internal static string Hex(long ch)
        {
            return Hex(ch, 4);
        }

        /**
         * Supplies a zero-padded hex representation of an integer (without 0x)
         */
        private static string Hex(long i, int places)
        {
            if (i == long.MinValue)
                return "-8000000000000000";

            var negative = i < 0;
            if (negative)
            {
                i = -i;
            }

            string result = i.ToString("X");
            if (result.Length < places)
            {
                result = "0000000000000000".Substring(result.Length, places) + result;
            }

            if (negative)
            {
                return '-' + result;
            }

            return result;
        }
    }
}