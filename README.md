# C# Repository Template

Repository template for a C# project.


# Getting Started

- Create a new repository on GitHub and choose this repository as the template, or click on the _"Use this template"_ button on the repository home page.
- Rename the solution file in the root of the repository ([RENAME-ME.sln](/RENAME-ME.sln)).
- Update [Directory.Build.props](/Directory.Build.props) in the root folder and replace the placeholder values in the shared project properties (e.g. `{{COPYRIGHT_START_YEAR}}`).
- Update [build.cake](/build.cake) in the root folder and replace the `DefaultSolutionName` constant at the start of the file with the name of your solution file.
- Create new library and application projects in the `src` folder.
- Create test and benchmarking projects in the `test` folder.
- Create example projects that demonstrate the library and application projects in the `samples` folder.


# Repository Structure

The repository is organised as follows:

- `[root]`
  - `.editorconfig` - Code style rules (see [here](https://editorconfig.org/) for details).
  - `.gitattributes`
  - `.gitignore`
  - `build.cake` - [Cake](https://cakebuild.net/) script for building the projects.
  - `build.ps1` - PowerShell script to bootstrap and run the Cake script.
  - `Directory.Build.props` - Common MSBuild properties and targets (see [here](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build) for details).
  - `Directory.Build.targets` - Common MSBuild properties and targets (see [here](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build) for details). 
  - `README.md`
  - `RENAME-ME.sln` - Visual Studio solution file.
  - `[build]` - Resources for building the solution.
    - `build-state.cake` - Additional build script.
    - `build-utilities.cake` - Additional build script.
    - `Dependencies.props` - Common NuGet package versions.
    - `NetFX.targets` - Adds package references for building projects that target .NET Framework on non-Windows systems.
    - `version.json` - Defines version numbers used when building the projects.
  - `[samples]` - Example projects to demonstrate the usage of the repository libraries and applications.
    - `Directory.Build.props` - Common MSBuild properties and targets related to example projects (see [here](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build) for details).
  - `[src]` - Source code for repository libraries and applications.
  - `[test]` - Test and benchmarking projects.
    - `Directory.Build.props` - Common MSBuild properties and targets related to test projects (see [here](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build) for details).


# Building the Solution

The repository uses [Cake](https://cakebuild.net/) for cross-platform build automation. The build script allows for metadata such as a build counter to be specified when called by a continuous integration system such as TeamCity.

A build can be run from the command line using the [build.ps1](/build.ps1) PowerShell script. For documentation about the available build script parameters, see [build.cake](/build.cake).
