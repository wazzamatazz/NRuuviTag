///////////////////////////////////////////////////////////////////////////////////////////////////
// Use build.ps1 or build.sh to run the build script. Command line arguments are documented below.
///////////////////////////////////////////////////////////////////////////////////////////////////

const string DefaultSolutionFile = "./NRuuviTag.slnx";
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
//     Possible Values: Clean, Restore, Build, Test, Pack, Publish, PublishContainer, BillOfMaterials
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
//   Additional build metadata that will be included in the informational version number generated 
//   for compiled assemblies.
//
// --container-registry=<REGISTRY>
//   The container registry to use when the PublishContainer target is specified.
//     Default: Local Docker or Podman daemon
//
// --container-os=<OS>
//   The container operating system to use when the PublishContainer target is specified.
//     Default: linux
//
// --container-arch=<ARCHITECTURE>
//   The container processor architecture to use when the PublishContainer target is specified.
//     Default: Same as current machine
//
// --property=<PROPERTY>
//   Specifies an additional property to pass to MSBuild during Build and Pack targets. The value
//   must be specified using a '<NAME>=<VALUE>' format e.g. --property="NoWarn=CS1591". This 
//   argument can be specified multiple times.
//
// --github-username=<USERNAME>
//   Specifies the GitHub username to use when making authenticated API calls to GitHub while 
//   running the BillOfMaterials target. You must specify the --github-token argument as well when 
//   specifying this argument.
//
// --github-token=<PERSONAL ACCESS TOKEN>
//   Specifies the GitHub personal access token to use when making authenticated API calls to 
//   GitHub while running the BillOfMaterials target. You must specify the --github-username 
//   argument as well when specifying this argument.
// 
///////////////////////////////////////////////////////////////////////////////////////////////////

#load nuget:?package=Jaahas.Cake.Extensions&version=3.1.0

// Bootstrap build context and tasks.
Bootstrap(
    DefaultSolutionFile, 
    VersionFile,
    containerProjects: new [] {
        "NRuuviTag.Cli.Linux"
    });

// Run the requested target.
Run();
