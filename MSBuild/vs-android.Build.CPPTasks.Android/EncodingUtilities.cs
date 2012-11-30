using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vs_android.Build.CPPTasks.Android
{
    internal static class EncodingUtilities
    {
        // Fields
        private static Encoding currentOemEncoding;

        // Properties
        internal static Encoding CurrentSystemOemEncoding
        {
            get
            {
                if (currentOemEncoding == null)
                {
                    currentOemEncoding = Encoding.Default;
                    try
                    {
                        currentOemEncoding = Encoding.GetEncoding(NativeMethodsShared.GetOEMCP());
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
                return currentOemEncoding;
            }
        }
    }

}