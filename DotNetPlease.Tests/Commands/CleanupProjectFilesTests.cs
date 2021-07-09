using FluentAssertions;
using Microsoft.Build.Globbing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public class CleanupProjectFilesTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_removes_files_that_are_excluded_with_pattern_when_AllowGlobs_is_set(bool allowGlobs, bool stage)
        {
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            var projectFileName = GetFullPath("TestProject.csproj");
            CreateProject(projectFileName);
            AddProjectToSolution(projectFileName, solutionFileName);
            var excludedFileName = GetFullPath("Excluded/Junk.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(excludedFileName));
            File.WriteAllText(excludedFileName, "{}");
            AddItemRemove(projectFileName, "Compile", "Excluded/**/*");

            var args = new List<string> { "cleanup-project-files" };
            if (allowGlobs)
            {
                args.Add("--allow-globs");
            }
            if (stage)
            {
                args.Add("--stage");
            }

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(args.ToArray());

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            File.Exists(excludedFileName).Should().Be(!allowGlobs);
        }

        [Theory, CombinatorialData]
        public async Task It_deletes_files_that_are_excluded_with_exact_filename_and_removes_the_Compile_item(bool stage)
        {
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            var projectFileName = GetFullPath("TestProject.csproj");
            CreateProject(projectFileName);
            AddProjectToSolution(projectFileName, solutionFileName);
            var excludedFileName = GetFullPath("Excluded/Junk.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(excludedFileName));
            File.WriteAllText(excludedFileName, "{}");
            AddItemRemove(projectFileName, "Compile", "Excluded/Junk.cs");

            await RunAndAssertSuccess("cleanup-project-files", StageOption(stage));

            if (stage)
            {
                File.Exists(excludedFileName).Should().BeTrue();
            }
            else
            {
                File.Exists(excludedFileName).Should().BeFalse();
                var project = LoadProjectFromFile(projectFileName);
                var glob = new CompositeGlob(project.GetAllGlobs("Compile").Select(x => x.MsBuildGlob));
                glob.IsMatch(excludedFileName).Should().BeFalse();
            }

        }

        public CleanupProjectFilesTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}