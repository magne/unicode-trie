using Xunit.Sdk;

namespace CodeHive.unicode_trie.tests
{
    public abstract class AbstractTestLog
    {
        /**
         * Report an error.
         */
        public static void err(string message)
        {
            throw new XunitException(message);
            //msg(message, ERR, true, false);
        }

        /**
         * Report an error and newline.
         */
        public static void errln(string message)
        {
            throw new XunitException(message);
            //msg(message, ERR, true, true);
        }
    }
}