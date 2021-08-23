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
    public static class PullPackageVersions
    {
        [Command(
            "pull-package-versions",
            "Pulls package versions from PackageReference items into PackageVersion items in a centrally managed file")]
        public class Command : IRequest
        {
            [Argument(0, "The file where the PackageVersion items are kept (defaults to Directory.Packages.props)")]
            public string? PackageVersionsFileName { get; set; }

            [Argument(1, CommandArguments.ProjectsOrSolution.Description)]
            public string? Projects { get; set; }
            
            [Option("--update", "Update the centrally managed version to the highest one found in PackageReference items (turned off by default)")]
            public bool Update { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Pulling package versions from project files");

                var context = CreateContext(command);

                CollectInitialPackageVersions(context);

                foreach (var project in context.Projects)
                {
                    VisitProject(project, context);
                }

                foreach (var project in context.Projects)
                {
                    UpdateProject(project, context);
                }

                UpdatePackageVersions(context);

                if (context.FilesUpdated.Count == 0)
                {
                    Reporter.Success("Nothing to update");
                }

                return Task.CompletedTask;
            }

            private Context CreateContext(Command command)
            {
                var projects = Workspace.LoadProjects(command.Projects);
                var packageVersionsFileName = command.PackageVersionsFileName ??
                                              GetFilePathAbove("Directory.Packages.props", Workspace.WorkingDirectory);
                if (packageVersionsFileName == null)
                {
                    throw new InvalidOperationException(
                        "Could not find the file holding the package versions");
                }

                var packageVersions = LoadProjectFromFile(Workspace.GetFullPath(packageVersionsFileName));

                return new Context(command, projects, packageVersions);
            }

            private void CollectInitialPackageVersions(Context context)
            {
                var packageVersions =
                    context.PackageVersionsProject.Xml.Items.Where(i => i.ItemType == "PackageVersion");
                foreach (var packageVersion in packageVersions)
                {
                    if (NuGetVersion.TryParse(packageVersion.GetMetadataValue("Version"), out var version))
                    {
                        context.PackageVersions[packageVersion.Include] = version;
                    }
                }
            }

            private void VisitProject(Project project, Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(project.FullPath);

                using (Reporter.BeginScope($"Reading project {projectRelativePath}"))
                {
                    var packageReferences =
                        project.Xml.Items.Where(i => i.ItemType == "PackageReference");

                    foreach (var packageReference in packageReferences)
                    {
                        VisitPackageReference(project, packageReference, context);
                    }
                }
            }

            private void VisitPackageReference(
                Project project,
                ProjectItemElement packageReference,
                Context context)
            {
                var packageName = packageReference.Include;
                var versionAttribute = packageReference.GetMetadataValue("Version");
                if (versionAttribute == null) return;
                if (NuGetVersion.TryParse(versionAttribute, out var version))
                {
                    if (!context.PackageVersions.TryGetValue(packageName, out var centralVersion))
                    {
                        context.PackageVersions[packageName] = version;
                    }
                    else if (ShouldUpdatePackageVersion(centralVersion, version, context))
                    {
                        // TODO: use user-defined strategy for consolidating different versions
                        context.PackageVersions[packageName] = version;
                    }
                }
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

            private void UpdatePackageReference(
                ProjectItemElement packageReference,
                Context context)
            {
                var packageName = packageReference.Include;
                var version = packageReference.GetMetadataValue("Version");
                if (version != null && context.PackageVersions.ContainsKey(packageName))
                {
                    Reporter.Success($"Update PackageReference \"{packageName}\", remove Version \"{version}\"");
                    packageReference.SetMetadataValue("Version", null);
                }
            }

            private void UpdatePackageVersions(Context context)
            {
                var packageVersionsRelativePath = Workspace.GetRelativePath(context.PackageVersionsProject.FullPath);
                using (Reporter.BeginScope($"File: {packageVersionsRelativePath}"))
                {
                    var itemGroup = context.PackageVersionsProject.Xml.ItemGroups
                        .FirstOrDefault(
                            g => g.Items.Any(i => i.ItemType == "PackageVersion"));

                    foreach (var version in context.PackageVersions)
                    {
                        var packageVersion = context.PackageVersionsProject.Xml.Items.FirstOrDefault(
                            i => i.ItemType == "PackageVersion"
                                 && i.Condition == ""
                                 && i.Include == version.Key);
                        if (packageVersion == null)
                        {
                            itemGroup ??= context.PackageVersionsProject.Xml.AddItemGroup();
                            packageVersion = itemGroup.AddItem("PackageVersion", version.Key);
                            Reporter.Success($"Add PackageVersion \"{version.Key}\", \"{version.Value.ToString()}\"");
                        }
                        else
                        {
                            Reporter.Success(
                                $"Update PackageVersion \"{version.Key}\", \"{version.Value.ToString()}\"");
                        }

                        packageVersion.SetMetadataValue("Version", version.Value.ToString(), expressAsAttribute: true);
                    }

                    if (context.PackageVersionsProject.Xml.HasUnsavedChanges)
                    {
                        context.FilesUpdated.Add(context.PackageVersionsProject.FullPath);
                        if (!Workspace.IsStaging)
                        {
                            context.PackageVersionsProject.Save();
                        }
                    }
                }
            }

            private bool ShouldUpdatePackageVersion(
                NuGetVersion centralVersion,
                NuGetVersion referencedVersion,
                Context context)
            {
                // TODO: use user-defined strategy
                return context.Command.Update && referencedVersion > centralVersion;
            }

            private class Context
            {
                public Command Command { get; }

                public Project PackageVersionsProject { get; }

                public List<Project> Projects { get; }

                public Dictionary<string, NuGetVersion> PackageVersions { get; } =
                    new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

                public HashSet<string> FilesUpdated { get; } = new HashSet<string>(PathComparer);

                public Context(Command command, List<Project> projects, Project packageVersionsProject)
                {
                    Command = command;
                    Projects = projects;
                    PackageVersionsProject = packageVersionsProject;
                }
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}
