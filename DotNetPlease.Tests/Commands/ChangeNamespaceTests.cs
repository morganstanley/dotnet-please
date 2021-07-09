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
        public async Task It_renames_and_moves_projects(bool stage)
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

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("change-namespace", "MorganStanley.OldNamespace", "NewNamespace", StageOption(stage));

            if (stage)
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
        public async Task It_replaces_the_project_in_the_solution_file(bool stage)
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

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("change-namespace", "MorganStanley.OldNamespace", "NewNamespace", "Test.sln", StageOption(stage));

            if (stage)
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