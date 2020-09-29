using System;
using System.Diagnostics;

namespace CodeHive.unicode_trie.tests
{
    public class TestFmwk : AbstractTestLog
    {
        protected static void Fail()
        {
            Fail("");
        }

        protected static void Fail(string message)
        {
            if (message == null)
            {
                message = "";
            }

            if (!message.Equals(""))
            {
                message = ": " + message;
            }

            ErrLn(SourceLocation() + message);
        }

        // Return the source code location of the caller located callDepth frames up the stack.
        protected static string SourceLocation()
        {
            // Walk up the stack to the first call site outside this file
            foreach (var sf in new StackTrace(true).GetFrames())
            {
                var source = sf?.GetFileName();
                if (source != null && !source.Equals("TestFmwk.cs") && !source.Equals("AbstractTestLog.cs"))
                {
                    var methodName = sf.GetMethod()?.Name;
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