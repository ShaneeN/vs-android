using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace vs_android.Build.CPPTasks.Android
{
    [Serializable]
    internal sealed class InternalErrorException : Exception
    {
        // Methods
        internal InternalErrorException()
        {
        }

        internal InternalErrorException(string message)
            : base("MSB0001: Internal MSBuild Error: " + message)
        {
            ConsiderDebuggerLaunch(message, null);
        }

        private InternalErrorException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        internal InternalErrorException(string message, Exception innerException)
            : base("MSB0001: Internal MSBuild Error: " + message + ((innerException == null) ? string.Empty : ("\n=============\n" + innerException.ToString() + "\n\n")), innerException)
        {
            ConsiderDebuggerLaunch(message, innerException);
        }

        private static void ConsiderDebuggerLaunch(string message, Exception innerException)
        {
            if (innerException != null)
            {
                innerException.ToString();
            }
            if (Environment.GetEnvironmentVariable("MSBUILDLAUNCHDEBUGGER") != null)
            {
                Debugger.Launch();
            }
        }
    }

}
