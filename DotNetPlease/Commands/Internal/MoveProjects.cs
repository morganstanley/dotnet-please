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

using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands.Internal
{
    internal static class MoveProjects
    {
        public class Command : IRequest
        {
            public Command(List<ProjectMoveItem> moves, bool force)
            {
                Moves = moves;
                Force = force;
            }

            public List<ProjectMoveItem> Moves { get; }
            public bool Force { get; }
        }

        public class ProjectMoveItem
        {
            public ProjectMoveItem(string oldProjectFileName, string newProjectFileName)
            {
                OldProjectFileName = oldProjectFileName;
                NewProjectFileName = newProjectFileName;
            }

            public string OldProjectFileName { get; }
            public string NewProjectFileName { get; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                var projects = Workspace.LoadProjects();
                var context = new Context(command, projects);

                PreCheck(context);
                ReplaceProjectsInSolution(context);
                FixProjectReferences(context);
                MoveProjectFiles(context);

                return Task.CompletedTask;
            }

            private void PreCheck(Context context)
            {
                foreach (var move in context.Command.Moves)
                {
                    var projectDirectory = Path.GetDirectoryName(move.OldProjectFileName)!;

                    var projectsInDirectory =
                        GetProjectsFromDirectory(projectDirectory, recursive: false);
                    if (projectsInDirectory.Count > 1)
                    {
                        throw new InvalidOperationException(
                            $"Cannot move project \"{Workspace.GetRelativePath(move.OldProjectFileName)}\" because the directory contains multiple project files");
                    }

                    if (context.Projects.Any(p => IsSamePath(p.FullPath, move.NewProjectFileName)))
                    {
                        throw new InvalidOperationException(
                            $"The solution already contains a project at \"{Workspace.GetRelativePath(move.NewProjectFileName)}\"");
                    }

                    var newProjectDirectory = Path.GetDirectoryName(move.NewProjectFileName)!;

                    if (!IsSamePath(newProjectDirectory, projectDirectory) && Directory.Exists(newProjectDirectory))
                    {
                        if (!context.Command.Force)
                        {
                            throw new InvalidOperationException(
                                $"The directory \"{Workspace.GetRelativePath(newProjectDirectory)}\" already exists");
                        }
                    }
                }
            }

            private void FixProjectReferences(
                Context context)
            {
                foreach (var project in context.Projects)
                {
                    FixProjectReferences(project, context);
                }
            }

            private void FixProjectReferences(
                Project project,
                Context context)
            {
                using (Reporter.BeginScope($"Project: {Workspace.GetRelativePath(project.FullPath)}"))
                {
                    var thisProjectMove =
                        context.Command.Moves.FirstOrDefault(_ => IsSamePath(_.OldProjectFileName, project.FullPath));
                    var thisProjectNewPath =
                        thisProjectMove == null ? project.FullPath : thisProjectMove.NewProjectFileName;
                    var thisProjectOldDirectory =
                        Path.GetDirectoryName(
                            thisProjectMove == null ? project.FullPath : thisProjectMove.OldProjectFileName)!;
                    var thisProjectNewDirectory = Path.GetDirectoryName(thisProjectNewPath)!;

                    foreach (var projectReference in project.GetItems("ProjectReference"))
                    {
                        if (projectReference.IsImported
                            || projectReference.EvaluatedInclude != projectReference.UnevaluatedInclude)
                            continue;

                        var referencedProjectPath = Path.GetFullPath(
                            projectReference.UnevaluatedInclude,
                            thisProjectOldDirectory);
                        var referencedProjectMove =
                            context.Command.Moves.FirstOrDefault(_ => IsSamePath(_.OldProjectFileName, referencedProjectPath));
                        var referencedProjectNewPath = referencedProjectMove == null
                            ? referencedProjectPath
                            : referencedProjectMove.NewProjectFileName;

                        var newReference =
                            NormalizePath(Path.GetRelativePath(thisProjectNewDirectory, referencedProjectNewPath));

                        if (newReference != projectReference.UnevaluatedInclude)
                        {
                            Reporter.Success($"Replace ProjectReference \"{projectReference.UnevaluatedInclude}\" => \"{newReference}\"");
                            projectReference.UnevaluatedInclude = newReference;
                        }
                    }

                    if (project.Xml.HasUnsavedChanges)
                    {
                        context.FilesUpdated.Add(project.FullPath);
                        if (!Workspace.IsDryRun)
                        {
                            project.Save();
                        }
                    }
                }
            }

            private static readonly Regex ProjectInSolutionRegex =
                new Regex(
                    $@"^Project\(""(?<projectTypeGuid>.*?)""\) = ""(?<projectName>.*?)"", ""(?<projectRelativePath>.*?)"", ""(?<projectGuid>{{.*?\}})""",
                    RegexOptions.Compiled | RegexOptions.Multiline);


            private void ReplaceProjectsInSolution(
                Context context)
            {
                if (Workspace.SolutionFileName == null)
                    return;

                using (Reporter.BeginScope($"Solution: {Workspace.GetRelativePath(Workspace.SolutionFileName)}"))
                {
                    // TODO: a .sln parser/DOM would be nice here
                    // TODO: detect encoding
                    var encoding = Encoding.UTF8;
                    var solutionText = File.ReadAllText(Workspace.SolutionFileName, encoding);
                    var solutionDirectory = Path.GetDirectoryName(Workspace.SolutionFileName)!;

                    var newSolutionText = ProjectInSolutionRegex.Replace(
                        solutionText,
                        match =>
                        {
                            var (
                                    projectTypeGuid,
                                    projectName,
                                    projectRelativePath,
                                    projectGuid) =
                                (
                                    match.Groups["projectTypeGuid"].Value,
                                    match.Groups["projectName"].Value,
                                    match.Groups["projectRelativePath"].Value,
                                    match.Groups["projectGuid"].Value);

                            var projectFileName = Path.GetFullPath(
                                projectRelativePath,
                                solutionDirectory);

                            var move =
                                context.Command.Moves.FirstOrDefault(
                                    m => IsSamePath(m.OldProjectFileName, projectFileName));

                            if (move == null)
                                return match.Value;

                            var newRelativePath = Path.GetRelativePath(
                                solutionDirectory,
                                move.NewProjectFileName);

                            var newProjectName = GetProjectNameFromFileName(move.NewProjectFileName);

                            Reporter.Success($"Replace project \"{projectRelativePath}\" => \"{newRelativePath}\"");

                            return
                                $@"Project(""{projectTypeGuid}"") = ""{newProjectName}"", ""{newRelativePath}"", ""{projectGuid}""";
                        });

                    if (solutionText != newSolutionText)
                    {
                        context.FilesUpdated.Add(Workspace.SolutionFileName);
                        if (!Workspace.IsDryRun)
                        {
                            File.WriteAllText(Workspace.SolutionFileName, newSolutionText, encoding);
                        }
                    }
                }
            }

            private void MoveProjectFiles(Context context)
            {
                using (Reporter.BeginScope("Moving files and directories"))
                {
                    foreach (var move in context.Command.Moves)
                    {
                        if (IsSamePath(move.OldProjectFileName, move.NewProjectFileName))
                            continue;

                        var projectDirectory = Path.GetDirectoryName(move.OldProjectFileName)!;
                        var newProjectDirectory = Path.GetDirectoryName(move.NewProjectFileName)!;

                        if (!IsSamePath(projectDirectory, newProjectDirectory))
                        {
                            if (context.Command.Force)
                            {
                                Workspace.SafeDeleteDirectory(newProjectDirectory!);
                            }

                            Workspace.SafeMoveDirectory(projectDirectory!, newProjectDirectory!);

                            if (context.Command.Force)
                            {
                                Workspace.SafeDeleteDirectory(projectDirectory!);
                            }
                        }

                        var tempProjectFileName = Path.Combine(
                            newProjectDirectory!,
                            Path.GetFileName(move.OldProjectFileName)!);

                        Workspace.SafeMoveFile(tempProjectFileName, move.NewProjectFileName);
                    }
                }
            }

            private class Context
            {
                public Context(Command command, List<Project> projects)
                {
                    Command = command;
                    Projects = projects;
                }

                public Command Command { get; }
                public List<Project> Projects { get; }

                public HashSet<string> FilesUpdated { get; } = new HashSet<string>(PathComparer);
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}