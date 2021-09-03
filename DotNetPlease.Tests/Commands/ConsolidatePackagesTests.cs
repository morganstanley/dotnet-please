/*
 * Morgan Stanley makes this available to you under the Apache License,
 * Version 2.0 (the "License"). You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0.
 *
 * See the NOTICE file distributed with this work for additional information
 * regarding copyright ownership. Unless required by applicable law or agreed
 * to in writing, software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
 * or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using FluentAssertions;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;


namespace DotNetPlease.Commands
{
    public class ConsolidatePackagesTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_updates_packages_to_the_highest_version(bool stage)
        {
            var projectFileNames = new List<string>
            {
                "Project1/Project1.csproj",
                "Project2/Project2.csproj",
                "Project3/Project3.csproj"
            };

            var version = new NuGetVersion(1, 0, 0);
            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                CreateProject(projectFileName);
                version = new NuGetVersion(version.Major, version.Minor + 1, 0);
                AddPackageReference(projectFileName, "Example.Package", version.ToString());
            }

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("consolidate-packages", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                var project = LoadProjectFromFile(projectFileName);
                var packageReference = FindPackageReference(project, "Example.Package");
                packageReference.Should().NotBeNull();
                packageReference!.GetMetadataValue("Version").Should().Be(version.ToString());
            }
        }

        [Theory, CombinatorialData]
        public async Task It_replaces_Version_with_property_name_when_props_file_is_provided(bool stage)
        {
            var projectFileNames = new List<string>
            {
                "Project1/Project1.csproj",
                "Project2/Project2.csproj",
                "Project3/Project3.csproj"
            };

            var propsFileName = GetFullPath("Dependencies.props");
            var props = new Project();
            props.SetProperty("ExamplePackageVersion", "2.0.0");
            props.Save(propsFileName);

            CreateDirectoryBuildProps("Dependencies.props");

            var version = new NuGetVersion(1, 0, 0);
            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                CreateProject(projectFileName);
                version = new NuGetVersion(version.Major, version.Minor + 1, 0);
                AddPackageReference(projectFileName, "Example.Package", version.ToString());
            }

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("consolidate-packages", "--props", "Dependencies.props", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                var project = LoadProjectFromFile(projectFileName);
                var packageReference = FindPackageReference(project, "Example.Package");
                packageReference.Should().NotBeNull();
                var versionAttribute = packageReference!.Xml.Metadata.FirstOrDefault(x => x.Name == "Version");
                versionAttribute.Should().NotBeNull();
                versionAttribute!.Value.Should().Be("$(ExamplePackageVersion)");
            }

            props = LoadProjectFromFile(propsFileName);
            props.GetPropertyValue("ExamplePackageVersion").Should().Be("1.3.0");
        }

        [Theory, CombinatorialData]
        public async Task It_adds_new_property_if_needed(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, "Example.Package", "1.2.3");

            var propsFileName = GetFullPath("Dependencies.props");
            var props = new Project();
            props.Save(propsFileName);

            CreateDirectoryBuildProps("Dependencies.props");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("consolidate-packages", "--props", "Dependencies.props", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            props = LoadProjectFromFile(propsFileName);
            props.GetPropertyValue("ExamplePackageVersion").Should().Be("1.2.3");
        }

        [Theory, CombinatorialData]
        public async Task It_keeps_Version_attributes_that_contain_an_expression(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, "Example.Package", "$(DefinedVersion)");

            var propsFileName = GetFullPath("Dependencies.props");
            var props = new Project();
            props.Save(propsFileName);

            CreateDirectoryBuildProps("Dependencies.props");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("consolidate-packages", "--props", "Dependencies.props", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var packageReference = FindPackageReference(projectFileName, "Example.Package");
            packageReference.Should().NotBeNull();
            packageReference!.GetUnevaluatedMetadataValue("Version").Should().Be("$(DefinedVersion)");
        }

        [Theory, CombinatorialData]
        public async Task It_reverts_to_version_numbers_when_explicit_is_defined(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, "Example.Package", "$(ExamplePackageVersion)");

            var propsFileName = GetFullPath("Dependencies.props");
            var props = new Project();
            props.SetProperty("ExamplePackageVersion", "1.2.3");
            props.Save(propsFileName);

            CreateDirectoryBuildProps("Dependencies.props");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("consolidate-packages", "--explicit", "--force", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var packageReference = FindPackageReference(projectFileName, "Example.Package");
            packageReference.Should().NotBeNull();
            packageReference!.GetUnevaluatedMetadataValue("Version").Should().Be("1.2.3");
        }

        private void CreateDirectoryBuildProps(params string[] imports)
        {
            var fileName = GetFullPath("Directory.Build.props");
            var project = new Project();
            foreach (var import in imports)
            {
                project.Xml.AddImport(import);
            }
            project.Save(fileName);
        }

        public ConsolidatePackagesTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}