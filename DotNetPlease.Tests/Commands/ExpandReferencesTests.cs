// Morgan Stanley makes this available to you under the Apache License,
// Version 2.0 (the "License"). You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0.
// 
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership. Unless required by applicable law or agreed
// to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
// or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands;

public class ExpandReferencesTests : TestFixtureBase
{
    [Theory, CombinatorialData]
    public async Task It_replaces_PackageReference_with_ProjectReference(bool dryRun)
    {
        var (_, sourceProjectPath) = CreateSolutionWithSingleProject("Source", "Package1");
        var (_, targetProjectPath) = CreateSolutionWithSingleProject("Target", "Project1");
        AddPackageReference(targetProjectPath, "Package1", "1.0.0");

        if (dryRun) CreateSnapshot();

        await RunAndAssertSuccess("expand-references", "Source/Source.sln", "--workspace", "Target/Target.sln", DryRunOption(dryRun));

        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectReference = FindProjectReference(targetProjectPath, sourceProjectPath);
        projectReference.Should().NotBeNull();

        var packageReference = FindPackageReference(targetProjectPath, "Package1");
        packageReference.Should().BeNull();
    }

    [Theory, CombinatorialData]
    public async Task It_adds_the_project_from_PackageReference_to_the_solution(bool dryRun)
    {
        var (sourceSlnPath, sourceProjectPath) = CreateSolutionWithSingleProject("Source", "Package1");
        var (targetSlnPath, targetProjectPath) = CreateSolutionWithSingleProject("Target", "Project1");
        AddPackageReference(targetProjectPath, "Package1", "1.0.0");

        if (dryRun) CreateSnapshot();

        await RunAndAssertSuccess("expand-references", "Source/Source.sln", "--workspace", "Target/Target.sln", DryRunOption(dryRun));

        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectsInSolution = GetProjectsFromSolution(targetSlnPath);
        projectsInSolution.Should().Contain(sourceProjectPath);
    }

    [Theory, CombinatorialData]
    public async Task It_replaces_Reference_with_ProjectReference(bool dryRun)
    {
        var (sourceSlnPath, sourceProjectPath) = CreateSolutionWithSingleProject("Source", "ClassLib1");
        var (targetSlnPath, targetProjectPath) = CreateSolutionWithSingleProject("Target", "Project1");
        AddAssemblyReference(targetProjectPath, "ClassLib1");

        if (dryRun) CreateSnapshot();

        await RunAndAssertSuccess("expand-references", "Source/Source.sln", "--workspace", "Target/Target.sln", DryRunOption(dryRun));

        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectReference = FindProjectReference(targetProjectPath, sourceProjectPath);
        projectReference.Should().NotBeNull();

        var assemblyReference = FindAssemblyReference(targetProjectPath, "ClassLib1");
        assemblyReference.Should().BeNull();
    }

    [Theory, CombinatorialData]
    public async Task It_adds_the_project_from_Reference_to_the_solution(bool dryRun)
    {
        var (sourceSlnPath, sourceProjectPath) = CreateSolutionWithSingleProject("Source", "ClassLib1");
        var (targetSlnPath, targetProjectPath) = CreateSolutionWithSingleProject("Target", "Project1");
        AddAssemblyReference(targetProjectPath, "ClassLib1");

        if (dryRun) CreateSnapshot();

        await RunAndAssertSuccess("expand-references", "Source/Source.sln", "--workspace", "Target/Target.sln", DryRunOption(dryRun));

        if (dryRun)
        {
            VerifySnapshot();
            return;
        }

        var projectsInSolution = GetProjectsFromSolution(targetSlnPath);
        projectsInSolution.Should().Contain(sourceProjectPath);
    }

    public ExpandReferencesTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    private (string solutionPath, string projectPath) CreateSolutionWithSingleProject(string solutionName, string projectName)
    {
        var solutionPath = GetFullPath($"{solutionName}/{solutionName}.sln");
        var projectPath = GetFullPath($"{solutionName}/{projectName}/{projectName}.csproj");
        CreateProject(projectPath);
        CreateSolution(solutionPath);
        AddProjectToSolution(projectPath, solutionPath);

        return (solutionPath, projectPath);
    }
}
