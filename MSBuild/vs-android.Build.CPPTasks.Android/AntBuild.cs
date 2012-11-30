﻿// ***********************************************************************************************
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

namespace vs_android.Build.CPPTasks.Android
{
    public class AntBuild : TrackedVCToolTask
    {
        private const string BUILD_LIB_PATH = "libs";
        private const string BUILD_BIN_PATH = "bin";

        private string m_toolFileName;
        private string m_inputSoPath;
        private string m_armEabiSoPath;
        private string m_antOpts;

        private AntBuildParser m_parser = new AntBuildParser();

        public bool BuildingInIDE { get; set; }
        public string JVMHeapInitial { get; set; }
		public string JVMHeapMaximum { get; set; }

		[Required]
		public bool IgnoreJavaOpts { get; set; }

        [Required]
        public string AntBuildPath { get; set; }

        [Required]
        public string AntAndroidSdkPath { get; set; }

        [Required]
        public string AntJavaHomePath { get; set; }

        [Required]
        public string AntBuildType { get; set; }
        
        [Required]
        public string AntLibraryName { get; set; }
        
        [Required]
        public string GCCToolPath { get; set; }

        [Required]
        public string ApkLibsPath { get; set; }
        
        [Required]
        public virtual ITaskItem[] Sources { get; set; }
        
        [Output]
        public virtual string OutputFile { get; set; }

        [Output]
        public string ApkName { get; set; }

        [Output]
        public string ActivityName { get; set; }

        [Output]
        public string PackageName { get; set; }

        public AntBuild()
            : base(new ResourceManager("vs_android.Build.CppTasks.Android.Properties.Resources", Assembly.GetExecutingAssembly()))
        {

        }

        protected override bool ValidateParameters()
        {
            m_toolFileName = Path.GetFileNameWithoutExtension(ToolName);

			if ( !m_parser.Parse( AntBuildPath, AntBuildType, Log, true ) )
            {
                return false;
            }

            ActivityName = m_parser.ActivityName;
            ApkName = m_parser.ApkName;
            PackageName = m_parser.PackageName;
            OutputFile = m_parser.OutputFile;

            // Only one .so library should be input to this task
            if ( Sources.Length > 1 )
            {
                Log.LogError("More than one .so library being built!");
                return false;
            }

            m_inputSoPath = Path.GetFullPath(Sources[0].GetMetadata("FullPath"));

            // Copy the .so file into the correct place
            m_armEabiSoPath = Path.GetFullPath(AntBuildPath + "\\" + BUILD_LIB_PATH + "\\" + ApkLibsPath + "\\" + AntLibraryName + ".so");

            m_antOpts = string.Empty;
            if (JVMHeapInitial != null && JVMHeapInitial.Length > 0)
            {
                m_antOpts += "-Xms" + JVMHeapInitial + "m";
            }
            if (JVMHeapMaximum != null && JVMHeapMaximum.Length > 0)
            {
                if ( m_antOpts.Length > 0 )
                {
                    m_antOpts += " ";
                }
                m_antOpts += "-Xmx" + JVMHeapMaximum + "m";
            }

            return base.ValidateParameters();
        }

        protected int ExecuteTool2(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            int num = 0;
            try
            {
                num = this.TrackerExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
            }
            finally
            {
                if (this.MinimalRebuildFromTracking || this.TrackFileAccess)
                {
                    CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(this.TLogWriteFiles);
                    CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles(this.TLogReadFiles, this.TrackedInputFiles, this.ExcludedInputPaths, outputs, false, this.MaintainCompositeRootingMarkers);
                    string[] strArray = null;
                    IDictionary<string, string> sourcesToCommandLines = this.MapSourcesToCommandLines();
                    if (num != 0)
                    {
                        outputs.RemoveEntriesForSource(this.SourcesCompiled);
                        outputs.SaveTlog();
                        compactInputs.RemoveEntriesForSource(this.SourcesCompiled);
                        compactInputs.SaveTlog();
                        if (this.MaintainCompositeRootingMarkers)
                        {
                            sourcesToCommandLines.Remove(FileTracker.FormatRootingMarker(this.SourcesCompiled));
                        }
                        else
                        {
                            foreach (ITaskItem item in this.SourcesCompiled)
                            {
                                sourcesToCommandLines.Remove(FileTracker.FormatRootingMarker(item));
                            }
                        }
                        this.WriteSourcesToCommandLinesTable(sourcesToCommandLines);
                    }
                    else
                    {
                        this.AddTaskSpecificOutputs(this.SourcesCompiled, outputs);
                        this.RemoveTaskSpecificOutputs(outputs);
                        outputs.RemoveDependenciesFromEntryIfMissing(this.SourcesCompiled);
                        if (this.MaintainCompositeRootingMarkers)
                        {
                            strArray = outputs.RemoveRootsWithSharedOutputs(this.SourcesCompiled);
                            foreach (string str in strArray)
                            {
                                compactInputs.RemoveEntryForSourceRoot(str);
                            }
                        }
                        if ((this.TrackedOutputFilesToIgnore != null) && (this.TrackedOutputFilesToIgnore.Length > 0))
                        {
                            Dictionary<string, ITaskItem> trackedOutputFilesToRemove = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
                            foreach (ITaskItem item2 in this.TrackedOutputFilesToIgnore)
                            {
                                trackedOutputFilesToRemove.Add(item2.GetMetadata("FullPath"), item2);
                            }
                            outputs.SaveTlog(delegate(string fullTrackedPath)
                            {
                                if (trackedOutputFilesToRemove.ContainsKey(fullTrackedPath))
                                {
                                    return false;
                                }
                                return true;
                            });
                        }
                        else
                        {
                            outputs.SaveTlog();
                        }
                        this.RemoveTaskSpecificInputs(compactInputs);
                        compactInputs.RemoveDependenciesFromEntryIfMissing(this.SourcesCompiled);
                        if ((this.TrackedInputFilesToIgnore != null) && (this.TrackedInputFilesToIgnore.Length > 0))
                        {
                            Dictionary<string, ITaskItem> trackedInputFilesToRemove = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
                            foreach (ITaskItem item3 in this.TrackedInputFilesToIgnore)
                            {
                                trackedInputFilesToRemove.Add(item3.GetMetadata("FullPath"), item3);
                            }
                            compactInputs.SaveTlog(delegate(string fullTrackedPath)
                            {
                                if (trackedInputFilesToRemove.ContainsKey(fullTrackedPath))
                                {
                                    return false;
                                }
                                return true;
                            });
                        }
                        else
                        {
                            compactInputs.SaveTlog();
                        }
                        if (this.MaintainCompositeRootingMarkers)
                        {
                            string str2 = GenerateCommandLine();
                            sourcesToCommandLines[FileTracker.FormatRootingMarker(this.SourcesCompiled)] = str2;
                            if (strArray != null)
                            {
                                foreach (string str3 in strArray)
                                {
                                    sourcesToCommandLines.Remove(str3);
                                }
                            }
                        }
                        else
                        {
                            string str4 = this.SourcesPropertyName ?? "Sources";
                            string str5 = GenerateCommandLineExceptSwitches(new string[] { str4 });
                            foreach (ITaskItem item4 in this.SourcesCompiled)
                            {
                                sourcesToCommandLines[FileTracker.FormatRootingMarker(item4)] = str5 + " " + item4.GetMetadata("FullPath").ToUpperInvariant();
                            }
                        }
                        this.WriteSourcesToCommandLinesTable(sourcesToCommandLines);
                    }
                }
            }

            return num;
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            // Copy over the .so file to the correct directory in the build structure
            Directory.CreateDirectory(AntBuildPath + "\\" + BUILD_LIB_PATH + "\\" + ApkLibsPath);
            File.Copy(m_inputSoPath, m_armEabiSoPath, true);

            // Create local properties file from Android SDK Path
            WriteLocalProperties();

            // List of environment variables
            List<String> envList = new List<String>();

            // Set JAVA_HOME for the ant build
			SetEnvVar( envList, "JAVA_HOME", AntJavaHomePath );

			// Set ANT_OPTS, if appropriate
			if ( m_antOpts.Length > 0 )
			{
				SetEnvVar( envList, "ANT_OPTS", m_antOpts );
			}

			// Ignore JAVA_OPTS?
			if ( IgnoreJavaOpts )
			{
				SetEnvVar( envList, "JAVA_OPTS", "" );
			}

            // Set environment variables
            this.EnvironmentVariables = envList.ToArray();

            return ExecuteTool2(pathToTool, responseFileCommands, commandLineCommands);
        }

		private void SetEnvVar( List<String> envList, string var, string setting )
		{
			Log.LogMessage( MessageImportance.High, "Envvar: {0} is set to '{1}'", var, setting );
			envList.Add( var + "=" + setting );
			System.Environment.SetEnvironmentVariable( var, setting, EnvironmentVariableTarget.Process );
		}

        private void WriteLocalProperties()
        {
            string localPropsFile = Path.GetFullPath(AntBuildPath + "\\local.properties");

            // Need double backslashes for this path
            string sdkPath = Path.GetFullPath(AntAndroidSdkPath).Replace( "\\", "\\\\" );

            string fileContents = vs_android.Build.CPPTasks.Android.Properties.Resources.localproperties_ant_file;
            fileContents = fileContents.Replace("{SDKDIR}", sdkPath);
            
            using (StreamWriter outfile = new StreamWriter(localPropsFile))
            {
                outfile.Write(fileContents);
            }
        }

        protected override void RemoveTaskSpecificInputs(CanonicalTrackedInputFiles compactInputs)
        {
            // This is necessary because the VC tracker gets confused by the intermingling of reading and writing by the support apps

            foreach (KeyValuePair<string, Dictionary<string, string>> pair in compactInputs.DependencyTable)
            {
                List<string> delFiles = new List<string>();

                foreach (KeyValuePair<string, string> depFile in pair.Value)
                {
                    // Remove the -unaligned.apk file, it shouldn't be in the input list
					if ( depFile.Key.ToLowerInvariant().EndsWith( "-unaligned.apk" ) )
                    {
                        delFiles.Add(depFile.Key);
					}
					// Same deal with build.prop
					if ( depFile.Key.ToLowerInvariant().EndsWith( "build.prop" ) )
					{
						delFiles.Add( depFile.Key );
					}
                }

                // Do deletions
                foreach (string delFile in delFiles)
                {
                    pair.Value.Remove(delFile);
                }

                // Add the two .so files to the inputs
				if ( pair.Value.ContainsKey( m_inputSoPath.ToUpperInvariant() ) == false )
				{
					pair.Value.Add( m_inputSoPath.ToUpperInvariant(), null );
				}
				if ( pair.Value.ContainsKey( m_armEabiSoPath.ToUpperInvariant() ) == false )
				{
					pair.Value.Add( m_armEabiSoPath.ToUpperInvariant(), null );
				}
            }
        }

        protected override void RemoveTaskSpecificOutputs(CanonicalTrackedOutputFiles compactOutputs)
        {
            // Find each non-apk output, and delete it
            // This is necessary because the VC tracker gets confused by the intermingling of reading and writing by the support apps

            foreach (KeyValuePair<string, Dictionary<string, DateTime>> pair in compactOutputs.DependencyTable)
            {
                List<string> delFiles = new List<string>();

                foreach (KeyValuePair<string, DateTime> depFile in pair.Value)
                {
                    // Remove all non-apk files from the output list
                    if (depFile.Key.ToLowerInvariant().EndsWith(".apk") == false)
                    {
                        delFiles.Add(depFile.Key);
                    }
                    // But *do* remove the -unaligned.apk from the list.
					if ( depFile.Key.ToLowerInvariant().EndsWith( "-unaligned.apk" ) )
					{
						delFiles.Add( depFile.Key );
					}
                }

                // Do deletions
                foreach (string delFile in delFiles)
                {
                    pair.Value.Remove(delFile);
                }
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

        protected override string GenerateCommandLineCommands()
        {
            // Simply 'debug', or 'release'.
            return AntBuildType.ToLower();
        }

        protected override string GenerateResponseFileCommands()
        {
            return string.Empty;
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
                return "Ant";
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
                return GCCToolPath;
            }
        }

        protected override ITaskItem[] TrackedInputFiles
        {
            get
            {
                return Sources;
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
                return new string[] { 
                    "cmd-java-zipalign.read.*.tlog", 
                    "cmd-java-zipalign.*.read.*.tlog",
                    "cmd-java-aapt.read.*.tlog", 
                    "cmd-java-aapt.*.read.*.tlog",
                    "cmd.read.*.tlog", 
                    "cmd.*.read.*.tlog",
                    "cmd-java.read.*.tlog", 
                    "cmd-java.*.read.*.tlog",
                    "java.read.*.tlog", 
                    "java.*.read.*.tlog",
                };
            }
        }

        protected override string[] WriteTLogNames
        {
            get
            {
                return new string[] { 
                    "cmd-java-zipalign.write.*.tlog", 
                    "cmd-java-zipalign.*.write.*.tlog",
                    "cmd-java-aapt.write.*.tlog", 
                    "cmd-java-aapt.*.write.*.tlog",
                    "cmd.write.*.tlog", 
                    "cmd.*.write.*.tlog",
                    "cmd-java.write.*.tlog", 
                    "cmd-java.*.write.*.tlog",
                    "java.write.*.tlog", 
                    "java.*.write.*.tlog",
                };
            }
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
            this.BuildAdditionalArgs(clb);
            return clb.ToString();
        }


    }


}
