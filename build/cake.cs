#:sdk Cake.Sdk@6.0.0

var target = Argument("target", "Test");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup<BuildData>(context => {
    var ciBuild = !BuildSystem.IsLocalBuild || HasArgument("ci");
    var buildCounter = Argument("build-counter", 0);
    
    Environment.SetEnvironmentVariable("ContinuousIntegrationBuild", ciBuild ? "true" : "false");
    Environment.SetEnvironmentVariable("CAKE_BUILD_COUNTER", buildCounter.ToString());
    
    return new BuildData(
        new ProjectData(
            "NRuuviTag.slnx",
            ContainerProjects: ["src/NRuuviTag.Cli.Linux/NRuuviTag.Cli.Linux.csproj"]),
        IsContinuousIntegrationBuild: ciBuild,
        BuildCounter: buildCounter,
        Configuration: Argument("configuration", "Release"),
        Clean: HasArgument("clean"),
        SkipTests: HasArgument("no-tests") || HasArgument("skip-tests"),
        ContainerRegistry: Argument("container-registry", ""),
        GitHubCredentials: HasArgument("github-username")
            ? new GitHubCredentials(
                Argument("github-username", ""),
                Argument("github-token", ""))
            : null);
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

Task("Publish")
    .IsDependentOn("Test")
    .DoesForEach<BuildData, FilePath>(GetFiles("**/*.*proj"), (data, projectFile) => {
        // Publish using publish profiles
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
        
        // Publish as container
        if (data.Projects.ContainerProjects is null or { Count: 0 }) {
            return;
        }

        var isContainerProject = data.Projects.ContainerProjects.Any(x => {
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
    ProjectData Projects,
    bool IsContinuousIntegrationBuild = false,
    int BuildCounter = 0,
    string Configuration = "Release",
    bool Clean = false,
    bool SkipTests = false,
    string? ContainerRegistry = null,
    GitHubCredentials? GitHubCredentials = null);


public record ProjectData(
    string SolutionFile, 
    IReadOnlyList<string>? ContainerProjects = null);


public record GitHubCredentials(
    string Username,
    string Token);

