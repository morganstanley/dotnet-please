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

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using DotNetPlease.Constants;
using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public class PullPackageVersionsTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_moves_package_versions_to_the_central_file(
            [CombinatorialValues(VersionSpec.Same, VersionSpec.Expression)] VersionSpec versionSpec,
            bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            var packageName = "Example.Package";
            var referencedVersion = versionSpec switch
            {
                VersionSpec.Same => "1.2.3",
                VersionSpec.Expression => "$(VersionExpression)",
                _ => throw new ArgumentOutOfRangeException(nameof(versionSpec), versionSpec, null)
            };
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, packageName, referencedVersion);

            var packageVersionsFileName = GetFullPath("Dependencies.props");
            File.WriteAllText(
                packageVersionsFileName,
                $@"
                <Project>
                </Project>
            ");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "pull-package-versions",
                "Dependencies.props",
                "Project1/Project1.csproj",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var shouldMoveVersion = versionSpec != VersionSpec.Expression;
            var project = LoadProjectFromFile(projectFileName);
            var packageVersions = LoadProjectFromFile(packageVersionsFileName);

            if (shouldMoveVersion)
            {
                project.Xml.Items
                    .Single(i => i.ItemType == "PackageReference" && i.Include == packageName)
                    .GetMetadataValue("Version")
                    .Should().BeNull();

                packageVersions.Xml.Items
                    .Single(i => i.ItemType == "PackageVersion" && i.Include == packageName)
                    .Metadata
                    .Single(m => m.Name == "Version")
                    .Value
                    .Should().Be(referencedVersion);
            }
            else
            {
                project.Xml.Items
                    .Single(i => i.ItemType == "PackageReference" && i.Include == packageName)
                    .GetMetadataValue("Version")
                    .Should().Be(referencedVersion);

                packageVersions.Xml.Items
                    .Where(i => i.ItemType == "PackageVersion" && i.Include == packageName)
                    .Should().BeEmpty();
            }
        }

        [Theory]
        [CombinatorialData]
        public async Task It_updates_the_central_version_if_needed(
            [CombinatorialValues(VersionSpec.Same, VersionSpec.Expression)]
            VersionSpec centralVersionSpec,
            VersionSpec referencedVersionSpec,
            bool update,
            bool stage)
        {
            var centralVersion = centralVersionSpec switch
            {
                VersionSpec.Same => "1.1.1",
                VersionSpec.Expression => "$(ExamplePackageVersion)",
                _ => throw new ArgumentOutOfRangeException(nameof(centralVersionSpec), centralVersionSpec, null)
            };
            var referencedVersion = referencedVersionSpec switch
            {
                VersionSpec.Same => centralVersion,
                VersionSpec.Lower => "1.0.0",
                VersionSpec.Higher => "1.2.3",
                VersionSpec.Expression => "$(VersionExpression)",
                _ => throw new ArgumentOutOfRangeException(nameof(referencedVersionSpec), referencedVersionSpec, null)
            };

            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, "Example.Package", referencedVersion);

            var packageVersionsFileName = GetFullPath("Dependencies.props");
            File.WriteAllText(
                packageVersionsFileName,
                $@"
                <Project>
                    <ItemGroup>
                        <PackageVersion Include=""Example.Package"" Version=""{centralVersion}"" />
                    </ItemGroup>
                </Project>
            ");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "pull-package-versions",
                "Dependencies.props",
                "Project1/Project1.csproj",
                update ? "--update" : "",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var expectedVersion =
                update
                && centralVersionSpec != VersionSpec.Expression
                && referencedVersionSpec == VersionSpec.Higher
                    ? referencedVersion
                    : centralVersion;

            var packageVersions = LoadProjectFromFile(packageVersionsFileName);
            packageVersions.Xml.Items
                .Single(i => i.ItemType == "PackageVersion" && i.Include == "Example.Package")
                .Metadata
                .Single(m => m.Name == "Version")
                .Value
                .Should().Be(expectedVersion);
        }

        public PullPackageVersionsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        public enum VersionSpec
        {
            Same,
            Lower,
            Higher,
            Expression
        }
    }
}
