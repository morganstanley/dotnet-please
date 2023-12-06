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

using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class ConsolidatePackages
    {
        [Command(
            "consolidate-packages",
            "Updates PackageReferences to the highest version found in the solution. Does not actually run any nuget commands.")]
        public class Command : IRequest
        {
            [Option(
                "--props",
                "The name of an optional .props file where the consolidated versions are saved. If provided, the version numbers in project references are replaced with MSBuild properties")]
            public string? PropsFileName { get; set; }

            [Option("--explicit", "Force explicit version numbers instead of properties")]
            public bool UseExplicitVersionsNumbers { get; set; }

            [Option(
                "--package",
                "Limit the command to specific packages. The '*' character can be used as a wildcard.")]
            public string? PackageName { get; set; }

            [Option(
                "--version",
                "Set the package version explicitly. Only used when --package is also set.")]
            public string? Version { get; set; }

            [Option(
                "--cleanup",
                "Clean up the props file by removing unused and redundant properties")]
            public bool Cleanup { get; set; }

            [Option("--force", "Overwrite versions even if they are already defined with an expression")]
            public bool Force { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                if (!string.IsNullOrWhiteSpace(command.Version) && string.IsNullOrWhiteSpace(command.PackageName))
                {
                    throw new ValidationException(
                        "Cannot set specific version for all packages. Please also provide --package");
                }

                Reporter.Info($"Consolidating package versions");

                command.Version = command.Version?.Trim();
                command.PackageName = command.PackageName?.Trim();

                var context = new Context(command);

                if (!string.IsNullOrWhiteSpace(command.PackageName))
                {
                    context.PackageNameRegex = CreateRegexForPackageName(command.PackageName!);
                }

                Project? props = null;

                if (command.PropsFileName != null)
                {
                    props = LoadProjectFromFile(Workspace.GetFullPath(command.PropsFileName));
                    context.UseProperties = true;
                }

                context.Projects = Workspace.LoadProjects();

                foreach (var project in context.Projects)
                {
                    VisitProject(project, context);
                }

                if (context.UseProperties)
                {
                    UpdateProperties(props!, context);
                    if (command.Cleanup)
                    {
                        CleanupProperties(props!, context);
                    }
                }

                foreach (var project in context.Projects)
                {
                    UpdateProject(project, context);
                }

                if (context.FilesUpdated.Count == 0)
                {
                    Reporter.Success($"Nothing to update");
                }

                return Task.CompletedTask;
            }

            private void VisitProject(Project project, Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(project.FullPath);

                using (Reporter.BeginScope($"Reading project {projectRelativePath}"))
                {
                    var packageReferences =
                        project.Items.Where(i => i.ItemType == "PackageReference");

                    foreach (var packageReference in packageReferences)
                    {
                        VisitPackageReference(project, packageReference, context);
                    }
                }
            }

            private void VisitPackageReference(
                Project project,
                ProjectItem packageReference,
                Context context)
            {
                var packageName = packageReference.EvaluatedInclude;

                if (context.UseProperties && !context.PropertyNames.ContainsKey(packageName))
                {
                    context.PropertyNames[packageName] = GetPackageVersionPropertyName(project, packageName);
                }

                if (context.PackageNameRegex != null
                    && !context.PackageNameRegex.IsMatch(packageName))
                {
                    return;
                }

                var versionAttribute = packageReference.GetMetadataValue("Version");
                if (string.IsNullOrEmpty(versionAttribute))
                    return;

                if (NuGetVersion.TryParse(versionAttribute, out var version)
                    && (!context.ConsolidatedVersions.TryGetValue(packageName, out var consolidatedVersion)
                        || version > consolidatedVersion))
                {
                    context.ConsolidatedVersions[packageName] = version;
                }
            }

            private void UpdateProject(Project project, Context context)
            {
                var projectRelativePath = Workspace.GetRelativePath(project.FullPath);

                using (Reporter.BeginScope($"Project: {projectRelativePath}"))
                {
                    var packageReferences =
                        project.Items.Where(i => !i.IsImported && i.ItemType == "PackageReference");

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
                ProjectItem packageReference,
                Context context)
            {
                if (packageReference.IsImported)
                    return;

                var packageName = packageReference.EvaluatedInclude;

                if (context.PackageNameRegex != null
                    && !context.PackageNameRegex.IsMatch(packageName))
                {
                    return;
                }

                var versionAttribute = packageReference.Metadata.FirstOrDefault(x => x.Name == "Version");

                if (versionAttribute == null)
                {
                    packageReference.Xml.AddMetadata("Version", "", true);
                    packageReference.Project.ReevaluateIfNecessary();
                    versionAttribute = packageReference.Metadata.FirstOrDefault(x => x.Name == "Version");
                    if (versionAttribute == null)
                    {
                        Reporter.Error($"Invalid PackageReference \"{packageName}\"");
                        return;
                    }
                }

                if (versionAttribute.UnevaluatedValue != versionAttribute.EvaluatedValue)
                {
                    if (!NuGetVersion.TryParse(versionAttribute.EvaluatedValue, out _))
                    {
                        Reporter.Error(
                            $"Invalid PackageReference \"{packageName}\" \"{versionAttribute.UnevaluatedValue}\" = \"{versionAttribute.EvaluatedValue}\"");
                    }

                    if (!context.Command.Force)
                        return;
                }

                var newVersion = context.Command.Version
                                 ?? context.ConsolidatedVersions.GetValueOrDefault(packageName)?.ToString();

                if (context.UseProperties
                    && context.PropertyNames.TryGetValue(packageName, out var propertyName)
                    && !context.Command.UseExplicitVersionsNumbers)
                {
                    newVersion = GetPropertyExpression(propertyName);
                }

                if (newVersion != versionAttribute.UnevaluatedValue)
                {
                    Reporter.Success(
                        $"Update PackageReference \"{packageName}\" \"{versionAttribute.UnevaluatedValue}\" => \"{newVersion}\"");
                    versionAttribute.Xml.Value = newVersion;
                }
            }

            private void UpdateProperties(
                Project props,
                Context context)
            {
                using (Reporter.BeginScope($"Updating {Workspace.GetRelativePath(props.FullPath)}"))
                {
                    ProjectPropertyGroupElement? newProperties = null;

                    foreach (var version in context.ConsolidatedVersions.OrderBy(i => i.Key))
                    {
                        if (context.PackageNameRegex != null && !context.PackageNameRegex.IsMatch(version.Key))
                        {
                            continue;
                        }

                        var propName = context.PropertyNames[version.Key];
                        var prop = props.GetProperty(propName);
                        var actualVersion =
                            string.IsNullOrWhiteSpace(context.Command.Version)
                                ? version.Value.ToString()
                                : context.Command.Version;

                        if (prop == null)
                        {
                            AddNewProperty(propName, actualVersion);
                        }
                        else
                        {
                            if (prop.IsImported)
                            {
                                if (prop.EvaluatedValue != actualVersion)
                                {
                                    Reporter.Warning(
                                        $"{propName} is defined with a different value in an imported file. Inherited value: \"{prop.EvaluatedValue}\"");
                                    AddNewProperty(propName, actualVersion);
                                }
                            }
                            else if (prop.UnevaluatedValue != prop.EvaluatedValue)
                            {
                                // skip
                            }
                            else if (prop.UnevaluatedValue != actualVersion)
                            {
                                Reporter.Success(
                                    $"Update {propName} \"{prop.UnevaluatedValue}\" => \"{actualVersion}\"");
                                prop.Xml.Value = actualVersion;
                            }
                        }
                    }

                    if (props.Xml.HasUnsavedChanges)
                    {
                        if (newProperties != null)
                        {
                            var list = newProperties.AllChildren.OrderBy(p => p.ElementName).ToList();
                            newProperties.RemoveAllChildren();
                            list.ForEach(newProperties.AppendChild);
                        }

                        if (!Workspace.IsStaging)
                        {
                            props.Save();
                        }

                        context.FilesUpdated.Add(props.FullPath);
                    }

                    void AddNewProperty(string name, string value)
                    {
                        newProperties ??= props.Xml.PropertyGroups.FirstOrDefault(
                                              g => g.Properties.Any(p => p.Name.EndsWith("Version")))
                                          ?? props.Xml.AddPropertyGroup();
                        newProperties.AddProperty(name, value);
                        Reporter.Success($"Add {name} = \"{value}\"");
                    }
                }
            }

            private void CleanupProperties(
                Project props,
                Context context)
            {
                using (Reporter.BeginScope($"Cleaning {Workspace.GetRelativePath(props.FullPath)}"))
                {
                    var importedProperties = props.Imports
                        .SelectMany(i => i.ImportedProject.Properties)
                        .ToDictionary(p => p.Name);

                    var allProjects = context.Projects.Concat(new[] {props});

                    var expressions = allProjects
                        .SelectMany(
                            proj => proj.AllEvaluatedProperties
                                .Where(
                                    p => !p.IsGlobalProperty
                                         && !p.IsEnvironmentProperty
                                         && !p.IsReservedProperty
                                         && !p.IsImported
                                         && p.UnevaluatedValue != p.EvaluatedValue)
                                .Select(p => p.UnevaluatedValue)
                                .Concat(proj.ConditionedProperties.Keys.Select(p => GetPropertyExpression(p))))
                        .Concat(
                            allProjects.SelectMany(
                                proj => proj.AllEvaluatedItems.Select(i => i.UnevaluatedInclude)))
                        .Concat(
                            allProjects.SelectMany(
                                proj => props.AllEvaluatedItems.SelectMany(
                                    i => i.Metadata.Select(m => m.UnevaluatedValue))))
                        .ToHashSet();

                    foreach (var property in props.AllEvaluatedProperties
                        .Where(
                            p => !p.IsImported
                                 && !p.IsGlobalProperty
                                 && !p.IsEnvironmentProperty
                                 && !p.IsReservedProperty)
                        .ToList())
                    {
                        var propertyExpression = GetPropertyExpression(property.Name);

                        if (!context.PropertyNames.Any(x => x.Value == property.Name)
                            && !expressions.Any(x => x.Contains(propertyExpression)))
                        {
                            Reporter.Success($"Remove {property.Name} (unused)");
                            property.Xml.Parent.RemoveChild(property.Xml);
                        }

                        var importedProperty = importedProperties.GetValueOrDefault(property.Name);
                        if (importedProperty != null)
                        {
                            if (importedProperty.Value == property.UnevaluatedValue)
                            {
                                Reporter.Success($"Remove {property.Name} (redundant)");
                                property.Xml.Parent.RemoveChild(property.Xml);
                            }
                            else
                            {
                                var change = "different value";
                                if (NuGetVersion.TryParse(importedProperty.Value, out var importedVersion)
                                    && NuGetVersion.TryParse(property.UnevaluatedValue, out var currentVersion))
                                {
                                    if (importedVersion < currentVersion)
                                    {
                                        change = "lower version";
                                    }
                                    else if (importedVersion > currentVersion)
                                    {
                                        change = "higher version";
                                    }
                                }

                                Reporter.Warning(
                                    $"Property {property.Name} is defined with a {change} in {Workspace.GetRelativePath(importedProperty.ContainingProject.FullPath)}");
                            }
                        }
                    }

                    if (props.Xml.HasUnsavedChanges)
                    {
                        if (!Workspace.IsStaging)
                        {
                            props.Save();
                        }

                        context.FilesUpdated.Add(props.FullPath);
                    }
                }
            }

            private static Regex CreateRegexForPackageName(string pattern)
            {
                pattern = pattern.Replace(".", "\\.").Replace("*", ".*?");
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            }

            private class Context
            {
                public Command Command { get; }

                public Dictionary<string, NuGetVersion> ConsolidatedVersions { get; } =
                    new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

                public HashSet<string> FilesUpdated { get; } = new HashSet<string>(PathComparer);
                public bool UseProperties { get; set; }

                public Dictionary<string, string> PropertyNames { get; } =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                public Regex? PackageNameRegex { get; set; }

                public List<Project> Projects { get; set; } = null!;

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