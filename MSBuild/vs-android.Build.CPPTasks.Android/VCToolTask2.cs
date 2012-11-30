/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.CPPTasks;
using Microsoft.Build.Utilities;
using System.Threading;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime;

namespace vs_android.Build.CPPTasks.Android
{
    public abstract class VCToolTask : ToolTask2
    {
        // Fields
        private string[] acceptableNonZeroExitCodes;
        private Dictionary<string, ToolSwitch> activeToolSwitches;
        private Dictionary<string, ToolSwitch> activeToolSwitchesValues;
        private string additionalOptions;
        private IntPtr cancelEvent;
        private string cancelEventName;
        protected TaskLoggingHelper logPrivate;
        private char prefix;
        private Dictionary<string, Dictionary<string, string>> values;

        // Methods
        protected VCToolTask(ResourceManager taskResources)
            : base(taskResources)
        {
            this.activeToolSwitchesValues = new Dictionary<string, ToolSwitch>();
            this.activeToolSwitches = new Dictionary<string, ToolSwitch>(StringComparer.OrdinalIgnoreCase);
            this.values = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            this.additionalOptions = string.Empty;
            this.prefix = '/';
            this.cancelEventName = "MSBuildConsole_CancelEvent" + Guid.NewGuid().ToString("N");
            this.cancelEvent = VCTaskNativeMethods.CreateEventW(IntPtr.Zero, false, false, this.cancelEventName);
            this.logPrivate = new TaskLoggingHelper(this);
            this.logPrivate.TaskResources = AssemblyResources.PrimaryResources;
            this.logPrivate.HelpKeywordPrefix = "MSBuild.";
        }

        protected void AddActiveSwitchToolValue(ToolSwitch switchToAdd)
        {
            if ((switchToAdd.Type != ToolSwitchType.Boolean) || switchToAdd.BooleanValue)
            {
                if (switchToAdd.SwitchValue != string.Empty)
                {
                    this.ActiveToolSwitchesValues.Add(switchToAdd.SwitchValue, switchToAdd);
                }
            }
            else if (switchToAdd.ReverseSwitchValue != string.Empty)
            {
                this.ActiveToolSwitchesValues.Add(switchToAdd.ReverseSwitchValue, switchToAdd);
            }
        }

        protected virtual void AddDefaultsToActiveSwitchList()
        {
        }

        protected virtual void AddFallbacksToActiveSwitchList()
        {
        }

        protected void BuildAdditionalArgs(CommandLineBuilder cmdLine)
        {
            if ((cmdLine != null) && !string.IsNullOrEmpty(this.additionalOptions))
            {
                cmdLine.AppendSwitch(Environment.ExpandEnvironmentVariables(this.additionalOptions));
            }
        }

        public override void Cancel()
        {
            VCTaskNativeMethods.SetEvent(this.cancelEvent);
        }

        private static void EmitAlwaysAppendSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            clb.AppendSwitch(toolSwitch.Name);
        }

        private void EmitBooleanSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (toolSwitch.BooleanValue)
            {
                if (!string.IsNullOrEmpty(toolSwitch.SwitchValue))
                {
                    StringBuilder builder = new StringBuilder(this.GetEffectiveArgumentsValues(toolSwitch));
                    builder.Insert(0, toolSwitch.Separator);
                    builder.Insert(0, toolSwitch.TrueSuffix);
                    builder.Insert(0, toolSwitch.SwitchValue);
                    clb.AppendSwitch(builder.ToString());
                }
            }
            else
            {
                this.EmitReversibleBooleanSwitch(clb, toolSwitch);
            }
        }

        private static void EmitDirectorySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!string.IsNullOrEmpty(toolSwitch.SwitchValue))
            {
                clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Separator);
            }
        }

        private static void EmitFileSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!string.IsNullOrEmpty(toolSwitch.Value))
            {
                string parameter = Environment.ExpandEnvironmentVariables(toolSwitch.Value);
                parameter.Trim();
                if (!parameter.StartsWith("\""))
                {
                    parameter = "\"" + parameter;
                    if (parameter.EndsWith(@"\") && !parameter.EndsWith(@"\\"))
                    {
                        parameter = parameter + "\\\"";
                    }
                    else
                    {
                        parameter = parameter + "\"";
                    }
                }
                clb.AppendSwitchUnquotedIfNotNull(toolSwitch.SwitchValue + toolSwitch.Separator, parameter);
            }
        }

        private void EmitIntegerSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (toolSwitch.IsValid)
            {
                if (!string.IsNullOrEmpty(toolSwitch.Separator))
                {
                    clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Separator + toolSwitch.Number.ToString() + this.GetEffectiveArgumentsValues(toolSwitch));
                }
                else
                {
                    clb.AppendSwitch(toolSwitch.SwitchValue + toolSwitch.Number.ToString() + this.GetEffectiveArgumentsValues(toolSwitch));
                }
            }
        }

        private void EmitReversibleBooleanSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!string.IsNullOrEmpty(toolSwitch.ReverseSwitchValue))
            {
                string str = toolSwitch.BooleanValue ? toolSwitch.TrueSuffix : toolSwitch.FalseSuffix;
                StringBuilder builder = new StringBuilder(this.GetEffectiveArgumentsValues(toolSwitch));
                builder.Insert(0, str);
                builder.Insert(0, toolSwitch.Separator);
                builder.Insert(0, toolSwitch.TrueSuffix);
                builder.Insert(0, toolSwitch.ReverseSwitchValue);
                clb.AppendSwitch(builder.ToString());
            }
        }

        private static void EmitStringArraySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            string[] parameters = new string[toolSwitch.StringList.Length];
            for (int i = 0; i < toolSwitch.StringList.Length; i++)
            {
                if (toolSwitch.StringList[i].StartsWith("\"") && toolSwitch.StringList[i].EndsWith("\""))
                {
                    parameters[i] = Environment.ExpandEnvironmentVariables(toolSwitch.StringList[i].Substring(1, toolSwitch.StringList[i].Length - 2));
                }
                else
                {
                    parameters[i] = Environment.ExpandEnvironmentVariables(toolSwitch.StringList[i]);
                }
            }
            if (string.IsNullOrEmpty(toolSwitch.Separator))
            {
                foreach (string str in parameters)
                {
                    clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, str);
                }
            }
            else
            {
                clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, parameters, toolSwitch.Separator);
            }
        }

        private void EmitStringSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            string switchName = string.Empty + toolSwitch.SwitchValue + toolSwitch.Separator;
            StringBuilder builder = new StringBuilder(this.GetEffectiveArgumentsValues(toolSwitch));
            string str2 = toolSwitch.Value;
            if (!toolSwitch.MultiValues)
            {
                str2.Trim();
                if (!str2.StartsWith("\""))
                {
                    str2 = "\"" + str2;
                    if (str2.EndsWith(@"\") && !str2.EndsWith(@"\\"))
                    {
                        str2 = str2 + "\\\"";
                    }
                    else
                    {
                        str2 = str2 + "\"";
                    }
                }
                builder.Insert(0, str2);
            }
            if ((switchName.Length != 0) || (builder.ToString().Length != 0))
            {
                clb.AppendSwitchUnquotedIfNotNull(switchName, builder.ToString());
            }
        }

        private static void EmitTaskItemArraySwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (string.IsNullOrEmpty(toolSwitch.Separator))
            {
                foreach (ITaskItem item in toolSwitch.TaskItemArray)
                {
                    clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, Environment.ExpandEnvironmentVariables(item.ItemSpec));
                }
            }
            else
            {
                ITaskItem[] parameters = (ITaskItem[])toolSwitch.TaskItemArray.Clone();
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i].ItemSpec = Environment.ExpandEnvironmentVariables(parameters[i].ItemSpec);
                }
                clb.AppendSwitchIfNotNull(toolSwitch.SwitchValue, parameters, toolSwitch.Separator);
            }
        }

        private static void EmitTaskItemSwitch(CommandLineBuilder clb, ToolSwitch toolSwitch)
        {
            if (!string.IsNullOrEmpty(toolSwitch.Name))
            {
                clb.AppendSwitch(Environment.ExpandEnvironmentVariables(toolSwitch.Name + toolSwitch.Separator));
            }
        }

        protected static string EnsureTrailingSlash(string directoryName)
        {
            ErrorUtilities.VerifyThrow(directoryName != null, "InternalError");
            if (!string.IsNullOrEmpty(directoryName))
            {
                char ch = directoryName[directoryName.Length - 1];
                if ((ch != Path.DirectorySeparatorChar) && (ch != Path.AltDirectorySeparatorChar))
                {
                    directoryName = directoryName + Path.DirectorySeparatorChar;
                }
            }
            return directoryName;
        }

        public override bool Execute()
        {
            bool flag = base.Execute();
            VCTaskNativeMethods.CloseHandle(this.cancelEvent);
            return flag;
        }

        protected internal string GenerateCommandLine()
        {
            string str = this.GenerateCommandLineCommands();
            string str2 = this.GenerateResponseFileCommands();
            if (!string.IsNullOrEmpty(str))
            {
                return (str + " " + str2);
            }
            return str2;
        }

        protected string GenerateCommandLineExceptSwitches(string[] switchesToRemove)
        {
            string str = this.GenerateCommandLineCommands();
            string str2 = this.GenerateResponseFileCommandsExceptSwitches(switchesToRemove);
            if (!string.IsNullOrEmpty(str))
            {
                return (str + " " + str2);
            }
            return str2;
        }

        protected void GenerateCommandsAccordingToType(CommandLineBuilder clb, ToolSwitch toolSwitch, bool bRecursive)
        {
            if ((toolSwitch.Parents.Count <= 0) || bRecursive)
            {
                switch (toolSwitch.Type)
                {
                    case ToolSwitchType.Boolean:
                        this.EmitBooleanSwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.Integer:
                        this.EmitIntegerSwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.String:
                        this.EmitStringSwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.StringArray:
                        EmitStringArraySwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.File:
                        EmitFileSwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.Directory:
                        EmitDirectorySwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.ITaskItem:
                        EmitTaskItemSwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.ITaskItemArray:
                        EmitTaskItemArraySwitch(clb, toolSwitch);
                        return;

                    case ToolSwitchType.AlwaysAppend:
                        EmitAlwaysAppendSwitch(clb, toolSwitch);
                        return;
                }
                ErrorUtilities.VerifyThrow(false, "InternalError");
            }
        }

        protected override string GenerateFullPathToTool()
        {
            return this.ToolName;
        }

        protected override string GenerateResponseFileCommands()
        {
            return this.GenerateResponseFileCommandsExceptSwitches(new string[0]);
        }

        protected string GenerateResponseFileCommandsExceptSwitches(string[] switchesToRemove)
        {
            this.AddDefaultsToActiveSwitchList();
            this.AddFallbacksToActiveSwitchList();
            this.PostProcessSwitchList();
            CommandLineBuilder clb = new CommandLineBuilder(true);
            foreach (string str in this.SwitchOrderList)
            {
                if (this.IsPropertySet(str))
                {
                    ToolSwitch property = this.activeToolSwitches[str];
                    if (!this.VerifyDependenciesArePresent(property) || !this.VerifyRequiredArgumentsArePresent(property, false))
                    {
                        continue;
                    }
                    bool flag = true;
                    if (switchesToRemove != null)
                    {
                        foreach (string str2 in switchesToRemove)
                        {
                            if (str.Equals(str2, StringComparison.OrdinalIgnoreCase))
                            {
                                flag = false;
                                break;
                            }
                        }
                    }
                    if (flag)
                    {
                        this.GenerateCommandsAccordingToType(clb, property, false);
                    }
                    continue;
                }
                if (string.Equals(str, "AlwaysAppend", StringComparison.OrdinalIgnoreCase))
                {
                    clb.AppendSwitch(this.AlwaysAppend);
                }
            }
            this.BuildAdditionalArgs(clb);
            return clb.ToString();
        }

        protected string GetEffectiveArgumentsValues(ToolSwitch property)
        {
            StringBuilder builder = new StringBuilder();
            bool flag = false;
            string argument = string.Empty;
            if (property.ArgumentRelationList != null)
            {
                foreach (ArgumentRelation relation in property.ArgumentRelationList)
                {
                    if ((argument != string.Empty) && (argument != relation.argument))
                    {
                        flag = true;
                    }
                    argument = relation.argument;
                    if ((((property.Value == relation.value) || (relation.value == string.Empty)) || ((property.Type == ToolSwitchType.Boolean) && property.BooleanValue)) && this.HasSwitch(relation.argument))
                    {
                        ToolSwitch toolSwitch = this.ActiveToolSwitches[relation.argument];
                        builder.Append(relation.separator);
                        CommandLineBuilder clb = new CommandLineBuilder();
                        this.GenerateCommandsAccordingToType(clb, toolSwitch, true);
                        builder.Append(clb.ToString());
                    }
                }
            }
            CommandLineBuilder builder3 = new CommandLineBuilder();
            if (flag)
            {
                builder3.AppendSwitchIfNotNull("", builder.ToString());
            }
            else
            {
                builder3.AppendSwitchUnquotedIfNotNull("", builder.ToString());
            }
            return builder3.ToString();
        }

        protected override bool HandleTaskExecutionErrors()
        {
            return (this.IsAcceptableReturnValue() || base.HandleTaskExecutionErrors());
        }

        protected bool HasSwitch(string propertyName)
        {
            return (this.IsPropertySet(propertyName) && !string.IsNullOrEmpty(this.activeToolSwitches[propertyName].Name));
        }

        protected bool IsAcceptableReturnValue()
        {
            if (this.AcceptableNonZeroExitCodes != null)
            {
                foreach (string str in this.AcceptableNonZeroExitCodes)
                {
                    if (base.ExitCode == Convert.ToInt32(str, CultureInfo.InvariantCulture))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected bool IsExplicitlySetToFalse(string propertyName)
        {
            return (this.activeToolSwitches.ContainsKey(propertyName) && !this.activeToolSwitches[propertyName].BooleanValue);
        }

        protected bool IsPropertySet(string propertyName)
        {
            return (!string.IsNullOrEmpty(propertyName) && this.activeToolSwitches.ContainsKey(propertyName));
        }

        protected bool IsSetToTrue(string propertyName)
        {
            return (this.activeToolSwitches.ContainsKey(propertyName) && this.activeToolSwitches[propertyName].BooleanValue);
        }

        protected bool IsSwitchValueSet(string switchValue)
        {
            return (!string.IsNullOrEmpty(switchValue) && this.ActiveToolSwitchesValues.ContainsKey("/" + switchValue));
        }

        protected virtual void PostProcessSwitchList()
        {
            this.ValidateRelations();
            this.ValidateOverrides();
        }

        private string Prefix(string toolSwitch)
        {
            if (!string.IsNullOrEmpty(toolSwitch) && (toolSwitch[0] != this.prefix))
            {
                return (this.prefix + toolSwitch);
            }
            return toolSwitch;
        }

        protected string ReadSwitchMap(string propertyName, string[][] switchMap, string value)
        {
            if (switchMap != null)
            {
                for (int i = 0; i < switchMap.Length; i++)
                {
                    if (string.Equals(switchMap[i][0], value, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return switchMap[i][1];
                    }
                }
                this.logPrivate.LogErrorFromResources("ArgumentOutOfRange", new object[] { propertyName, value });
            }
            return string.Empty;
        }

        protected void RemoveSwitchToolBasedOnValue(string switchValue)
        {
            if ((this.ActiveToolSwitchesValues.Count > 0) && this.ActiveToolSwitchesValues.ContainsKey("/" + switchValue))
            {
                ToolSwitch switch2 = this.ActiveToolSwitchesValues["/" + switchValue];
                if (switch2 != null)
                {
                    this.ActiveToolSwitches.Remove(switch2.Name);
                }
            }
        }

        protected bool ValidateInteger(string switchName, int min, int max, int value)
        {
            if ((value >= min) && (value <= max))
            {
                return true;
            }
            this.logPrivate.LogErrorFromResources("ArgumentOutOfRange", new object[] { switchName, value });
            return false;
        }

        protected virtual void ValidateOverrides()
        {
            List<string> list = new List<string>();
            foreach (KeyValuePair<string, ToolSwitch> pair in this.ActiveToolSwitches)
            {
                foreach (KeyValuePair<string, string> pair2 in pair.Value.Overrides)
                {
                    if (string.Equals(pair2.Key, ((pair.Value.Type == ToolSwitchType.Boolean) && !pair.Value.BooleanValue) ? pair.Value.ReverseSwitchValue.TrimStart(new char[] { '/' }) : pair.Value.SwitchValue.TrimStart(new char[] { '/' }), StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (KeyValuePair<string, ToolSwitch> pair3 in this.ActiveToolSwitches)
                        {
                            if (!string.Equals(pair3.Key, pair.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.Equals(pair3.Value.SwitchValue.TrimStart(new char[] { '/' }), pair2.Value, StringComparison.OrdinalIgnoreCase))
                                {
                                    list.Add(pair3.Key);
                                    break;
                                }
                                if (((pair3.Value.Type == ToolSwitchType.Boolean) && !pair3.Value.BooleanValue) && string.Equals(pair3.Value.ReverseSwitchValue.TrimStart(new char[] { '/' }), pair2.Value, StringComparison.OrdinalIgnoreCase))
                                {
                                    list.Add(pair3.Key);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            foreach (string str in list)
            {
                this.ActiveToolSwitches.Remove(str);
            }
        }

        protected internal override bool ValidateParameters()
        {
            return (!this.logPrivate.HasLoggedErrors && !base.Log.HasLoggedErrors);
        }

        protected virtual void ValidateRelations()
        {
        }

        protected virtual bool VerifyDependenciesArePresent(ToolSwitch property)
        {
            if (property.Parents.Count <= 0)
            {
                return true;
            }
            bool flag = false;
            foreach (string str in property.Parents)
            {
                flag = flag || this.HasSwitch(str);
            }
            return flag;
        }

        protected bool VerifyRequiredArgumentsArePresent(ToolSwitch property, bool bThrowOnError)
        {
            if (property.ArgumentRelationList != null)
            {
                foreach (ArgumentRelation relation in property.ArgumentRelationList)
                {
                    if ((relation.required && ((property.Value == relation.value) || (relation.value == string.Empty))) && !this.HasSwitch(relation.argument))
                    {
                        string message = "";
                        if (string.Empty == relation.value)
                        {
                            message = base.Log.FormatResourceString("MissingRequiredArgument", new object[] { relation.argument, property.Name });
                        }
                        else
                        {
                            message = base.Log.FormatResourceString("MissingRequiredArgumentWithValue", new object[] { relation.argument, property.Name, relation.value });
                        }
                        base.Log.LogError(message, new object[0]);
                        if (bThrowOnError)
                        {
                            throw new LoggerException(message);
                        }
                        return false;
                    }
                }
            }
            return true;
        }

        // Properties
        public virtual string[] AcceptableNonZeroExitCodes
        {
            get
            {
                return this.acceptableNonZeroExitCodes;
            }
            set
            {
                this.acceptableNonZeroExitCodes = value;
            }
        }

        protected Dictionary<string, ToolSwitch> ActiveToolSwitches
        {
            get
            {
                return this.activeToolSwitches;
            }
        }

        public Dictionary<string, ToolSwitch> ActiveToolSwitchesValues
        {
            get
            {
                return this.activeToolSwitchesValues;
            }
            set
            {
                this.activeToolSwitchesValues = value;
            }
        }

        public string AdditionalOptions
        {
            get
            {
                return this.additionalOptions;
            }
            set
            {
                this.additionalOptions = value;
            }
        }

        protected virtual string AlwaysAppend
        {
            get
            {
                return string.Empty;
            }
            set
            {
            }
        }

        protected string CancelEventName
        {
            get
            {
                return this.cancelEventName;
            }
        }

        protected override Encoding ResponseFileEncoding
        {
            get
            {
                return Encoding.Unicode;
            }
        }

        protected override MessageImportance StandardErrorLoggingImportance
        {
            get
            {
                return MessageImportance.High;
            }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get
            {
                return MessageImportance.High;
            }
        }

        protected virtual ArrayList SwitchOrderList
        {
            get
            {
                return null;
            }
        }
    }
}


*/