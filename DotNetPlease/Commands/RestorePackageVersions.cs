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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class RestorePackageVersions
    {
        [Command(
            "restore-package-versions",
            "Restores the version on PackageReference items from PackageVersion items.")]
        public class Command : IRequest
        {
            [Argument(0, "The file where the PackageVersion items are kept (defaults to Directory.Packages.props)")]
            public string? PackageVersionsFileName { get; set; }
        }

        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Restoring package versions");

                var context = CreateContext(command);

                CollectPackageVersions(context);

                UpdateProjects(context);

                if (context.FilesUpdated.Count == 0)
                {
                    Reporter.Success("Nothing to update");
                }

                return Task.CompletedTask;
            }

            private void CollectPackageVersions(Context context)
            {
                var items =
                    context.PackageVersionsProject.Xml.Items.Where(i => i.ItemType == "PackageVersion");
                foreach (var packageVersion in items)
                {
                    var version = packageVersion.GetMetadataValue("Version");
                    if (version != null)
                    {
                        context.PackageVersions[packageVersion.Include] =
                            NuGetVersion.TryParse(version, out var nuGetVersion)
                                ? (object)nuGetVersion
                                : version;
                    }
                }
            }

            private void UpdateProjects(Context context)
            {
                foreach (var project in context.Projects)
                {
                    UpdateProject(project, context);
                }
            }

            private Context CreateContext(Command command)
            {
                var projects = Workspace.LoadProjects();
                var packageVersionsFileName = command.PackageVersionsFileName
                                              ?? GetFilePathAbove(
                                                  "Directory.Packages.props",
                                                  Workspace.WorkingDirectory);

                if (packageVersionsFileName == null)
                {
                    throw new InvalidOperationException(
                        "Could not find the file holding the package versions");
                }

                var packageVersions = LoadProjectFromFile(Workspace.GetFullPath(packageVersionsFileName));

                return new Context(command, projects, packageVersions);
            }

            private void UpdateProject(Project project, Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(project.FullPath);

                using (Reporter.BeginScope($"Project: {projectRelativePath}"))
                {
                    var packageReferences =
                        project.Xml.Items.Where(i => i.ItemType == "PackageReference");

                    foreach (var packageReference in packageReferences)
                    {
                        UpdatePackageReference(packageReference, context);
                    }

                    if (project.Xml.HasUnsavedChanges)
                    {
                        context.FilesUpdated.Add(project.FullPath);
                        if (!Workspace.IsStaging)
                        {
                            project.Save();
                        }
                    }
                }
            }

            private void UpdatePackageReference(ProjectItemElement packageReference, Context context)
            {
                if (packageReference.GetMetadataValue("Version") != null) return;

                var packageName = packageReference.Include;
                if (!context.PackageVersions.TryGetValue(packageName, out var version)) return;

                Reporter.Success($"Update PackageReference \"{packageName}\", set Version = \"{version}\"");
                packageReference.SetMetadataValue("Version", version.ToString(), expressAsAttribute: true);
            }

            private class Context
            {
                public Command Command { get; }

                public Project PackageVersionsProject { get; }

                public List<Project> Projects { get; }

                public Dictionary<string, object> PackageVersions { get; } =
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                public HashSet<string> FilesUpdated { get; } = new HashSet<string>(PathComparer);

                public Context(Command command, List<Project> projects, Project packageVersionsProject)
                {
                    Command = command;
                    Projects = projects;
                    PackageVersionsProject = packageVersionsProject;
                }
            }

            public CommandHandler([NotNull] CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}