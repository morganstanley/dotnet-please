<!-- Morgan Stanley makes this available to you under the Apache License, Version 2.0 (the "License"). You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. See the NOTICE file distributed with this work for additional information regarding copyright ownership. Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License. -->
# dotnet-please

![Lifecycle Active](https://badgen.net/badge/Lifecycle/Active/green)
[![Continuous](https://github.com/morganstanley/dotnet-please/actions/workflows/continuous.yml/badge.svg)](https://github.com/morganstanley/dotnet-please/actions/workflows/continuous.yml)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/morganstanley/dotnet-please/badge)](https://securityscorecards.dev/viewer/?uri=github.com/morganstanley/dotnet-please)

This CLI tool aims to streamline some repetitive tasks around Visual Studio 
projects and solutions, including renaming multiple projects, extracting
NuGet package versions into properties, and many more.

Note: Although some commands might work with the legacy project file format,
the tool is meant for SDK-style project files.

## Installation

To install `please` as a global dotnet tool:
```console
dotnet tool install -g MorganStanley.DotNetPlease
```

Alternatively, you can just clone the repo and run the `build-and-install.ps1` script.

If you install dotnet tools to a custom folder, either pass 
the folder to the script, or create an environment variable called `DotNetToolsPath`.

Then to get the list of available commands:

```console
please --help
```

### Installing on Ubuntu Linux

This project uses MSBuild assemblies from Microsoft that don't seem to work when the
.NET SDK is installed using Snap. If you encounter errors like this one:
```

```
...then try installing the SDK from the official Ubuntu package feed ([instructions](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#supported-distributions))

## Usage

Open a command prompt, navigate to your solution's root directory, and start
asking things. Commands are normally kebab-cased, but the dashes can be 
replaced with spaces to make them more readable.

To get a complete list of options and arguments for each command, run
```console
please --help
```
or
```console
please <command> --help
```

### --dry-run

Most commands have a `--dry-run` option to just list what the command would do,
but we strongly advise to back up/commit your code before pleasing your projects.

### --workspace

This tool mostly works on project and solution files. To specify which files to work on,
use the `--workspace` option. This option accepts any of the following:

|Value|Behavior|
|--------|--------|
|(empty)|Discover the projects automatically|
|Single solution file (.sln)|Work with projects in the solution|
|Single project file (.csproj, .fsproj, etc.)|Work with a single project|
|Globbing pattern|Work with multiple project and solution files|

When the workspace is not provided, `please` will try the following, in this order:
- A standalone solution file in the working directory or any of its parent directories
- A standalone project file in the working directory or any of its parent directories
- Searhc for all project files in the working directory, recursively.

### Consolidate NuGet packages

#### Basic usage

Consolidate all NuGet package references in the solution to the highest version used:

```console
please consolidate packages
```

Limit the command to specific packages:

```console
please consolidate packages --package Microsoft.Extensions.*
```

Set a specific version number (must be used with `--package`):

```console
please consolidate packages --package Microsoft.Extensions.* --version 3.1.3
```

#### Keeping package versions in a central .props file

When working with large solutions with lots of projects, some teams choose to keep
their dependencies in a separate file (let's say Dependencies.props), typically
imported in a Directory.Build.props file:

Dependencies.props:
```xml
<Project>
    <PropertyGroup>
        <xunitVersion>2.4.1</xunitVersion>
    </PropertyGroup>
</Project>
```

Directory.Build.props:

```xml
<Project>
    <Import Project="Dependencies.props" />
</Project>
```

In the project file:
```xml
<ItemGroup>
    <PackageReference Include="xunit" Version="$(xunitVersion)" />
<ItemGroup>
```

Use `please` to update all package references and the .props file:

```console
please consolidate packages --props Dependencies.props
```

`please` will add the missing properties to your .props file and replace package 
versions with property names in the project files. You can still update packages
using NuGet CLI or Visual Studio. When you're done, just run the command again
and your references will be consistent.

In case you want to back out from using a .props file, use the `--explicit` option
to replace the property names with actual versions:

```console
please consolidate packages --explicit
```

### Manage package versions centrally

A set of commands are continually added to support centrally managed NuGet package versions
(see https://github.com/NuGet/Home/wiki/Centrally-managing-NuGet-package-versions).

#### Move explicit versions to a central file

Use the `pull-package-versions` command to pull explicit package versions from projects into
a central packages file.

```console
please pull package versions Dependencies.props
```

This command will 
1. scan all the projects in the solution, extract and remove any explicit `Version` attributes from
`PackageReference` items
2. update the specified `.props` file, add the missing `PackageVersion` items with the extracted versions.

When the file name is omitted, `dotnet-please` will try to find a `Directory.Packages.props` file
in the directory tree (to conform with the original NuGet proposal).

It is also possible to update the existing `PackageVersion` items if some projects reference newer versions:

```console
please pull package versions --update
```

You can also move back the version attributes to the project files:

```
please restore package versions
```

### Move/rename projects

To rename or move a project while fixing the solution file and any project references:

```console
please move project Old/Path/OldProjectName.csproj New/Path/NewProjectName.csproj
```

To just rename a project (by renaming the .csproj file and its directory):

```console
please move project OldProjectName NewProjectName
```

You can also rename projects in bulk (eg. when trying to change the root namespace of
your 100-project solution):

```console
please change namespace Old.Namespace New.Namespace
```

### Fix broken project references

Having a hard time fixing a broken solution after moving projects manually?

```console
please fix project references
```

The tool will try to fix any broken reference by looking for a project
with the same file name (but at a different relative path), and remove the reference 
if that fails.

### Clean up leftover files and projects

To remove any code files that were explicitly excluded with a `<Complile Remove="..."/>` item:

```console
please cleanup project files
```

This will remove the file AND the `Compile` item.

Use the `--allow-globs` option to remove files that were excluded with a globbing pattern.

To list projects that are not included in the solution, run:

```console
please find stray projects
```

### Convert package and assembly references to project references

This is useful when debugging and editing a library from within the consuming project.
The below command will find any `PackageReference` and `Reference` items that refer to
a project in `Utility.sln` and replace them with a `ProjectReference`. It will also add
the referenced projects to the current solution.

```console
please expand references Path/To/Utility.sln
```

### Remove junk from the solution directory

Delete those magic folders after a table-flipping Visual Studio experience:

```console
please remove junk --bin --suo --testStore
```

(or just use those options individually)

### Change the `PATH` variable

To quickly append the working directory to the user's `PATH` variable, run

```console
please add to path
```

Conversely, you can remove the working directory from `PATH`:

```console
please remove from path
```

You can also specify the directory to add or remove, relative to the working directory:

```console
please add to path some/relative/path
```

### Evaluate MSBuild properties

This can be useful when troubleshooting build errors. To load, evaluate and list all the properties  
in a project file with their unevaluated and evaluated values:

```console
please evaluate props
```
