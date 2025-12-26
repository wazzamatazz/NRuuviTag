#:sdk Cake.Sdk@6.0.0
#:package Cake.MinVer

InstallTool("dotnet:https://api.nuget.org/v3/index.json?package=minver-cli&version=6.0.0");
InstallTool("dotnet:https://api.nuget.org/v3/index.json?package=CycloneDX&version=5.5.0");

var target = Argument("target", "Test");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup<BuildData>(context => {
    var ciBuild = !BuildSystem.IsLocalBuild || HasArgument("ci");
    var buildCounter = Argument("build-counter", 0);
    
    var version = MinVer(settings => settings
        .WithTagPrefix("v")
        .WithVerbosity(MinVerVerbosity.Info)
    );
    
    Environment.SetEnvironmentVariable("ContinuousIntegrationBuild", ciBuild ? "true" : "false");
    Environment.SetEnvironmentVariable("MinVerVersionOverride", version);
    Environment.SetEnvironmentVariable("CAKE_BUILD_COUNTER", buildCounter.ToString());
    
    var data = new BuildData(
        new ProjectData(
            "NRuuviTag.slnx",
            ContainerProjects: ["src/NRuuviTag.Cli.Linux/NRuuviTag.Cli.Linux.csproj"]),
        BuildVersion: version,
        BuildCounter: buildCounter,
        IsContinuousIntegrationBuild: ciBuild,
        Configuration: Argument("configuration", "Release"),
        Clean: HasArgument("clean"),
        SkipTests: HasArgument("no-tests") || HasArgument("skip-tests"),
        ContainerRegistry: Argument("container-registry", ""),
        GitHubCredentials: HasArgument("github-username")
            ? new GitHubCredentials(
                Argument("github-username", ""),
                Argument("github-token", ""))
            : null);
    
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
            });
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
            });
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
            });
    });

Task("PublishExe")
    .IsDependentOn("Test")
    .DoesForEach<BuildData, FilePath>(GetFiles("**/*.*proj"), (data, projectFile) => {
        var projectDir = projectFile.GetDirectory();
        foreach (var publishProfileFile in GetFiles(projectDir.FullPath + "/**/*.pubxml")) {
            var buildSettings = new DotNetPublishSettings {
                Configuration = data.Configuration,
                MSBuildSettings = new DotNetMSBuildSettings().WithProperty("PublishProfile", [publishProfileFile.FullPath])
            };
            
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
            containerBuildSettings);
    });

Task("Publish")
    .IsDependentOn("PublishExe")
    .IsDependentOn("PublishContainer")
    .Does(() => { });

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

Task("CreateArtifacts")
    .IsDependentOn("Pack")
    .IsDependentOn("Publish")
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
    [property: System.Text.Json.Serialization.JsonIgnore] GitHubCredentials? GitHubCredentials = null);


public record ProjectData(
    string SolutionFile, 
    IReadOnlyList<string>? ContainerProjects = null);


public record GitHubCredentials(
    string Username,
    string Token);

