// ***********************************************************************************************
// (c) 2012 Gavin Pugh http://www.gavpugh.com/ - Released under the open-source zlib license
// ***********************************************************************************************

// GCC Linker task. Switches are data-driven via PropXmlParse.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.CPPTasks;
using Microsoft.Build.Utilities;

namespace vs_android.Build.CPPTasks.Android
{
	public class GCCLink : TrackedVCToolTask
	{
        private string m_toolFileName;
        private PropXmlParse m_propXmlParse;

        public bool BuildingInIDE { get; set; }

        [Required]
        public string GCCToolPath { get; set; }

        [Required]
        public string PropertyXmlFile { get; set; }

        [Required]
        public string EchoCommandLines { get; set; }

        [Required]
        public virtual string OutputFile { get; set; }

        [Required]
        public virtual ITaskItem[] Sources { get; set; }
        

		public GCCLink()
			: base( new ResourceManager( "vs_android.Build.CppTasks.Android.Properties.Resources", Assembly.GetExecutingAssembly() ) )
		{
            
		}

        protected override bool ValidateParameters()
        {
            m_propXmlParse = new PropXmlParse(PropertyXmlFile);

            m_toolFileName = Path.GetFileNameWithoutExtension(GCCToolPath);

            return base.ValidateParameters();
        }
        
        protected override string GenerateResponseFileCommands()
        {
            StringBuilder templateStr = new StringBuilder(Utils.EST_MAX_CMDLINE_LEN);

            foreach ( ITaskItem sourceFile in Sources )
            {
                templateStr.Append( Utils.PathSanitize( sourceFile.GetMetadata("Identity")) );
                templateStr.Append(" ");
            }

            templateStr.Append(m_propXmlParse.ProcessProperties(Sources[0]));
             
            return templateStr.ToString();
        }

        private void CleanUnusedTLogFiles()
        {
            // These tlog files are seemingly unused dep-wise, but cause problems when I add them to the proper TLog list
            // Incremental builds keep appending to them, so this keeps them from just growing and growing.
            string ignoreReadLogPath = Path.GetFullPath(TrackerLogDirectory + "\\" + m_toolFileName + ".read.1.tlog");
            string ignoreWriteLogPath = Path.GetFullPath(TrackerLogDirectory + "\\" + m_toolFileName + ".write.1.tlog");

            try
            {
                File.Delete(ignoreReadLogPath);
                File.Delete(ignoreWriteLogPath);
            }
            finally
            {

            }
        }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            CleanUnusedTLogFiles();
            if (EchoCommandLines == "true")
            {
                Log.LogMessage(MessageImportance.High, pathToTool + " " + responseFileCommands);
            }

            return ExecuteTool2(pathToTool, responseFileCommands, commandLineCommands);
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

 


        // Called when linker outputs a line
        protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
        {
            base.LogEventsFromTextOutput(Utils.GCCOutputReplace(singleLine), messageImportance);   
        }

		protected override void PostProcessSwitchList()
		{

		}
        
		public override bool AttributeFileTracking
		{
			get
			{
				return true;
			}
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
				return "GCC";
			}
			set
			{
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
				return this.Sources;
			}
		}

		protected override string TrackerIntermediateDirectory
		{
			get
			{
				if ( this.TrackerLogDirectory != null )
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
				if ( base.IsPropertySet( "TrackerLogDirectory" ) )
				{
					return base.ActiveToolSwitches["TrackerLogDirectory"].Value;
				}
				return null;
			}
			set
			{
				base.ActiveToolSwitches.Remove( "TrackerLogDirectory" );
				ToolSwitch switch2 = new ToolSwitch( ToolSwitchType.Directory )
				{
					DisplayName = "Tracker Log Directory",
					Description = "Tracker log directory.",
					ArgumentRelationList = new ArrayList(),
					Value = VCToolTask.EnsureTrailingSlash( value )
				};
				base.ActiveToolSwitches.Add( "TrackerLogDirectory", switch2 );
				base.AddActiveSwitchToolValue( switch2 );
			}
		}

        protected override string CommandTLogName
        {
            get
            {
                return m_toolFileName + "-link.command.1.tlog";
            }
        }

        protected override string[] ReadTLogNames
        {
            get
            {
                return new string[] { 
                    m_toolFileName + "-collect2.read.*.tlog", 
                    m_toolFileName + "-collect2.*.read.*.tlog", 
                    m_toolFileName + "-collect2-ld.read.*.tlog", 
                    m_toolFileName + "-collect2-ld.*.read.*.tlog"
                };
            }
        }

		protected override string[] WriteTLogNames
		{
			get
			{
                return new string[] { 
                    m_toolFileName + "-collect2.write.*.tlog", 
                    m_toolFileName + "-collect2.*.write.*.tlog", 
                    m_toolFileName + "-collect2-ld.write.*.tlog", 
                    m_toolFileName + "-collect2-ld.*.write.*.tlog"
                };
			}
		}

        public void Logger(String lines)
        {

            // Write the string to a file.append mode is enabled so that the log
            // lines get appended to  test.txt than wiping content and writing the log

            System.IO.StreamWriter file = new System.IO.StreamWriter("c:\\test.txt", true);
            file.WriteLine(lines);

            file.Close();

        }
	}


}
