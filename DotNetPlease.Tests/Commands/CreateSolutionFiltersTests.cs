// Morgan Stanley makes this available to you under the Apache License,
// Version 2.0 (the "License"). You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0.
// 
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership. Unless agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations
// under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands;

public class CreateSolutionFiltersTests : TestFixtureBase
{
    [Theory, CombinatorialData]
    public async Task It_consolidates_two_solutions_into_one_with_filters(bool dryRun)
    {
        // Arrange
        var solution1Path = GetFullPath("Solution1/Solution1.sln");
        CreateSolution(solution1Path);
        var project1Path = GetFullPath("Solution1/Project1/Project1.csproj");
        CreateProject(project1Path);
        AddProjectToSolution(project1Path, solution1Path);

        var solution2Path = GetFullPath("Solution2/Solution2.sln");
        CreateSolution(solution2Path);
        var project2Path = GetFullPath("Solution2/Project2/Project2.csproj");
        CreateProject(project2Path);
        AddProjectToSolution(project2Path, solution2Path);

        var targetSolutionPath = GetFullPath("Consolidated.sln");

        if (dryRun) CreateSnapshot();

        // Act
        await RunAndAssertSuccess(
            "create-solution-filters",
            "--from", "Solution*/Solution*.sln",
            "--target", "Consolidated.sln",
            DryRunOption(dryRun));

        // Assert
        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        File.Exists(targetSolutionPath).Should().BeTrue("Target solution should be created");

        var projectsInTarget = GetProjectsFromSolution(targetSolutionPath);
        projectsInTarget.Should().Contain(project1Path, "Project1 should be in target");
        projectsInTarget.Should().Contain(project2Path, "Project2 should be in target");

        File.Exists(solution1Path).Should().BeFalse("Source solution 1 should be deleted");
        File.Exists(solution2Path).Should().BeFalse("Source solution 2 should be deleted");

        var filter1Path = Path.ChangeExtension(solution1Path, ".slnf");
        var filter2Path = Path.ChangeExtension(solution2Path, ".slnf");
        File.Exists(filter1Path).Should().BeTrue("Solution1.slnf should be created");
        File.Exists(filter2Path).Should().BeTrue("Solution2.slnf should be created");

        VerifyFilterFile(filter1Path, "Solution1", targetSolutionPath, new[] { project1Path });
        VerifyFilterFile(filter2Path, "Solution2", targetSolutionPath, new[] { project2Path });
    }

    [Theory, CombinatorialData]
    public async Task It_uses_existing_target_solution(bool dryRun)
    {
        // Arrange
        var targetSolutionPath = GetFullPath("Consolidated.sln");
        CreateSolution(targetSolutionPath);

        var solution1Path = GetFullPath("Solution1/Solution1.sln");
        CreateSolution(solution1Path);
        var project1Path = GetFullPath("Solution1/Project1/Project1.csproj");
        CreateProject(project1Path);
        AddProjectToSolution(project1Path, solution1Path);

        var solution2Path = GetFullPath("Solution2/Solution2.sln");
        CreateSolution(solution2Path);
        var project2Path = GetFullPath("Solution2/Project2/Project2.csproj");
        CreateProject(project2Path);
        AddProjectToSolution(project2Path, solution2Path);

        if (dryRun) CreateSnapshot();

        // Act
        await RunAndAssertSuccess(
            "create-solution-filters",
            "--from", "Solution*/Solution*.sln",
            "--target", "Consolidated.sln",
            DryRunOption(dryRun));

        // Assert
        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectsInTarget = GetProjectsFromSolution(targetSolutionPath);
        projectsInTarget.Should().Contain(project1Path);
        projectsInTarget.Should().Contain(project2Path);
    }

    [Theory, CombinatorialData]
    public async Task It_handles_multiple_projects_per_solution(bool dryRun)
    {
        // Arrange
        var solution1Path = GetFullPath("Solution1/Solution1.sln");
        CreateSolution(solution1Path);
        var project1APath = GetFullPath("Solution1/ProjectA/ProjectA.csproj");
        var project1BPath = GetFullPath("Solution1/ProjectB/ProjectB.csproj");
        CreateProject(project1APath);
        CreateProject(project1BPath);
        AddProjectToSolution(project1APath, solution1Path);
        AddProjectToSolution(project1BPath, solution1Path);

        var solution2Path = GetFullPath("Solution2/Solution2.sln");
        CreateSolution(solution2Path);
        var project2APath = GetFullPath("Solution2/ProjectC/ProjectC.csproj");
        var project2BPath = GetFullPath("Solution2/ProjectD/ProjectD.csproj");
        CreateProject(project2APath);
        CreateProject(project2BPath);
        AddProjectToSolution(project2APath, solution2Path);
        AddProjectToSolution(project2BPath, solution2Path);

        var targetSolutionPath = GetFullPath("Consolidated.sln");

        if (dryRun) CreateSnapshot();

        // Act
        await RunAndAssertSuccess(
            "create-solution-filters",
            "--from", "Solution*/Solution*.sln",
            "--target", "Consolidated.sln",
            DryRunOption(dryRun));

        // Assert
        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectsInTarget = GetProjectsFromSolution(targetSolutionPath);
        projectsInTarget.Should().Contain(new[] { project1APath, project1BPath, project2APath, project2BPath });

        VerifyFilterFile(Path.ChangeExtension(solution1Path, ".slnf"), "Solution1", targetSolutionPath, new[] { project1APath, project1BPath });
        VerifyFilterFile(Path.ChangeExtension(solution2Path, ".slnf"), "Solution2", targetSolutionPath, new[] { project2APath, project2BPath });
    }

    [Theory, CombinatorialData]
    public async Task It_handles_single_solution(bool dryRun)
    {
        // Arrange
        var solution1Path = GetFullPath("Solution1/Solution1.sln");
        CreateSolution(solution1Path);
        var project1Path = GetFullPath("Solution1/Project1/Project1.csproj");
        CreateProject(project1Path);
        AddProjectToSolution(project1Path, solution1Path);

        var targetSolutionPath = GetFullPath("Consolidated.sln");

        if (dryRun) CreateSnapshot();

        // Act
        await RunAndAssertSuccess(
            "create-solution-filters",
            "--from", "Solution*/Solution*.sln",
            "--target", "Consolidated.sln",
            DryRunOption(dryRun));

        // Assert
        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        File.Exists(targetSolutionPath).Should().BeTrue();
        var projectsInTarget = GetProjectsFromSolution(targetSolutionPath);
        projectsInTarget.Should().Contain(project1Path);

        var filterPath = Path.ChangeExtension(solution1Path, ".slnf");
        File.Exists(filterPath).Should().BeTrue();
        VerifyFilterFile(filterPath, "Solution1", targetSolutionPath, new[] { project1Path });
    }

    private void VerifyFilterFile(string filterPath, string expectedFilterName, string targetSolutionPath, string[] expectedProjectPaths)
    {
        File.Exists(filterPath).Should().BeTrue($"Filter file {filterPath} should exist");

        var json = File.ReadAllText(filterPath);
        using var filter = JsonDocument.Parse(json);

        var root = filter.RootElement;
        root.TryGetProperty("solution", out var solutionElement).Should().BeTrue();

        var filterDir = Path.GetDirectoryName(filterPath)!;
        var expectedRelativePath = Path.GetRelativePath(filterDir, targetSolutionPath);

        solutionElement.TryGetProperty("path", out var pathElement).Should().BeTrue();
        pathElement.GetString().Should().Be(expectedRelativePath);

        solutionElement.TryGetProperty("projects", out var projectsElement).Should().BeTrue();
        var projects = projectsElement.EnumerateArray().Select(e => e.GetString()).ToList();

        var expectedProjects = expectedProjectPaths
            .Select(p => Path.GetRelativePath(filterDir, p))
            .OrderBy(p => p)
            .ToList();

        projects.Should().BeEquivalentTo(expectedProjects);
    }

    public CreateSolutionFiltersTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }
}
