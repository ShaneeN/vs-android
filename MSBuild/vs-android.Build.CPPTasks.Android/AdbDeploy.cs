// ***********************************************************************************************
// (c) 2012 Gavin Pugh http://www.gavpugh.com/ - Released under the open-source zlib license
// ***********************************************************************************************

// Apache Ant, Apk Building Task.

using System;
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
using System.ComponentModel;
using System.Runtime;
using System.Collections.Specialized;
using System.Threading;

namespace vs_android.Build.CPPTasks.Android
{
    public class AdbDeploy : TrackedVCToolTask
    {
        // Fields
        private IntPtr cancelEvent;
 

        private List<KeyValuePair<object, object>> environmentVariablePairs;
        private static char[] equalsSplitter = new char[] { '=' };
        private object eventCloseLock;
        private bool eventsDisposed;
        private int exitCode;
        private TaskLoggingHelper logPrivate;
        private TaskLoggingHelper logShared;
        private bool logStandardErrorAsError;
        private Queue standardErrorData;
        private ManualResetEvent standardErrorDataAvailable;
        private string standardErrorImportance;
        private MessageImportance standardErrorImportanceToUse;
        private Queue standardOutputData;
        private ManualResetEvent standardOutputDataAvailable;
        private string standardOutputImportance;
        private MessageImportance standardOutputImportanceToUse;
        private string temporaryBatchFile;
        private bool terminatedTool;
        private int timeout;
        private string toolExe;
        private ManualResetEvent toolExited;
        private string toolPath;
        private ManualResetEvent toolTimeoutExpired;
        private Timer toolTimer;
 
        public bool BuildingInIDE { get; set; }

        //public string StandardErrorImportance { [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")] get; [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")] set; }
        //protected MessageImportance StandardErrorImportanceToUse { [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")] get; }
   

        [Required]
        public string AntBuildPath { get; set; }

        [Required]
        public string AntBuildType { get; set; }

        [Required]
        public string AdbPath { get; set; }

        [Required]
        public string Params { get; set; }

        [Required]
        public string DeviceArgs { get; set; }

        public string GenerateCmdFilePath { get; set; }
		
        private AntBuildParser m_parser = new AntBuildParser();

        private string m_toolFileName;

        public AdbDeploy()
            : base(new ResourceManager("vs_android.Build.CppTasks.Android.Properties.Resources", Assembly.GetExecutingAssembly()))
        {

        }

        private void WriteDebugRunCmdFile()
        {            
            string destCmdFile = Path.GetFullPath(GenerateCmdFilePath);

            using (StreamWriter outfile = new StreamWriter(destCmdFile))
            {
                outfile.Write(string.Format("{0} {1} shell am start -n {2}/{3}\n", AdbPath, MakeStringReplacements(DeviceArgs), m_parser.PackageName, m_parser.ActivityName));
            }
        }

        protected override bool ValidateParameters()
        {
            m_toolFileName = Path.GetFileNameWithoutExtension(ToolName);

            if ( !m_parser.Parse( AntBuildPath, AntBuildType, Log, false ) )
            {
                return false;
            }

            return base.ValidateParameters();
        }

        public override void Cancel()
        {
            Process.Start(AdbPath, "kill-server");

            base.Cancel();
        }

        public override bool Execute()
        {
            bool flag = Execute2();
            this.Cancel();
            VCTaskNativeMethods.CloseHandle( this.cancelEvent);
            return flag;

        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            Log.LogMessage(MessageImportance.High, "{0} {1}", pathToTool, commandLineCommands);

			if ( ( GenerateCmdFilePath != null ) && ( GenerateCmdFilePath.Length > 0 ) )
			{
				WriteDebugRunCmdFile();
			}

			if ( commandLineCommands.Contains( "wait-for-device" ) || commandLineCommands.Contains( "start-server" ) )
			{
				// Hack to spawn a process, instead of waiting on it
				Process.Start( pathToTool, commandLineCommands );
				return 0;
			}
			else
			{
				return base.ExecuteTool( pathToTool, responseFileCommands, commandLineCommands );
			}
        }

        public override bool AttributeFileTracking
        {
            get
            {
                return true;
            }
        }

        protected override string GetWorkingDirectory()
        {
            return AntBuildPath;
        }

        private string MakeStringReplacements( string theString )
        {
            string paramCopy = theString;
            paramCopy = paramCopy.Replace("{PackageName}", m_parser.PackageName);
            paramCopy = paramCopy.Replace("{ApkPath}", "\"" + m_parser.OutputFile + "\"");
            paramCopy = paramCopy.Replace("{ActivityName}", m_parser.ActivityName);
            return paramCopy.Trim();
        }

        protected override string GenerateCommandLineCommands()
        {
            return (MakeStringReplacements(DeviceArgs) + " " + MakeStringReplacements(Params)).Trim();
        }

        protected override bool MaintainCompositeRootingMarkers
        {
            get
            {
                return true;
            }
        }

        public virtual string PlatformToolset
        {
            get
            {
                return "Adb";
            }
        }

        protected override Encoding ResponseFileEncoding
        {
            get
            {
                return Encoding.ASCII;
            }
        }

        protected override string ToolName
        {
            get
            {
                return AdbPath;
            }
        }

        protected override ITaskItem[] TrackedInputFiles
        {
            get
            {
                return new TaskItem[] { new TaskItem( m_parser.OutputFile) };
            }
        }

        protected override string TrackerIntermediateDirectory
        {
            get
            {
                if (this.TrackerLogDirectory != null)
                {
                    return this.TrackerLogDirectory;
                }
                return string.Empty;
            }
        }

        public virtual string TrackerLogDirectory
        {
            get
            {
                if (base.IsPropertySet("TrackerLogDirectory"))
                {
                    return base.ActiveToolSwitches["TrackerLogDirectory"].Value;
                }
                return null;
            }
            set
            {
                base.ActiveToolSwitches.Remove("TrackerLogDirectory");
                ToolSwitch switch2 = new ToolSwitch(ToolSwitchType.Directory)
                {
                    DisplayName = "Tracker Log Directory",
                    Description = "Tracker log directory.",
                    ArgumentRelationList = new ArrayList(),
                    Value = VCToolTask.EnsureTrailingSlash(value)
                };
                base.ActiveToolSwitches.Add("TrackerLogDirectory", switch2);
                base.AddActiveSwitchToolValue(switch2);
            }
        }

        protected override string CommandTLogName
        {
            get
            {
                return (m_toolFileName + ".command.1.tlog");
            }
        }

        protected override string[] ReadTLogNames
        {
            get
            {
                return new string[] { (m_toolFileName + ".read.*.tlog"), (m_toolFileName + ".*.read.*.tlog") };
            }
        }

        protected override string[] WriteTLogNames
        {
            get
            {
                return new string[] { (m_toolFileName + ".write.*.tlog"), (m_toolFileName + ".*.write.*.tlog") };
            }
        }

        private bool AssignStandardStreamLoggingImportance()
        {
            if ((this.standardErrorImportance == null) || (this.standardErrorImportance.Length == 0))
            {
                this.standardErrorImportanceToUse = this.StandardErrorLoggingImportance;
            }
            else
            {
                try
                {
                    this.standardErrorImportanceToUse = (MessageImportance)Enum.Parse(typeof(MessageImportance), this.standardErrorImportance, true);
                }
                catch (ArgumentException)
                {
                    base.Log.LogErrorWithCodeFromResources("Message.InvalidImportance", new object[] { this.standardErrorImportance });
                    return false;
                }
            }
            if ((this.standardOutputImportance == null) || (this.standardOutputImportance.Length == 0))
            {
                this.standardOutputImportanceToUse = this.StandardOutputLoggingImportance;
            }
            else
            {
                try
                {
                    this.standardOutputImportanceToUse = (MessageImportance)Enum.Parse(typeof(MessageImportance), this.standardOutputImportance, true);
                }
                catch (ArgumentException)
                {
                    base.Log.LogErrorWithCodeFromResources("Message.InvalidImportance", new object[] { this.standardOutputImportance });
                    return false;
                }
            }
            return true;
        }

        public bool Execute2()
        {
            bool flag2;
            if (!this.ValidateParameters())
            {
                return false;
            }
            if (this.EnvironmentVariables != null)
            {
                this.environmentVariablePairs = new List<KeyValuePair<object, object>>(this.EnvironmentVariables.Length);
                foreach (string str in this.EnvironmentVariables)
                {
                    string[] strArray = str.Split(equalsSplitter, 2);
                    if ((strArray.Length == 1) || ((strArray.Length == 2) && (strArray[0].Length == 0)))
                    {
                        this.LogPrivate.LogErrorWithCodeFromResources("ToolTask.InvalidEnvironmentParameter", new object[] { strArray[0] });
                        return false;
                    }
                    this.environmentVariablePairs.Add(new KeyValuePair<object, object>(strArray[0], strArray[1]));
                }
            }
            if (!this.AssignStandardStreamLoggingImportance())
            {
                return false;
            }
            try
            {
                if (this.SkipTaskExecution())
                {
                    return true;
                }
                string contents = this.GenerateCommandLineCommands();
                string message = contents;
                string responseFileCommands = this.GenerateResponseFileCommands();
                if (this.UseCommandProcessor)
                {
                    this.ToolExe = "cmd.exe";
                    this.temporaryBatchFile = FileUtilities.GetTemporaryFile(".cmd");
                    File.AppendAllText(this.temporaryBatchFile, contents, EncodingUtilities.CurrentSystemOemEncoding);
                    string temporaryBatchFile = this.temporaryBatchFile;
                    if (temporaryBatchFile.Contains("&") && !temporaryBatchFile.Contains("^&"))
                    {
                        temporaryBatchFile = NativeMethodsShared.GetShortFilePath(temporaryBatchFile).Replace("&", "^&");
                    }
                    contents = "/C \"" + temporaryBatchFile + "\"";
                    if (this.EchoOff)
                    {
                        contents = "/Q " + contents;
                    }
                }
                if ((contents == null) || (contents.Length == 0))
                {
                    contents = string.Empty;
                }
                else
                {
                    contents = " " + contents;
                }
                HostObjectInitializationStatus status = this.InitializeHostObject();
                switch (status)
                {
                    case HostObjectInitializationStatus.NoActionReturnSuccess:
                        return true;

                    case HostObjectInitializationStatus.NoActionReturnFailure:
                        this.exitCode = 1;
                        return this.HandleTaskExecutionErrors();

                    default:
                        {
                            string pathToTool = this.ComputePathToTool();
                            if (pathToTool == null)
                            {
                                return false;
                            }
                            bool alreadyLoggedEnvironmentHeader = false;
                            StringDictionary environmentOverride = this.EnvironmentOverride;
                            if (environmentOverride != null)
                            {
                                foreach (DictionaryEntry entry in environmentOverride)
                                {
                                    alreadyLoggedEnvironmentHeader = this.LogEnvironmentVariable(alreadyLoggedEnvironmentHeader, (string)entry.Key, (string)entry.Value);
                                }
                            }
                            if (this.environmentVariablePairs != null)
                            {
                                foreach (KeyValuePair<object, object> pair in this.environmentVariablePairs)
                                {
                                    alreadyLoggedEnvironmentHeader = this.LogEnvironmentVariable(alreadyLoggedEnvironmentHeader, (string)pair.Key, (string)pair.Value);
                                }
                            }
                            if (this.UseCommandProcessor)
                            {
                                this.LogToolCommand(pathToTool + contents);
                                this.LogToolCommand(message);
                            }
                            else
                            {
                                this.LogToolCommand(pathToTool + contents + " " + responseFileCommands);
                            }
                            this.exitCode = 0;
                            if (status == HostObjectInitializationStatus.UseHostObjectToExecute)
                            {
                                try
                                {
                                    if (!this.CallHostObjectToExecute())
                                    {
                                        this.exitCode = 1;
                                    }
                                    break;
                                }
                                catch (Exception exception)
                                {
                                    this.LogPrivate.LogErrorFromException(exception);
                                    return false;
                                }
                            }
                            ErrorUtilities.VerifyThrow(status == HostObjectInitializationStatus.UseAlternateToolToExecute, "Invalid return status");
                            this.exitCode = this.ExecuteTool(pathToTool, responseFileCommands, contents);
                            break;
                        }
                }
                if (this.terminatedTool)
                {
                    return false;
                }
                if (this.exitCode != 0)
                {
                    return this.HandleTaskExecutionErrors();
                }
                flag2 = true;
            }
            catch (ArgumentException exception2)
            {
                if (!this.terminatedTool)
                {
                    this.LogPrivate.LogErrorWithCodeFromResources("General.InvalidToolSwitch", new object[] { this.ToolExe, this.GetErrorMessageWithDiagnosticsCheck(exception2) });
                }
                flag2 = false;
            }
            catch (Win32Exception exception3)
            {
                if (!this.terminatedTool)
                {
                    this.LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", new object[] { this.ToolExe, this.GetErrorMessageWithDiagnosticsCheck(exception3) });
                }
                flag2 = false;
            }
            catch (IOException exception4)
            {
                if (!this.terminatedTool)
                {
                    this.LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", new object[] { this.ToolExe, this.GetErrorMessageWithDiagnosticsCheck(exception4) });
                }
                flag2 = false;
            }
            catch (UnauthorizedAccessException exception5)
            {
                if (!this.terminatedTool)
                {
                    this.LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", new object[] { this.ToolExe, this.GetErrorMessageWithDiagnosticsCheck(exception5) });
                }
                flag2 = false;
            }
            finally
            {
                if ((this.temporaryBatchFile != null) && File.Exists(this.temporaryBatchFile))
                {
                    File.Delete(this.temporaryBatchFile);
                }
            }
            return flag2;
        }

        private string ComputePathToTool()
        {
            string str;
            if (this.UseCommandProcessor)
            {
                return this.ToolExe;
            }
            if ((this.ToolPath != null) && (this.ToolPath.Length > 0))
            {
                str = Path.Combine(this.ToolPath, this.ToolExe);
            }
            else
            {
                str = this.GenerateFullPathToTool();
                if ((str != null) && !string.IsNullOrEmpty(this.toolExe))
                {
                    str = Path.Combine(Path.GetDirectoryName(str), this.ToolExe);
                }
            }
            if (str != null)
            {
                if (Path.GetFileName(str).Length != str.Length)
                {
                    if (!File.Exists(str))
                    {
                        this.LogPrivate.LogErrorWithCodeFromResources("ToolTask.ToolExecutableNotFound", new object[] { str });
                        return null;
                    }
                    return str;
                }
                string str3 = NativeMethodsShared.FindOnPath(str);
                if (str3 != null)
                {
                    str = str3;
                }
            }
            return str;
        }

        private bool LogEnvironmentVariable(bool alreadyLoggedEnvironmentHeader, string key, string value)
        {
            if (!alreadyLoggedEnvironmentHeader)
            {
                this.LogPrivate.LogMessageFromResources(MessageImportance.Low, "ToolTask.EnvironmentVariableHeader", new object[0]);
                alreadyLoggedEnvironmentHeader = true;
            }
            base.Log.LogMessage(MessageImportance.Low, "  {0}={1}", new object[] { key, value });
            return alreadyLoggedEnvironmentHeader;
        }

       /* protected virtual void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            base.Log.LogMessageFromText(singleLine, messageImportance);
        }*/

        private void LogMessagesFromStandardError()
        {
            this.LogMessagesFromStandardErrorOrOutput(this.standardErrorData, this.standardErrorDataAvailable, this.standardErrorImportanceToUse, StandardOutputOrErrorQueueType.StandardError);
        }

        private void LogMessagesFromStandardErrorOrOutput(Queue dataQueue, ManualResetEvent dataAvailableSignal, MessageImportance messageImportance, StandardOutputOrErrorQueueType queueType)
        {
            ErrorUtilities.VerifyThrow(dataQueue != null, "The data queue must be available.");
            lock (dataQueue.SyncRoot)
            {
                while (dataQueue.Count > 0)
                {
                    string singleLine = dataQueue.Dequeue() as string;
                    if (!this.LogStandardErrorAsError || (queueType == StandardOutputOrErrorQueueType.StandardOutput))
                    {
                        this.LogEventsFromTextOutput(singleLine, messageImportance);
                    }
                    else if (this.LogStandardErrorAsError && (queueType == StandardOutputOrErrorQueueType.StandardError))
                    {
                        base.Log.LogError(singleLine, new object[0]);
                    }
                }
                ErrorUtilities.VerifyThrow(dataAvailableSignal != null, "The signalling event must be available.");
                dataAvailableSignal.Reset();
            }
        }

        private void LogMessagesFromStandardOutput()
        {
            this.LogMessagesFromStandardErrorOrOutput(this.standardOutputData, this.standardOutputDataAvailable, this.standardOutputImportanceToUse, StandardOutputOrErrorQueueType.StandardOutput);
        }

      /*  protected virtual void LogPathToTool(string toolName, string pathToTool)
        {
        }

        protected virtual void LogToolCommand(string message)
        {
            this.LogPrivate.LogCommandLine(MessageImportance.High, message);
        }
        */
        private void ReceiveExitNotification(object sender, EventArgs e)
        {
            ErrorUtilities.VerifyThrow(this.toolExited != null, "The signalling event for tool exit must be available.");
            lock (this.eventCloseLock)
            {
                if (!this.eventsDisposed)
                {
                    this.toolExited.Set();
                }
            }
        }

        // Nested Types
        private enum StandardOutputOrErrorQueueType
        {
            StandardError,
            StandardOutput
        }

        private string GetErrorMessageWithDiagnosticsCheck(Exception e)
        {
            if (Environment.GetEnvironmentVariable("MSBuildDiagnostics") != null)
            {
                return e.ToString();
            }
            return e.Message;
        }

        protected string GenerateCommandLine()
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

            if (SwitchOrderList != null)
            {
                foreach (string str in this.SwitchOrderList)
                {
                    if (this.IsPropertySet(str))
                    {
                        ToolSwitch property = this.ActiveToolSwitches[str];
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

            }
            this.BuildAdditionalArgs(clb);
            return clb.ToString();
        }
    }

    

 

 
}