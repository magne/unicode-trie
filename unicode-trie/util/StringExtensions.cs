namespace CodeHive.unicode_trie.util
{
    internal static class StringExtensions
    {
        internal static byte[] ToByteArray(this string str)
        {
            var chars = str.ToCharArray();
            var bytes = new byte[chars.Length << 1];
            for (var i = 0; i < chars.Length; ++i)
            {
                var ch = chars[i];
                bytes[i << 1] = (byte) (ch >> 8);
                bytes[(i << 1) + 1] = (byte) (ch & 0xff);
            }

            return bytes;
        }
    }
}