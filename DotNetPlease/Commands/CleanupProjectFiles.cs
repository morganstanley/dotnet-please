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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Globbing;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class CleanupProjectFiles
    {
        [Command("cleanup-project-files", "Removes code files from the project directory that are explicitly excluded with a Compile Remove item.")]
        public class Command : IRequest
        {
            [Option("--allow-globs", "Remove all code files that are excluded, even those removed with globs")]
            public bool AllowGlobs { get; set; }

        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            public override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Cleaning up project files");

                var context = new Context(command);

                foreach (var project in Workspace.ProjectFileNames)
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

                    if (project.Xml.HasUnsavedChanges && !Workspace.IsDryRun)
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
                        if (!glob.IsMatch(fileName) && Workspace.SafeDeleteFile(fileName))
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
                            if (Workspace.SafeDeleteFile(fileName))
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
                public HashSet<string> FilesRemoved { get; } = new(PathComparer);

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