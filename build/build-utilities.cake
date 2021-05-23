// Miscellaneous build utilities.
public static class BuildUtilities {

    // Informs the build system of the build number that is being used.
    public static void SetBuildSystemBuildNumber(BuildSystem buildSystem, BuildState buildState) {
        // Tell TeamCity the build number if required.
        if (buildSystem.IsRunningOnTeamCity) {
            buildSystem.TeamCity.SetBuildNumber(buildState.BuildNumber);
        }
    }


    // Writes a log message.
    public static void WriteLogMessage(BuildSystem buildSystem, string message, bool newlineBeforeMessage = true) {
        if (buildSystem.IsRunningOnTeamCity) {
            buildSystem.TeamCity.WriteProgressMessage(message);
        }
        else {
            if (newlineBeforeMessage) {
                Console.WriteLine();
            }
            Console.WriteLine(message);
        }
    }


    // Writes a task started message.
    public static void WriteTaskStartMessage(BuildSystem buildSystem, string description) {
        if (buildSystem.IsRunningOnTeamCity) {
            buildSystem.TeamCity.WriteStartBuildBlock(description);
        }
    }


    // Writes a task completed message.
    public static void WriteTaskEndMessage(BuildSystem buildSystem, string description) {
        if (buildSystem.IsRunningOnTeamCity) {
            buildSystem.TeamCity.WriteEndBuildBlock(description);
        }
    }


    // Writes the specified build state to the log.
    public static void WriteBuildStateToLog(BuildSystem buildSystem, BuildState state) {
        WriteLogMessage(buildSystem, $"Solution Name: {state.SolutionName}", true);
        WriteLogMessage(buildSystem, $"Build Number: {state.BuildNumber}", false);
        WriteLogMessage(buildSystem, $"Target: {state.Target}", false);
        WriteLogMessage(buildSystem, $"Configuration: {state.Configuration}", false);
        WriteLogMessage(buildSystem, $"Clean: {state.RunCleanTarget}", false);
        WriteLogMessage(buildSystem, $"Skip Tests: {state.SkipTests}", false);
        WriteLogMessage(buildSystem, $"Continuous Integration Build: {state.ContinuousIntegrationBuild}", false);
        WriteLogMessage(buildSystem, $"Sign Output: {state.CanSignOutput}", false);
    }


    // Adds MSBuild properties from the build state.
    public static void ApplyMSBuildProperties(DotNetCoreMSBuildSettings settings, BuildState state) {
        if (state.MSBuildProperties?.Count > 0) {
            // We expect each property to be in "NAME=VALUE" format.
            var regex = new System.Text.RegularExpressions.Regex(@"^(?<name>.+)=(?<value>.+)$");

            foreach (var prop in state.MSBuildProperties) {
                if (string.IsNullOrWhiteSpace(prop)) {
                    continue;
                }

                var m = regex.Match(prop.Trim());
                if (!m.Success) {
                    continue;
                }

                settings.Properties[m.Groups["name"].Value] = new List<string> { m.Groups["value"].Value };
            }
        }

        // Specify if this is a CI build. 
        if (state.ContinuousIntegrationBuild) {
            settings.Properties["ContinuousIntegrationBuild"] = new List<string> { "True" };
        }

        // Specify if we are signing DLLs and NuGet packages.
        if (state.CanSignOutput) {
            settings.Properties["SignOutput"] = new List<string> { "True" };
        }
    }


    // Imports test results into the build system.
    public static void ImportTestResults(BuildSystem buildSystem, string testProvider, FilePath resultsFile) {
        if (resultsFile == null) {
            return;
        }

        if (buildSystem.IsRunningOnTeamCity) {
            buildSystem.TeamCity.ImportData(testProvider, resultsFile);
        }
    }

}
