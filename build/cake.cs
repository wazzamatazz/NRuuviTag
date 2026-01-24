#:sdk Cake.Sdk@6.0.0
#:package Cake.MinVer

using System.Collections.Immutable;

InstallTool("dotnet:https://api.nuget.org/v3/index.json?package=minver-cli&version=6.0.0");
InstallTool("dotnet:https://api.nuget.org/v3/index.json?package=CycloneDX&version=5.5.0");

var target = Argument("target", "Default");
var ciBuild = !BuildSystem.IsLocalBuild || HasArgument("ci");
var preparingRelease = "PrepareRelease".Equals(target, StringComparison.OrdinalIgnoreCase);

if (string.Equals(target, "Default", StringComparison.OrdinalIgnoreCase)) {
    AnsiConsole.Write(new FigletText("NRuuviTag"));
    
    AnsiConsole.Write(
        new Table()
            .AddColumns("Target", "Description")
            .AddRow("[yellow]Clean[/]", "Cleans up artifacts")
            .AddRow("[yellow]Build[/]", "Builds the solution")
            .AddRow("[yellow]Test[/]", "Runs unit tests")
            .AddRow("[yellow]Pack[/]", "Creates NuGet packages")
            .AddRow("[yellow]PublishExe[/]", "Creates CLI executable")
            .AddRow("[yellow]PublishContainer[/]", "Publishes CLI container image")
            .AddRow("[yellow]BillOfMaterials[/]", "Generates a Bill of Materials (BOM) for the solution")
            .AddRow("[yellow]PrepareRelease[/]", "Creates all build artifacts (NuGet packages, executables, container images, BOM)"));

    AnsiConsole.Write(
        new Table()
            .AddColumns("Option", "Description")
            .AddRow("[yellow]--configuration=<value>[/]", "The build configuration to use (default: Debug for local builds, Release for CI builds)")
            .AddRow("[yellow]--clean[/]", "Cleans up artifacts before building")
            .AddRow("[yellow]--no-tests[/]", "Skips running tests")
            .AddRow("[yellow]--skip-tests[/]", "Skips running tests")
            .AddRow("[yellow]--ci[/]", "Forces the build to run in CI mode")
            .AddRow("[yellow]--build-counter=<value>[/]", "The build counter value (default: 0)")
            .AddRow("[yellow]--container-registry=<value>[/]", "The container registry to use when publishing container images")
            .AddRow("[yellow]--github-username=<value>[/]", "The GitHub username for Bill of Materials generation")
            .AddRow("[yellow]--github-token=<value>[/]", "The GitHub personal access token for Bill of Materials generation")
            .AddRow("[yellow]--property=<value>[/]", "A custom MSBuild property in NAME=VALUE format (can be specified multiple times)")
        );

    Environment.Exit(1);
}

// Validate that the target can be run in the current environment.
if (!ciBuild && preparingRelease) {
    AnsiConsole.MarkupLine("[red]This target can only be run in a CI environment.[/]");
    Environment.Exit(1);
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup<BuildData>(context => {
    var buildCounter = Argument("build-counter", 0);
    
    var version = MinVer(settings => settings
        .WithTagPrefix("v")
        .WithVerbosity(MinVerVerbosity.Info)
    );
    
    Environment.SetEnvironmentVariable("ContinuousIntegrationBuild", ciBuild ? "true" : "false");
    Environment.SetEnvironmentVariable("MinVerVersionOverride", version);
    Environment.SetEnvironmentVariable("CAKE_BUILD_COUNTER", buildCounter.ToString());
    
    // We expect each custom build property to be in "NAME=VALUE" format.
    var buildPropRegex = new System.Text.RegularExpressions.Regex(@"^(?<name>.+)=(?<value>.+)$");
    
    IReadOnlyDictionary<string, string> buildProps = Arguments<string>("property", []).Select(x => {
        var m = buildPropRegex.Match(x);
        if (!m.Success) {
            return null;
        }

        return new { name = m.Groups["name"].Value, value = m.Groups["value"].Value };
    }).Where(x => x != null).ToImmutableDictionary(x => x!.name, x => x!.value);
    
    var data = new BuildData(
        new ProjectData(
            "NRuuviTag.slnx",
            ContainerProjects: ["src/NRuuviTag.Cli.Linux/NRuuviTag.Cli.Linux.csproj"]),
        BuildVersion: version,
        BuildCounter: buildCounter,
        IsContinuousIntegrationBuild: ciBuild,
        Configuration: Argument("configuration", "Release"),
        Clean: preparingRelease || HasArgument("clean"),
        SkipTests: HasArgument("no-tests") || HasArgument("skip-tests"),
        ContainerRegistry: Argument("container-registry", ""),
        GitHubCredentials: HasArgument("github-username")
            ? new GitHubCredentials(
                Argument("github-username", ""),
                Argument("github-token", ""))
            : null,
        BuildProperties: buildProps);
    
    Information(System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions {
        WriteIndented = true
    }));
    
    return data;
}); 

Task("Clean")
    .WithCriteria<BuildData>(data => data.Clean)
    .Does<BuildData>(data => {
        CleanDirectory($"./artifacts");
        DotNetClean(data.Projects.SolutionFile);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does<BuildData>(data => {
        DotNetRestore(data.Projects.SolutionFile);
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does<BuildData>(data => {
        DotNetBuild(
            data.Projects.SolutionFile,
            new DotNetBuildSettings {
                Configuration = data.Configuration,
            }.ApplyBuildProperties(data.BuildProperties));
    });

Task("Test")
    .WithCriteria<BuildData>(data => !data.SkipTests)
    .IsDependentOn("Build")
    .Does<BuildData>(data => {
        DotNetTest(
            data.Projects.SolutionFile,
            new DotNetTestSettings {
                Configuration = data.Configuration,
                NoBuild = true
            }.ApplyBuildProperties(data.BuildProperties));
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does<BuildData>(data => {
        DotNetPack(
            data.Projects.SolutionFile,
            new DotNetPackSettings {
                Configuration = data.Configuration,
                OutputDirectory = $"./artifacts/packages/{data.Configuration}",
                NoBuild = true
            }.ApplyBuildProperties(data.BuildProperties));
    });

Task("PublishExe")
    .IsDependentOn("Test")
    .DoesForEach<BuildData, FilePath>(GetFiles("**/*.*proj"), (data, projectFile) => {
        var projectDir = projectFile.GetDirectory();
        foreach (var publishProfileFile in GetFiles(projectDir.FullPath + "/**/*.pubxml")) {
            var buildSettings = new DotNetPublishSettings {
                Configuration = data.Configuration,
                MSBuildSettings = new DotNetMSBuildSettings().WithProperty("PublishProfile", [publishProfileFile.FullPath])
            }.ApplyBuildProperties(data.BuildProperties);
            
            DotNetPublish(
                projectFile.FullPath, 
                buildSettings);
        }
    });

Task("PublishContainer")
    .WithCriteria<BuildData>(data => data.Projects.ContainerProjects is { Count: > 0 })
    .IsDependentOn("Test")
    .DoesForEach<BuildData, FilePath>(GetFiles("**/*.*proj"), (data, projectFile) => {
        var isContainerProject = data.Projects.ContainerProjects!.Any(x => {
            var containerProjectFile = MakeAbsolute(File(x));
            return containerProjectFile.Equals(projectFile);
        });

        if (!isContainerProject) {
            return;
        }
        
        var containerBuildSettings = new DotNetPublishSettings {
            Configuration = data.Configuration,
            MSBuildSettings = new DotNetMSBuildSettings().WithTarget("PublishContainer")
        };
        
        if (!string.IsNullOrWhiteSpace(data.ContainerRegistry)) {
            containerBuildSettings.MSBuildSettings.WithProperty("ContainerRegistry", data.ContainerRegistry);
        }
            
        DotNetPublish(
            projectFile.FullPath, 
            containerBuildSettings.ApplyBuildProperties(data.BuildProperties));
    });

Task("BillOfMaterials")
    .IsDependentOn("Clean")
    .Does<BuildData>(data => {
        var cycloneDx = Context.Tools.Resolve(IsRunningOnWindows()
            ? "dotnet-CycloneDX.exe"
            : "dotnet-CycloneDX");

        var githubUser = data.GitHubCredentials?.Username;
        var githubToken = data.GitHubCredentials?.Token;

        if (!string.IsNullOrWhiteSpace(githubUser) && string.IsNullOrWhiteSpace(githubToken)) {
            throw new InvalidOperationException("When specifying a GitHub username for Bill of Materials generation you must also specify a personal access token using the '--github-token' argument.");
        }

        if (string.IsNullOrWhiteSpace(githubUser) && !string.IsNullOrWhiteSpace(githubToken)) {
            throw new InvalidOperationException("When specifying a GitHub personal access token for Bill of Materials generation you must also specify the username for the token using the '--github-username' argument.");
        }

        var cycloneDxArgs = new ProcessArgumentBuilder()
            .Append(data.Projects.SolutionFile)
            .Append("-o")
            .Append($"./artifacts/bom/{data.Configuration}")
            .Append("-F")
            .Append("Json");

        if (!string.IsNullOrWhiteSpace(githubUser)) {
            cycloneDxArgs.Append("-egl"); // Enable GitHub licence resolution.
            cycloneDxArgs.Append("-gu").Append(githubUser);
        }

        if (!string.IsNullOrWhiteSpace(githubToken)) {
            cycloneDxArgs.Append("-gt").Append(githubToken);
        }

        StartProcess(cycloneDx, new ProcessSettings {
            Arguments = cycloneDxArgs
        });
    });

Task("PrepareRelease")
    .WithCriteria<BuildData>(data => data.IsContinuousIntegrationBuild)
    .IsDependentOn("Pack")
    .IsDependentOn("PublishExe")
    .IsDependentOn("PublishContainer")
    .IsDependentOn("BillOfMaterials")
    .Does(() => { });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target); 

//////////////////////////////////////////////////////////////////////
// TYPES
//////////////////////////////////////////////////////////////////////

public record BuildData(
    [property: System.Text.Json.Serialization.JsonIgnore] ProjectData Projects,
    string BuildVersion,
    int BuildCounter = 0,
    bool IsContinuousIntegrationBuild = false,
    string Configuration = "Release",
    bool Clean = false,
    bool SkipTests = false,
    string? ContainerRegistry = null,
    [property: System.Text.Json.Serialization.JsonIgnore] GitHubCredentials? GitHubCredentials = null,
    IReadOnlyDictionary<string, string>? BuildProperties = null);


public record ProjectData(
    string SolutionFile, 
    IReadOnlyList<string>? ContainerProjects = null);


public record GitHubCredentials(
    string Username,
    string Token);


public static class BuildExtensions {

    private static void ApplyBuildProperties(DotNetMSBuildSettings settings, IReadOnlyDictionary<string, string>? properties) {
        if (properties is null) {
            return;
        }
            
        foreach (var item in properties) {
            settings.WithProperty(item.Key, [item.Value]);
        }
    }
    

    extension(DotNetBuildSettings settings) {

        public DotNetBuildSettings ApplyBuildProperties(IReadOnlyDictionary<string, string>? properties) {
            if (properties is null) {
                return settings;
            }
            
            settings.MSBuildSettings ??= new DotNetMSBuildSettings();
            ApplyBuildProperties(settings.MSBuildSettings, properties);
            
            return settings;
        }

    }
    
    
    extension(DotNetTestSettings settings) {

        public DotNetTestSettings ApplyBuildProperties(IReadOnlyDictionary<string, string>? properties) {
            if (properties is null) {
                return settings;
            }
            
            settings.MSBuildSettings ??= new DotNetMSBuildSettings();
            ApplyBuildProperties(settings.MSBuildSettings, properties);
            
            return settings;
        }

    }
    
    
    extension(DotNetPackSettings settings) {

        public DotNetPackSettings ApplyBuildProperties(IReadOnlyDictionary<string, string>? properties) {
            if (properties is null) {
                return settings;
            }
            
            settings.MSBuildSettings ??= new DotNetMSBuildSettings();
            ApplyBuildProperties(settings.MSBuildSettings, properties);
            
            return settings;
        }

    }
    
    
    extension(DotNetPublishSettings settings) {

        public DotNetPublishSettings ApplyBuildProperties(IReadOnlyDictionary<string, string>? properties) {
            if (properties is null) {
                return settings;
            }
            
            settings.MSBuildSettings ??= new DotNetMSBuildSettings();
            ApplyBuildProperties(settings.MSBuildSettings, properties);
            
            return settings;
        }

    }
    

}
