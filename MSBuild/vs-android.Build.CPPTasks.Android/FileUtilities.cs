using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;

namespace vs_android.Build.CPPTasks.Android
{
    class FileUtilities
    {
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        internal static string GetTemporaryFile(string extension)
        {
            return GetTemporaryFile(null, extension);
        }

 


        internal static string GetTemporaryFile(string directory, string extension)
        {
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(directory, "directory");
            ErrorUtilities.VerifyThrowArgumentLength(extension, "extension");
            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }
            string path = null;
            try
            {
                directory = directory ?? Path.GetTempPath();
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                path = Path.Combine(directory, "tmp" + Guid.NewGuid().ToString("N") + extension);
                ErrorUtilities.VerifyThrow(!File.Exists(path), "Guid should be unique");
                File.WriteAllText(path, string.Empty);
            }
            catch (Exception exception)
            {
                if (ExceptionHandling.NotExpectedException(exception))
                {
                    throw;
                }
                throw new IOException(ResourceUtilities.FormatResourceString("Shared.FailedCreatingTempFile", new object[] { exception.Message }), exception);
            }
            return path;
        }


    }
}
