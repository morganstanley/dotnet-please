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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Helpers;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using Mediator;
using Microsoft.Build.Construction;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands;

public static class CreateSolutionFilters
{
    [Command(
        "create-solution-filters",
        "Converts multiple solutions to a single solution with solution filter files")]
    public class Command : IRequest
    {
        [Option("--from", "Globbing pattern for source .sln files")]
        [Required]
        public string From { get; set; } = null!;

        [Option("--target", "Target solution file name")]
        [Required]
        public string Target { get; set; } = null!;
    }

    [UsedImplicitly]
    public class CommandHandler : CommandHandlerBase<Command>
    {
        public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
        {
        }

        public override ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var context = new Context
            {
                Command = command,
                SourceSolutions = new List<string>(),
                TargetSolutionPath = Path.Combine(Workspace.WorkingDirectory, command.Target),
                ProjectsBySource = new Dictionary<string, List<string>>()
            };

            DiscoverSourceSolutions(context);
            
            if (context.SourceSolutions.Count == 0)
            {
                Reporter.Error($"No solutions found matching pattern: {command.From}");
                return ValueTask.FromResult(Unit.Value);
            }

            ExtractProjectsFromSources(context);
            CreateOrValidateTargetSolution(context);
            ConsolidateProjectsToTarget(context);
            GenerateSolutionFilters(context);
            CleanupSourceSolutions(context);

            Reporter.Success("Solution filter conversion completed successfully");
            return ValueTask.FromResult(Unit.Value);
        }

        private void DiscoverSourceSolutions(Context context)
        {
            Reporter.Info("Discovering source solutions");

            var solutionFiles = FileSystemHelper.GetFileNamesFromGlob(
                context.Command.From,
                Workspace.WorkingDirectory);

            context.SourceSolutions.AddRange(
                solutionFiles
                    .Where(IsSolutionFileName)
                    .Distinct()
                    .OrderBy(p => p));

            foreach (var solution in context.SourceSolutions)
            {
                Reporter.Info($"  Found: {Workspace.GetRelativePath(solution)}");
            }
        }

        private void ExtractProjectsFromSources(Context context)
        {
            Reporter.Info("Extracting projects from source solutions");

            foreach (var sourceSolution in context.SourceSolutions)
            {
                try
                {
                    var projects = GetProjectsFromSolution(sourceSolution);
                    context.ProjectsBySource[sourceSolution] = projects;
                    Reporter.Info($"  {Path.GetFileNameWithoutExtension(sourceSolution)}: {projects.Count} projects");
                }
                catch (Exception ex)
                {
                    Reporter.Error($"Failed to parse {sourceSolution}: {ex.Message}");
                }
            }
        }

        private void CreateOrValidateTargetSolution(Context context)
        {
            if (!File.Exists(context.TargetSolutionPath))
            {
                Reporter.Info($"Creating target solution: {Workspace.GetRelativePath(context.TargetSolutionPath)}");
                
                if (!Workspace.IsDryRun)
                {
                    DotNetCliHelper.CreateSolution(context.TargetSolutionPath);
                }
            }
            else
            {
                Reporter.Info($"Target solution already exists: {Workspace.GetRelativePath(context.TargetSolutionPath)}");
            }
        }

        private void ConsolidateProjectsToTarget(Context context)
        {
            Reporter.Info("Adding projects to target solution");

            using (Reporter.BeginScope("Projects"))
            {
                foreach (var sourceSolution in context.SourceSolutions)
                {
                    var solutionFolderName = Path.GetFileNameWithoutExtension(sourceSolution);
                    var projects = context.ProjectsBySource[sourceSolution];

                    foreach (var project in projects)
                    {
                        var projectRelativePath = Workspace.GetRelativePath(project);
                        
                        if (!Workspace.IsDryRun)
                        {
                            DotNetCliHelper.AddProjectToSolution(
                                project,
                                context.TargetSolutionPath,
                                solutionFolderName);
                        }

                        Reporter.Success($"  {projectRelativePath} -> {solutionFolderName}");
                    }
                }
            }
        }

        private void GenerateSolutionFilters(Context context)
        {
            Reporter.Info("Generating solution filter files");

            var targetSolutionRelativePath = Workspace.GetRelativePath(context.TargetSolutionPath);
            var targetSolutionDirectory = Path.GetDirectoryName(context.TargetSolutionPath)!;

            foreach (var sourceSolution in context.SourceSolutions)
            {
                var filterFileName = Path.ChangeExtension(sourceSolution, ".slnf");
                var filterName = Path.GetFileNameWithoutExtension(sourceSolution);
                var projects = context.ProjectsBySource[sourceSolution];

                var projectIncludePaths = projects
                    .Select(p => Path.GetRelativePath(targetSolutionDirectory, p))
                    .OrderBy(p => p)
                    .ToList();

                var filter = new SolutionFilterJson
                {
                    Version = "0.1",
                    DefaultFilter = filterName,
                    Filters = new Dictionary<string, FilterDefinition>
                    {
                        {
                            filterName,
                            new FilterDefinition
                            {
                                Path = Path.GetRelativePath(targetSolutionDirectory, context.TargetSolutionPath),
                                Includes = projectIncludePaths
                            }
                        }
                    }
                };

                if (!Workspace.IsDryRun)
                {
                    var json = JsonSerializer.Serialize(filter, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    File.WriteAllText(filterFileName, json);
                }

                Reporter.Success($"  {Workspace.GetRelativePath(filterFileName)}");
            }
        }

        private void CleanupSourceSolutions(Context context)
        {
            Reporter.Info("Cleaning up source solution files");

            foreach (var sourceSolution in context.SourceSolutions)
            {
                if (!Workspace.IsDryRun)
                {
                    File.Delete(sourceSolution);
                }

                Reporter.Success($"  Deleted {Workspace.GetRelativePath(sourceSolution)}");
            }
        }

        private class Context
        {
            public Command Command { get; set; } = null!;
            public List<string> SourceSolutions { get; set; } = null!;
            public string TargetSolutionPath { get; set; } = null!;
            public Dictionary<string, List<string>> ProjectsBySource { get; set; } = null!;
        }

        private class SolutionFilterJson
        {
            public string? Version { get; set; }
            public string? DefaultFilter { get; set; }
            public Dictionary<string, FilterDefinition>? Filters { get; set; }
        }

        private class FilterDefinition
        {
            public string? Path { get; set; }
            public List<string>? Includes { get; set; }
        }
    }
}
