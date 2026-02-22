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
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Helpers;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using Mediator;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands;

public static class ExpandReferences
{
    [Command(
        "expand-references",
        "Includes projects from the specified solution or globbing pattern to the current solution, replacing any Reference and PackageReference items")]
    public class Command : IRequest
    {
        [Argument(0, "The projects or solutions to include")]
        public string ProjectsOrSolutions { get; set; } = null!;
    }

    [UsedImplicitly]
    public class CommandHandler : CommandHandlerBase<Command>
    {
        public override ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            var context = CreateContext(command);

            DiscoverProjectsToInclude(context);
            FindReferencesToReplace(context);
            ReplaceReferencesWithProjectReference(context);
            AddProjectsToCurrentSolution(context);

            if (context.FilesUpdated.Count == 0)
            {
                Reporter.Success("Nothing to update");
            }

            return ValueTask.FromResult(Unit.Value);
        }

        private void DiscoverProjectsToInclude(Context context)
        {
            Reporter.Info("Discovering projects to include");

            context.ProjectsInSource.AddRange(
                GetProjectInfosFromGlob(
                    context.Command.ProjectsOrSolutions,
                    Workspace.WorkingDirectory,
                    allowSolutions: true));

            CreateLookupsForIncludedProjects(context);
        }

        private void AddProjectsToCurrentSolution(Context context)
        {
            var slnRelativePath = Path.GetRelativePath(Workspace.WorkingDirectory, context.SolutionFileName);

            var projectsToInclude = new HashSet<string>(PathComparer);

            foreach (var packageId in context.PackagesToReplace)
            {
                IncludeProjectWithDependencies(context.PackageNameToProjectFileName[packageId]);
            }

            foreach (var assemblyName in context.AssembliesToReplace)
            {
                IncludeProjectWithDependencies(context.AssemblyNameToProjectFileName[assemblyName]);
            }

            using (Reporter.BeginScope($"Solution {slnRelativePath}"))
            {
                foreach (var source in context.ProjectsInSource.ToLookup(x => x.SolutionFileName ?? ""))
                {
                    var solutionFolder = source.Key == ""
                        ? ""
                        : Path.GetFileNameWithoutExtension(Path.GetFileName(source.Key));

                    foreach (var projectInfo in source.Where(p => projectsToInclude.Contains(p.ProjectFileName)))
                    {
                        var projectRelativePath = Path.GetRelativePath(
                            Path.GetDirectoryName(context.SolutionFileName),
                            projectInfo.ProjectFileName);

                        Reporter.Success($"Add project {projectRelativePath}");

                        if (!Workspace.IsDryRun)
                        {
                            if (solutionFolder == "")
                            {
                                DotNetCliHelper.AddProjectToSolution(
                                    projectInfo.ProjectFileName,
                                    context.SolutionFileName);
                            }
                            else
                            {
                                DotNetCliHelper.AddProjectToSolution(
                                    projectInfo.ProjectFileName,
                                    context.SolutionFileName,
                                    solutionFolder);
                            }

                            context.FilesUpdated.Add(context.SolutionFileName);
                        }
                    }
                }
            }

            void IncludeProjectWithDependencies(string projectFileName)
            {
                if (projectsToInclude.Contains(projectFileName))
                    return;

                projectsToInclude.Add(projectFileName);

                var project = LoadProjectFromFile(projectFileName);

                foreach (var projectRef in project.AllEvaluatedItems.Where(i => i.ItemType == "ProjectReference"))
                {
                    IncludeProjectWithDependencies(
                        Path.GetFullPath(projectRef.EvaluatedInclude, Path.GetDirectoryName(projectFileName)));
                }
            }
        }

        private void CreateLookupsForIncludedProjects(Context context)
        {
            foreach (var projectInfo in context.ProjectsInSource)
            {
                var project = LoadProjectFromFile(
                    projectInfo.ProjectFileName,
                    new ProjectOptions
                    {
                        LoadSettings =
                            ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition
                            | ProjectLoadSettings.IgnoreInvalidImports
                            | ProjectLoadSettings.IgnoreMissingImports,
                        ProjectCollection = new ProjectCollection(),
                    });

                var packageName =
                    project.GetProperty("PackageId")?.EvaluatedValue
                    ?? project.GetProperty("AssemblyName")?.EvaluatedValue
                    ?? Path.GetFileNameWithoutExtension(Path.GetFileName(projectInfo.ProjectFileName));

                context.PackageNameToProjectFileName[packageName] = projectInfo.ProjectFileName;
                context.ProjectFileNameToPackageName[projectInfo.ProjectFileName] = packageName;

                var assemblyName =
                    project.GetProperty("AssemblyName")?.EvaluatedValue
                    ?? Path.GetFileNameWithoutExtension(Path.GetFileName(projectInfo.ProjectFileName));

                context.AssemblyNameToProjectFileName[assemblyName] = projectInfo.ProjectFileName;
                context.ProjectFileNameToAssemblyName[projectInfo.ProjectFileName] = assemblyName;
            }
        }

        private void FindReferencesToReplace(Context context)
        {
            var projects = LoadProjects(GetProjectsFromSolution(context.SolutionFileName));

            foreach (var project in projects)
            {
                FindReferencesToReplace(context, project);
            }
        }

        private void FindReferencesToReplace(Context context, Project project)
        {
            var packageRefs = project.Items.Where(x => x.ItemType == "PackageReference").ToList();

            foreach (var packageRef in packageRefs)
            {
                if (context.PackageNameToProjectFileName.ContainsKey(packageRef.EvaluatedInclude))
                {
                    context.PackagesToReplace.Add(packageRef.EvaluatedInclude);
                }
            }

            var assemblyRefs = project.Items.Where(x => x.ItemType == "Reference").ToList();

            foreach (var assemblyRef in assemblyRefs)
            {
                if (context.AssemblyNameToProjectFileName.ContainsKey(assemblyRef.EvaluatedInclude))
                {
                    context.AssembliesToReplace.Add(assemblyRef.EvaluatedInclude);
                }
            }
        }

        private void ReplaceReferencesWithProjectReference(Context context)
        {
            var projects = LoadProjects(GetProjectsFromSolution(context.SolutionFileName));

            foreach (var project in projects)
            {
                ReplaceReferencesInProject(context, project);

                if (project.Xml.HasUnsavedChanges)
                {
                    if (!Workspace.IsDryRun)
                    {
                        project.Xml.Save();
                    }

                    context.FilesUpdated.Add(project.FullPath);
                }
            }
        }

        private void ReplaceReferencesInProject(Context context, Project project)
        {
            var projectRelativePath = Path.GetRelativePath(Workspace.WorkingDirectory, project.FullPath);

            using (Reporter.BeginScope($"Project {projectRelativePath}"))
            {
                var projectPaths = new HashSet<string>(PathComparer);

                var packageRefs = project.Xml.Items.Where(x => x.ElementName == "PackageReference").ToList();

                foreach (var packageRef in packageRefs)
                {
                    if (context.PackageNameToProjectFileName.TryGetValue(
                            packageRef.Include,
                            out var refProjectFileName))
                    {
                        packageRef.Parent.RemoveChild(packageRef);
                        var projectPath = Path.GetRelativePath(project.DirectoryPath, refProjectFileName);
                        projectPaths.Add(projectPath);

                        Reporter.Success(
                            $"Replace PackageReference \"{packageRef.Include}\" => ProjectReference \"{projectPath}\"");
                    }
                }

                var assemblyRefs = project.Xml.Items.Where(x => x.ElementName == "Reference").ToList();

                foreach (var assemblyRef in assemblyRefs)
                {
                    if (context.AssemblyNameToProjectFileName.TryGetValue(
                            assemblyRef.Include,
                            out var refProjectFileName))
                    {
                        assemblyRef.Parent.RemoveChild(assemblyRef);
                        var projectPath = Path.GetRelativePath(project.DirectoryPath, refProjectFileName);
                        projectPaths.Add(projectPath);

                        Reporter.Success(
                            $"Replace Reference \"{assemblyRef.Include}\" => ProjectReference \"{projectPath}\"");
                    }
                }

                if (projectPaths.Count == 0)
                    return;

                var itemGroup = project.Xml.AddItemGroup();

                foreach (var projectPath in projectPaths)
                {
                    itemGroup.AddItem("ProjectReference", projectPath);
                }
            }
        }

        private Context CreateContext(Command command)
        {
            return new Context(command)
            {
                SolutionFileName = Workspace.SolutionFileName ?? throw new ValidationException("Could not find the target solution")
            };
        }

        private class Context
        {
            public Command Command { get; }

            public Context(Command command)
            {
                Command = command;
            }

            public string SolutionFileName { get; set; }

            public List<ProjectInfo> ProjectsInSource { get; } = new();

            public Dictionary<string, string> PackageNameToProjectFileName { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> AssemblyNameToProjectFileName { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> ProjectFileNameToPackageName { get; } =
                new(PathComparer);

            public Dictionary<string, string> ProjectFileNameToAssemblyName { get; } =
                new(PathComparer);

            public HashSet<string> PackagesToReplace { get; } = new(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> AssembliesToReplace { get; } = new(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> FilesUpdated { get; } = new(PathComparer);
        }

        public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies) { }
    }
}
