﻿using System;
using FluentAssertions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

// ReSharper disable StringLiteralTypo

namespace DotNetPlease.Commands
{
    public class MoveProjectTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_moves_project_file_and_directory(bool moveOutsideOfRootDirectory, bool stage)
        {
            var originalRelativePath = "OriginalProjectDirectory/OriginalProjectName.csproj";
            var originalFullPath = GetFullPath(originalRelativePath);
            CreateProject(originalFullPath);

            var newRelativePath = moveOutsideOfRootDirectory 
                ? $"../{Guid.NewGuid()}/NewSolution/NewProjectDirectory/NewProjectName.csproj"
                : "NewProjectDirectory/NewProjectName.csproj";
            var newFullPath = GetFullPath(newRelativePath);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                originalRelativePath,
                newRelativePath,
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            File.Exists(newFullPath).Should().BeTrue();
            File.Exists(originalFullPath).Should().BeFalse();
            Directory.Exists(Path.GetDirectoryName(originalFullPath)).Should().BeFalse();
        }

        [Theory, CombinatorialData]
        public async Task It_replaces_the_project_in_the_solution_file(bool moveOutsideOfRootDirectory, bool stage)
        {
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            var originalRelativePath = "OriginalProjectDirectory/OriginalProjectName.csproj";
            var originalFullPath = GetFullPath(originalRelativePath);
            CreateProject(originalFullPath);
            AddProjectToSolution(originalFullPath, solutionFileName);
            var newRelativePath = moveOutsideOfRootDirectory
                ? $"../{Guid.NewGuid()}/NewSolution/NewProjectDirectory/NewProjectName.csproj"
                : "NewProjectDirectory/NewProjectName.csproj";
            var newFullPath = GetFullPath(newRelativePath);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                originalRelativePath,
                newRelativePath,
                "Test.sln",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var solution = LoadAndValidateSolution(solutionFileName);
            var project = solution.ProjectsInOrder.FirstOrDefault(p => IsSamePath(p.AbsolutePath, newFullPath));
            project.Should().NotBeNull();
            var oldProject =
                solution.ProjectsInOrder.FirstOrDefault(p => IsSamePath(p.AbsolutePath, originalFullPath));
            oldProject.Should().BeNull();
        }

        [Theory, CombinatorialData]
        public async Task It_fixes_ProjectReferences(bool moveOutsideOfRootDirectory, bool stage)
        {
            var originalRelativePath = "OriginalProject/OriginalProject.csproj";
            var originalFullPath = GetFullPath(originalRelativePath);
            CreateProject(originalFullPath);
            var originalReferencePath = "OtherProject/OtherProject.csproj";
                var referenceFullPath = GetFullPath(originalReferencePath);
            CreateProject(referenceFullPath);
            AddProjectReference(originalFullPath, referenceFullPath);
            AddProjectReference(referenceFullPath, originalFullPath);
            var newRelativePath = moveOutsideOfRootDirectory
                ? $"../{Guid.NewGuid()}/NewSolution/NewProjectDirectory/NewProjectName.csproj"
                : "NewProjectDirectory/NewProjectName.csproj";
            var newFullPath = GetFullPath(newRelativePath);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                originalRelativePath,
                newRelativePath,
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            FindProjectReference(newFullPath, referenceFullPath).Should().NotBeNull();
            FindProjectReference(referenceFullPath, newFullPath).Should().NotBeNull();
            FindProjectReference(referenceFullPath, originalFullPath).Should().BeNull();
        }

        public MoveProjectTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}