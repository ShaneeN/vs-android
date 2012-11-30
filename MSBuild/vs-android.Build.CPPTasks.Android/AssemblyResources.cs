using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime;
using System.Text;

namespace vs_android.Build.CPPTasks.Android
{
    internal static class AssemblyResources
    {
        // Fields
        private static readonly ResourceManager resources = new ResourceManager("Microsoft.Build.Utilities.Strings", Assembly.GetExecutingAssembly());
        private static readonly ResourceManager sharedResources = new ResourceManager("Microsoft.Build.Utilities.Strings.shared", Assembly.GetExecutingAssembly());

        // Methods
        internal static string FormatResourceString(string resourceName, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(resourceName, "resourceName");
            return FormatString(GetString(resourceName), args);
        }

        internal static string FormatString(string unformatted, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(unformatted, "unformatted");
            return ResourceUtilities.FormatString(unformatted, args);
        }

        internal static string GetString(string name)
        {
            string str = resources.GetString(name, CultureInfo.CurrentUICulture);
            if (str == null)
            {
                str = sharedResources.GetString(name, CultureInfo.CurrentUICulture);
            }
            ErrorUtilities.VerifyThrow(str != null, "Missing resource '{0}'", name);
            return str;
        }

        // Properties
        internal static ResourceManager PrimaryResources
        {
            [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
            get
            {
                return resources;
            }
        }

        internal static ResourceManager SharedResources
        {
            [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
            get
            {
                return sharedResources;
            }
        }
    }

}