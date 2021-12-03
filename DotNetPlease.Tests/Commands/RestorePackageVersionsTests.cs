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
using JetBrains.Annotations;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;
using FluentAssertions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;
using Task = System.Threading.Tasks.Task;

namespace DotNetPlease.Commands
{
    public class RestorePackageVersionsTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_restores_missing_package_versions_from_the_central_file(
            VersionSpec versionSpec,
            bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            var packageName = "Example.Package";

            CreateProject(projectFileName);
            AddPackageReference(projectFileName, packageName, version: null);

            var packageVersionsFileName = GetFullPath("Dependencies.props");
            var centralVersion = versionSpec switch
            {
                VersionSpec.Value => "1.2.3",
                VersionSpec.Expression => "$(PackageVersionExpression)",
                _ => throw new ArgumentOutOfRangeException(nameof(versionSpec), versionSpec, null)
            };
            
            File.WriteAllText(
                packageVersionsFileName,
                $@"
                <Project>
                    <ItemGroup>
                        <PackageVersion Include=""{packageName}"" Version=""{centralVersion}"" />
                    </ItemGroup>
                </Project>
            ");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "restore-package-versions",
                "Dependencies.props",
                "Project1/Project1.csproj",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var project = LoadProjectFromFile(projectFileName);

            project.Xml.Items.Single(i => i.ItemType == "PackageReference" && i.Include == packageName)
                .GetMetadataValue("Version")
                .Should()
                .Be(centralVersion);
        }

        public enum VersionSpec
        {
            Value,
            Expression
        }

        public RestorePackageVersionsTests([NotNull] ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}