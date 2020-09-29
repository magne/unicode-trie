using Xunit.Sdk;

namespace CodeHive.unicode_trie.tests
{
    public abstract class AbstractTestLog
    {
        /**
         * Report an error.
         */
        public static void Err(string message)
        {
            throw new XunitException(message);
            //msg(message, ERR, true, false);
        }

        /**
         * Report an error and newline.
         */
        public static void ErrLn(string message)
        {
            throw new XunitException(message);
            //msg(message, ERR, true, true);
        }
    }
}