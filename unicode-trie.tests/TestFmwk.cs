using System;
using System.Diagnostics;

namespace unicode_trie.tests
{
    public class TestFmwk : AbstractTestLog
    {
        protected static void fail()
        {
            fail("");
        }

        protected static void fail(string message)
        {
            if (message == null)
            {
                message = "";
            }

            if (!message.Equals(""))
            {
                message = ": " + message;
            }

            errln(sourceLocation() + message);
        }

        // Return the source code location of the caller located callDepth frames up the stack.
        protected static string sourceLocation()
        {
            // Walk up the stack to the first call site outside this file
            foreach (var sf in new StackTrace(true).GetFrames())
            {
                var source = sf.GetFileName();
                if (source != null && !source.Equals("TestFmwk.cs") && !source.Equals("AbstractTestLog.cs"))
                {
                    var methodName = sf.GetMethod().Name;
                    if (methodName != null &&
                        (methodName.StartsWith("Test") || methodName.StartsWith("test") || methodName.Equals("main")))
                    {
                        return "(" + source + ":" + sf.GetFileLineNumber() + ") ";
                    }
                }
            }

            throw new InternalError();
        }

        private class InternalError : Exception
        { }
    }
}