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

using DotNetPlease.TestUtils;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public class ChangeNamespaceTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_renames_and_moves_projects(bool dryRun)
        {
            var projectFileNames = new List<string>()
            {
                "MorganStanley.OldNamespace.Alpha/MorganStanley.OldNamespace.Alpha.csproj",
                "Bravo/MorganStanley.OldNamespace.Bravo.csproj",
                "MorganStanley.OldNamespace.Charlie/Charlie.csproj",
            };
            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                CreateProject(projectFileName);
            }

            if (dryRun) CreateSnapshot();

            await RunAndAssertSuccess("change-namespace", "MorganStanley.OldNamespace", "NewNamespace", DryRunOption(dryRun));

            if (dryRun)
            {
                VerifySnapshot();
                return;
            }

            var newProjectFileNames = GetProjectsFromDirectory(WorkingDirectory, recursive: true)
                .Select(GetRelativePath)
                .ToList();
            newProjectFileNames.Should()
                .BeEquivalentTo(
                    new List<string>
                    {
                        "NewNamespace.Alpha/NewNamespace.Alpha.csproj",
                        "Bravo/NewNamespace.Bravo.csproj",
                        "MorganStanley.OldNamespace.Charlie/Charlie.csproj" // this should not be renamed as the file name is what counts
                    },
                    opt => opt.CompareAsFileName());

        }

        [Theory, CombinatorialData]
        public async Task It_replaces_the_project_in_the_solution_file(bool dryRun)
        {
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);

            var projectFileNames = new List<string>()
            {
                "MorganStanley.OldNamespace.Alpha/MorganStanley.OldNamespace.Alpha.csproj",
                "Bravo/MorganStanley.OldNamespace.Bravo.csproj",
                "MorganStanley.OldNamespace.Charlie/Charlie.csproj",
            };

            foreach (var projectFileName in projectFileNames.Select(GetFullPath))
            {
                CreateProject(projectFileName);
                AddProjectToSolution(projectFileName, solutionFileName);
            }

            if (dryRun) CreateSnapshot();

            await RunAndAssertSuccess("change-namespace", "MorganStanley.OldNamespace", "NewNamespace", DryRunOption(dryRun));

            if (dryRun)
            {
                VerifySnapshot();
                return;
            }

            var newProjectFileNames = GetProjectsFromDirectory(WorkingDirectory, recursive: true)
                .Select(GetRelativePath)
                .ToList();

            newProjectFileNames.Should()
                .BeEquivalentTo(
                    new List<string>
                    {
                        "NewNamespace.Alpha/NewNamespace.Alpha.csproj",
                        "Bravo/NewNamespace.Bravo.csproj",
                        "MorganStanley.OldNamespace.Charlie/Charlie.csproj" // this should not be renamed as the file name is what counts
                    },
                    opt => opt.CompareAsFileName());
        }

        public ChangeNamespaceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}