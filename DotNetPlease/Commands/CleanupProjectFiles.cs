using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Globbing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class CleanupProjectFiles
    {
        [Command("cleanup-project-files", "Removes code files from the project directory that are explicitly excluded with a Compile Remove item.")]
        public class Command : IRequest
        {
            [Argument(0, CommandArguments.ProjectsOrSolution.Description)]
            public string? Projects { get; set; }

            [Option("--allow-globs", "Remove all code files that are excluded, even those removed with globs")]
            public bool AllowGlobs { get; set; }

        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Cleaning up project files");

                var context = new Context(command);

                foreach (var project in Workspace.GetProjects(command.Projects))
                {
                    VisitProject(project, context);
                }

                if (context.FilesRemoved.Count == 0)
                {
                    Reporter.Success("No files to remove");
                }

                return Task.CompletedTask;
            }

            private void VisitProject(string projectFileName, Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(projectFileName);

                using (Reporter.BeginScope($"Project: {projectRelativePath}"))
                {
                    var project = LoadProjectFromFile(projectFileName);

                    RemoveExcludedFiles(project, context);

                    if (project.Xml.HasUnsavedChanges && !Workspace.IsStaging)
                    {
                        project.Save();
                    }
                }
            }

            private void RemoveExcludedFiles(Project project, Context context)
            {
                var glob = new CompositeGlob(project.GetAllGlobs("Compile").Select(x => x.MsBuildGlob));

                if (context.Command.AllowGlobs)
                {
                    var codeFilesInProjectDirectory = GetCodeFilesInProjectDirectory(project.DirectoryPath);
                    foreach (var fileName in codeFilesInProjectDirectory)
                    {
                        if (!glob.IsMatch(fileName) && Workspace.TryDeleteFile(fileName))
                        {
                            context.FilesRemoved.Add(fileName);
                        }
                    }

                    return;
                }

                foreach (var item in
                    project.Xml.Items
                        .Where(
                            i => i.ElementName == "Compile")
                        .ToList())
                {
                    var remove = item.Remove;
                    if (!string.IsNullOrEmpty(remove))
                    {
                        var fileName = Path.GetFullPath(remove, project.DirectoryPath);

                        if (File.Exists(fileName)
                            && KnownCodeFileExtensions.Contains(Path.GetExtension(fileName))
                            && !glob.IsMatch(remove))
                        {
                            if (Workspace.TryDeleteFile(fileName))
                            {
                                context.FilesRemoved.Add(fileName);
                                item.Parent.RemoveChild(item);
                            }
                        }
                    }
                }
            }

            private class Context
            {
                public Command Command { get; }
                public HashSet<string> FilesRemoved { get; } = new HashSet<string>(PathComparer);

                public Context(Command command)
                {
                    Command = command;
                }
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}