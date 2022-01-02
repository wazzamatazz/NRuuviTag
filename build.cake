///////////////////////////////////////////////////////////////////////////////////////////////////
// Use build.ps1 or build.sh to run the build script. Command line arguments are documented below.
///////////////////////////////////////////////////////////////////////////////////////////////////

const string DefaultSolutionFile = "./NRuuviTag.sln";
const string VersionFile = "./build/version.json";

///////////////////////////////////////////////////////////////////////////////////////////////////
// COMMAND LINE ARGUMENTS:
//
// --project=<PROJECT OR SOLUTION>
//   The MSBuild project or solution to build. 
//     Default: see DefaultSolutionFile constant above.
//
// --target=<TARGET>
//   The Cake target to run. 
//     Default: Test
//     Possible Values: Clean, Restore, Build, Test, Pack
//
// --configuration=<CONFIGURATION>
//   The MSBuild configuration to use. 
//     Default: Debug
//
// --clean
//   Specifies that this is a rebuild rather than an incremental build. All artifact, bin, and test 
//   output folders will be cleaned prior to running the specified target.
//
// --no-tests
//   Specifies that unit tests should be skipped, even if a target that depends on the Test target 
//   is specified.
//
// --ci
//   Forces continuous integration build mode. Not required if the build is being run by a 
//   supported continuous integration build system.
//
// --sign-output
//   Tells MSBuild that signing is required by setting the 'SignOutput' build property to 'True'. 
//   The signing implementation must be supplied by MSBuild.
//
// --build-counter=<COUNTER>
//   The build counter. This is used when generating version numbers for the build.
//
// --build-metadata=<METADATA>
//   Additional build metadata that will be included in the information version number generated 
//   for compiled assemblies.
//
// --property=<PROPERTY>
//   Specifies an additional property to pass to MSBuild during Build and Pack targets. The value
//   must be specified using a '<NAME>=<VALUE>' format e.g. --property="NoWarn=CS1591". This 
//   argument can be specified multiple times.
// 
///////////////////////////////////////////////////////////////////////////////////////////////////

#load nuget:?package=Jaahas.Cake.Extensions&version=1.0.0

// Bootstrap build context and tasks.
Bootstrap(DefaultSolutionFile, VersionFile);

// Add Publish target
Task("Publish")
    .IsDependentOn("Test")
    .Does<BuildState>(state => {
        foreach (var projectFile in GetFiles("./**/*.*proj")) {
            var projectDir = projectFile.GetDirectory();
            foreach (var publishProfileFile in GetFiles(projectDir.FullPath + "/**/*.pubxml")) {
                WriteLogMessage(BuildSystem, $"Publishing project {projectFile.GetFilename()} using profile {publishProfileFile.GetFilename()}.");

                var buildSettings = new DotNetPublishSettings {
                    Configuration = state.Configuration,
                    MSBuildSettings = new DotNetMSBuildSettings()
                };

                ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
                buildSettings.MSBuildSettings.Properties["PublishProfile"] = new List<string> { publishProfileFile.FullPath };
                DotNetPublish(projectFile.FullPath, buildSettings);
            }
        }
    });

// Get the target that was specified.
var target = GetTarget();

// Run the target.
RunTarget(target);
