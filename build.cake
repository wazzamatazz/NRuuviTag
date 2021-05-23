///////////////////////////////////////////////////////////////////////////////////////////////////
// Use build.ps1 to run the build script. Command line arguments are documented below.
///////////////////////////////////////////////////////////////////////////////////////////////////

const string DefaultSolutionName = "./RENAME-ME.sln";

///////////////////////////////////////////////////////////////////////////////////////////////////
// COMMAND LINE ARGUMENTS:
//
// --project=<PROJECT OR SOLUTION>
//   The MSBuild project or solution to build. 
//     Default: see DefaultSolutionName constant above.
//
// --target=<TARGET>
//   Specifies the Cake target to run. 
//     Default: Test
//     Possible Values: Clean, Restore, Build, Test, Pack
//
// --configuration=<CONFIGURATION>
//   Specifies the MSBuild configuration to use. 
//     Default: Debug
//
// --clean
//   Specifies if this is a rebuild rather than an incremental build. All artifact, bin, and test 
//   output folders will be cleaned prior to running the specified target.
//
// --no-tests
//   Specifies that tests should be skipped.
//
// --ci
//   Forces continuous integration build mode. Not required if the build is being run by a 
//   supported continuous integration build system.
//
// --sign-output
//   Tells MSBuild that signing is required by setting the 'SignOutput' property to 'True'. The 
//   signing implementation must be supplied by MSBuild.
//
// --build-counter=<COUNTER>
//   The build counter. This is used when generating version numbers for the build.
//
// --build-metadata=<METADATA>
//   Additional build metadata that will be included in the information version number generated 
//   for compiled assemblies.
//
// --verbose
//   Enables verbose messages.
//
// --property=<PROPERTY>
//   Specifies an additional property to pass to MSBuild during Build and Pack targets. The value
//   must be specified using a '<NAME>=<VALUE>' format e.g. --property="NoWarn=CS1591". This 
//   argument can be specified multiple times.
//   
///////////////////////////////////////////////////////////////////////////////////////////////////

#addin nuget:?package=Cake.Git&version=1.0.0
#addin nuget:?package=Cake.Json&version=6.0.0
#addin nuget:?package=Newtonsoft.Json&version=12.0.3

#load "build/build-state.cake"
#load "build/build-utilities.cake"

// Get the target that was specified.
var target = Argument("target", "Test");


///////////////////////////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////////////////////////


// Constructs the build state object.
Setup<BuildState>(context => {
    try {
        BuildUtilities.WriteTaskStartMessage(BuildSystem, "Setup");
        var state = new BuildState() {
            SolutionName = Argument("project", DefaultSolutionName),
            Target = target,
            Configuration = Argument("configuration", "Debug"),
            ContinuousIntegrationBuild = HasArgument("ci") || !BuildSystem.IsLocalBuild,
            Clean = HasArgument("clean"),
            SkipTests = HasArgument("no-tests"),
            SignOutput = HasArgument("sign-output"),
            Verbose = HasArgument("verbose"),
            MSBuildProperties = HasArgument("property") ? Arguments<string>("property") : new List<string>()
        };

        // Get raw version numbers from JSON.

        var versionJson = ParseJsonFromFile("./build/version.json");

        var majorVersion = versionJson.Value<int>("Major");
        var minorVersion = versionJson.Value<int>("Minor");
        var patchVersion = versionJson.Value<int>("Patch");
        var versionSuffix = versionJson.Value<string>("PreRelease");

        // Compute build number.

        var buildCounter = Argument("build-counter", 0);
        var branch = GitBranchCurrent(DirectoryPath.FromString(".")).FriendlyName;

        state.BuildNumber = string.IsNullOrWhiteSpace(versionSuffix)
            ? $"{majorVersion}.{minorVersion}.{patchVersion}.{buildCounter}+{branch}"
            : $"{majorVersion}.{minorVersion}.{patchVersion}-{versionSuffix}.{buildCounter}+{branch}";

        if (!string.Equals(state.Target, "Clean", StringComparison.OrdinalIgnoreCase)) {
            BuildUtilities.SetBuildSystemBuildNumber(BuildSystem, state);
            BuildUtilities.WriteBuildStateToLog(BuildSystem, state);
        }

        return state;
    }
    finally {
        BuildUtilities.WriteTaskEndMessage(BuildSystem, "Setup");
    }
});


// Pre-task action.
TaskSetup(context => {
    BuildUtilities.WriteTaskStartMessage(BuildSystem, context.Task.Name);
    BuildUtilities.WriteLogMessage(BuildSystem, $"Running {context.Task.Name} task");
});


// Post task action.
TaskTeardown(context => {
    BuildUtilities.WriteLogMessage(BuildSystem, $"Completed {context.Task.Name} task");
    BuildUtilities.WriteTaskEndMessage(BuildSystem, context.Task.Name);
});


///////////////////////////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////////////////////////


// Cleans up artifact and bin folders.
Task("Clean")
    .WithCriteria<BuildState>((c, state) => state.RunCleanTarget)
    .Does<BuildState>(state => {
        foreach (var pattern in new [] { $"./src/**/bin/{state.Configuration}", "./artifacts/**", "./**/TestResults/**" }) {
            BuildUtilities.WriteLogMessage(BuildSystem, $"Cleaning directories: {pattern}");
            CleanDirectories(pattern);
        }
    });


// Restores NuGet packages.
Task("Restore")
    .Does<BuildState>(state => {
        DotNetCoreRestore(state.SolutionName);
    });


// Builds the solution.
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does<BuildState>(state => {
        var buildSettings = new DotNetCoreBuildSettings {
            Configuration = state.Configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        buildSettings.MSBuildSettings.Targets.Add(state.Clean ? "Rebuild" : "Build");
        BuildUtilities.ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
        DotNetCoreBuild(state.SolutionName, buildSettings);
    });


// Runs unit tests.
Task("Test")
    .IsDependentOn("Build")
    .WithCriteria<BuildState>((c, state) => !state.SkipTests)
    .Does<BuildState>(state => {
        var testSettings = new DotNetCoreTestSettings {
            Configuration = state.Configuration,
            NoBuild = true
        };

        var testResultsPrefix = state.ContinuousIntegrationBuild
            ? Guid.NewGuid().ToString()
            : null;

        if (testResultsPrefix != null) {
            // We're using a build system; write the test results to a file so that they can be 
            // imported into the build system.
            testSettings.Loggers = new List<string> {
                $"trx;LogFilePrefix={testResultsPrefix}"
            };
        }

        DotNetCoreTest(state.SolutionName, testSettings);

        if (testResultsPrefix != null) {
            foreach (var testResultsFile in GetFiles($"./**/TestResults/{testResultsPrefix}*.trx")) {
                BuildUtilities.ImportTestResults(BuildSystem, "mstest", testResultsFile);
            }
        }
    });


// Builds NuGet packages.
Task("Pack")
    .IsDependentOn("Test")
    .Does<BuildState>(state => {
        var buildSettings = new DotNetCorePackSettings {
            Configuration = state.Configuration,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
        };

        BuildUtilities.ApplyMSBuildProperties(buildSettings.MSBuildSettings, state);
        DotNetCorePack(state.SolutionName, buildSettings);
    });


///////////////////////////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////////////////////////


RunTarget(target);