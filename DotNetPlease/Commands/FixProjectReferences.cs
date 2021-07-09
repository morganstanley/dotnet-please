﻿using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Commands
{
    public static class FixProjectReferences
    {
        [Command("fix-project-references", "Try to fix or remove invalid ProjectReference items")]
        public class Command : IRequest
        {
            [Argument(0, CommandArguments.Projects.Description)]
            public string? Projects { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Fixing project references");

                var projects = Workspace.LoadProjects(command.Projects);

                var context = new Context(command)
                {
                    Projects = projects
                };

                foreach (var project in context.Projects)
                {
                    FixProjectReferences(project, context);
                }

                if (context.FilesUpdated.Count == 0)
                {
                    Reporter.Success("Nothing to fix");
                }

                return Task.CompletedTask;
            }

            private void FixProjectReferences(
                Project project,
                Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(project.FullPath);

                using (Reporter.BeginScope($"Project {projectRelativePath}"))
                {
                    foreach (var projectReference in
                        project.Items
                            .Where(
                                i => i.ItemType == "ProjectReference"
                                     && i.EvaluatedInclude == i.UnevaluatedInclude)
                            .ToList())
                    {
                        FixProjectReference(project, projectReference, context);
                    }

                    if (project.Xml.HasUnsavedChanges && !Workspace.IsStaging)
                    {
                        project.Save();
                    }
                }
            }

            private void FixProjectReference(
                Project project,
                ProjectItem projectReference,
                Context context)
            {
                var relativePath = projectReference.EvaluatedInclude;
                var projectDirectory = project.DirectoryPath;
                var absolutePath = Path.GetFullPath(relativePath, projectDirectory);
                var referencedProject =
                    context.Projects.FirstOrDefault(p => IsSamePath(p.FullPath, absolutePath));
                if (referencedProject != null) return;

                var projectFileName = Path.GetFileName(relativePath);
                referencedProject =
                    context.Projects.FirstOrDefault(
                        p => string.Equals(
                            projectFileName,
                            Path.GetFileName(p.FullPath),
                            StringComparison.OrdinalIgnoreCase));
                if (referencedProject != null)
                {
                    var fixedRelativePath = Path.GetRelativePath(projectDirectory!, referencedProject.FullPath);
                    projectReference.UnevaluatedInclude = fixedRelativePath;
                    context.FilesUpdated.Add(project.FullPath);
                    Reporter.Success($"Update ProjectReference \"{relativePath}\" => \"{fixedRelativePath}\"");
                }
                else
                {
                    project.RemoveItem(projectReference);
                    context.FilesUpdated.Add(project.FullPath);
                    Reporter.Success($"Remove ProjectReference \"{relativePath}\"");
                }
            }

            private class Context
            {
                public Context(Command command)
                {
                    Command = command;
                }

                public Command Command { get; }
                public List<Project> Projects { get; set; } = null!;
                public HashSet<string> FilesUpdated { get; } = new HashSet<string>(PathComparer);
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}