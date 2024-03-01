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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.FileSystemGlobbing;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Helpers
{
    public static class MSBuildHelper
    {
        public static readonly HashSet<string> KnownProjectFileExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".csproj",
                ".fsproj",
                ".vbproj",
                ".pyproj"
            };

        public static readonly HashSet<string> KnownCodeFileExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".cs",
                ".fs",
                ".vb",
                ".py"
            };

        public static readonly HashSet<string> ExcludedDirectoryNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "bin",
                "obj",
                ".vs",
                ".git"
            };

        public static readonly ImmutableHashSet<string> ReservedMSBuildPropertyNames;

        static MSBuildHelper()
        {
            ReservedMSBuildPropertyNames =
                (new[]
                {
                    "Choose",
                    "ImportGroup",
                    "ItemGroup",
                    "MSBuildToolsVersion",
                    "MSBuildVersion",
                    "OnError",
                    "Otherwise",
                    "Output",
                    "ProjectExtensions",
                    "PropertyGroup",
                    "Target",
                    "UsingTask",
                    "VisualStudioProject",
                    "VisualStudioVersion",
                    "When",
                }).ToImmutableHashSet();
            LocateMSBuild();
        }

        public static string GetPropertyExpression(string propertyName)
        {
            return "$(" + propertyName + ")";
        }

        public static string GetPackageVersionPropertyName(Project project, string packageName)
        {
            var packageKey = packageName.Replace(".", "");
            var propName = packageKey + "Version";
            var altPropName = packageKey + "PackageVersion";
            if (ReservedMSBuildPropertyNames.Contains(propName) || project.GetProperty(altPropName) != null)
                return altPropName;
            return propName;
        }

        public static bool IsProjectFileName(string reference)
        {
            return KnownProjectFileExtensions.Contains(Path.GetExtension(reference));
        }

        public static bool IsSolutionFileName(string reference)
        {
            var ext = Path.GetExtension(reference)?.ToLower();
            return ext == ".sln" || ext == ".slnf";
        }

        public static bool IsCodeFileName(string fileName)
        {
            return KnownCodeFileExtensions.Contains(Path.GetExtension(fileName));
        }

        public static string GetProjectNameFromFileName(string projectFileName)
        {
            return Path.ChangeExtension(Path.GetFileName(projectFileName), null);
        }

        public static ProjectInSolution? FindProjectInSolution(SolutionFile solution, string projectNameOrRelativePath)
        {
            if (!IsProjectFileName(projectNameOrRelativePath))
            {
                return solution.ProjectsInOrder.SingleOrDefault(
                    p => string.Equals(p.ProjectName, projectNameOrRelativePath, StringComparison.OrdinalIgnoreCase));
            }

            return solution.ProjectsInOrder.SingleOrDefault(
                p => IsSamePath(p.RelativePath, projectNameOrRelativePath));
        }

        public static List<string> GetCodeFilesInProjectDirectory(string path, bool recursive = true)
        {
            var matcher = new Matcher();
            foreach (var extension in KnownCodeFileExtensions)
            {
                matcher.AddInclude((recursive ? "**/*" : "*") + extension);
            }

            foreach (var dirName in ExcludedDirectoryNames)
            {
                matcher.AddExclude(dirName + "/**/*");
            }

            return matcher.GetResultsInFullPath(path).ToList();
        }

        public static bool IsFileNameInNamespace(string fileName, string @namespace)
        {
            return fileName.StartsWith(@namespace + ".", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(@namespace, fileName, StringComparison.OrdinalIgnoreCase);
        }

        public static string ChangeFileNameWithNamespace(string fileName, string oldNamespace, string newNamespace)
        {
            if (!IsFileNameInNamespace(fileName, oldNamespace))
                return fileName;
            return newNamespace + fileName.Substring(oldNamespace.Length);
        }

        public static List<string> GetProjectsFromGlob(
            string pattern,
            string? workingDirectory = null,
            bool allowSolutions = true)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            workingDirectory ??= Directory.GetCurrentDirectory();

            var matcher = new Matcher();
            foreach (var segment in pattern!.Split('|'))
            {
                matcher.AddInclude(segment.Trim());
            }

            var files = matcher.GetResultsInFullPath(workingDirectory);

            var projects = new List<string>();

            foreach (var fileName in files)
            {
                var ext = Path.GetExtension(fileName);

                if (string.Equals(".sln", ext, StringComparison.OrdinalIgnoreCase))
                {
                    if (allowSolutions)
                    {
                        projects.AddRange(GetProjectsFromSolution(fileName));
                    }
                }
                else if (KnownProjectFileExtensions.Contains(ext))
                {
                    projects.Add(fileName);
                }
            }

            return RemoveProjectsInExcludedProjectDirectories(projects);
        }

        public static List<ProjectInfo> GetProjectInfosFromGlob(
            string pattern,
            string? workingDirectory = null,
            bool allowSolutions = true)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            workingDirectory ??= Directory.GetCurrentDirectory();

            var files = new List<string>();

            var matcher = new Matcher();
            foreach (var segment in pattern!.Split('|').Select(x => x.Trim()))
            {
                var fullPath = Path.GetFullPath(segment, workingDirectory);

                if (File.Exists(fullPath))
                {
                    files.Add(fullPath);
                }
                else
                {
                    matcher.AddInclude(segment);
                }
            }

            files.AddRange(matcher.GetResultsInFullPath(workingDirectory));

            var projects = new List<ProjectInfo>();

            foreach (var fileName in files)
            {
                var ext = Path.GetExtension(fileName);

                if (string.Equals(".sln", ext, StringComparison.OrdinalIgnoreCase))
                {
                    if (allowSolutions)
                    {
                        projects.AddRange(GetProjectsFromSolution(fileName).Select(p => new ProjectInfo(p, fileName)));
                    }
                }
                else if (KnownProjectFileExtensions.Contains(ext))
                {
                    projects.Add(new ProjectInfo(fileName));
                }
            }

            var includedProjectFileNames =
                RemoveProjectsInExcludedProjectDirectories(projects.Select(p => p.ProjectFileName).ToList())
                    .ToHashSet();

            return projects.Where(p => includedProjectFileNames.Contains(p.ProjectFileName)).ToList();

        }

        public static List<string> GetProjectsFromDirectory(
            string path,
            bool recursive)
        {
            var matcher = new Matcher();
            foreach (var extension in KnownProjectFileExtensions)
            {
                matcher.AddInclude((recursive ? "**/*" : "*") + extension);
            }

            var projects = matcher.GetResultsInFullPath(path).ToList();

            return RemoveProjectsInExcludedProjectDirectories(projects);
        }

        private static List<string> RemoveProjectsInExcludedProjectDirectories(List<string> projectFileNames)
        {
            var matcher = new Matcher();
            foreach (var projectFileName in projectFileNames)
            {
                foreach (var dirName in ExcludedDirectoryNames)
                {
                    matcher.AddInclude(Path.GetDirectoryName(projectFileName) + "/" + dirName + "/**/*");
                }
            }

            return projectFileNames.Where(projectFileName => !matcher.Match(projectFileName).HasMatches).ToList();
        }

        public static List<string> GetProjectsFromSolution(string solutionFileName)
        {
            var sln = SolutionFile.Parse(solutionFileName);
            return sln.ProjectsInOrder
                .Where(p => KnownProjectFileExtensions.Contains(Path.GetExtension(p.AbsolutePath!)))
                .Select(p => p.AbsolutePath)
                .ToList();
        }

        public static string GetSolutionFromDirectory(string path)
        {
            try
            {
                return GetSolutionsFromDirectory(path).Single();
            }
            catch
            {
                throw new InvalidOperationException(
                    "Could not find solution file, or multiple solution files were found.");
            }
        }

        public static List<string> GetSolutionsFromDirectory(string path)
        {
            var matcher = new Matcher();
            matcher.AddInclude("*.sln");
            return matcher.GetResultsInFullPath(path).ToList();
        }

        public static Project LoadProjectFromFile(string fileName, ProjectOptions? projectOptions = null)
        {
            projectOptions ??= new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreInvalidImports,
                ProjectCollection = new ProjectCollection()
            };

            return Project.FromProjectRootElement(
                ProjectRootElement.Open(
                    fileName,
                    projectOptions.ProjectCollection,
                    preserveFormatting: true),
                projectOptions);
        }

        public static Project LoadProjectFromXmlString(string xml, ProjectOptions? projectOptions = null)
        {
            projectOptions ??= new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreInvalidImports,
                ProjectCollection = new ProjectCollection()
            };

            return Project.FromProjectRootElement(
                ProjectRootElement.Create(
                    XmlReader.Create(new StringReader(xml)),
                    projectOptions.ProjectCollection,
                    preserveFormatting: true),
                projectOptions);
        }

        public static List<Project> LoadProjects(
            IEnumerable<string> fileNames,
            ProjectCollection? projectCollection = null)
        {
            var projectOptions = new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.IgnoreInvalidImports,
                ProjectCollection = projectCollection ?? new ProjectCollection()
            };

            return fileNames.Select(
                    fileName => LoadProjectFromFile(fileName, projectOptions))
                .ToList();
        }

        public static SolutionFile ValidateSolution(SolutionFile solution)
        {
            var projectFileNames = new List<string>();
            foreach (var project in solution.ProjectsInOrder)
            {
                if (!File.Exists(project.AbsolutePath))
                    throw new InvalidOperationException(
                        $"The solution references an invalid project path: \"{project.RelativePath}\"");
                projectFileNames.Add(project.AbsolutePath);
            }

            LoadProjects(projectFileNames);

            return solution;
        }

        public static SolutionFile LoadAndValidateSolution(string solutionFileName)
        {
            return ValidateSolution(SolutionFile.Parse(solutionFileName));
        }

        public static void AddItemRemove(this Project project, string itemType, string unevaluatedRemove)
        {
            var item = project.Xml.AddItem(itemType, "!" + unevaluatedRemove);
            item.Include = "";
            item.Remove = unevaluatedRemove;
        }

        public static void AddItemRemove(string projectFileName, string itemType, string unevaluatedRemove)
        {
            var project = LoadProjectFromFile(projectFileName);
            AddItemRemove(project, itemType, unevaluatedRemove);
            project.Save();
        }

        public static void AddProjectReference(Project project, string referencedProjectFileName)
        {
            var reference = NormalizePath(
                Path.GetRelativePath(project.DirectoryPath, referencedProjectFileName));
            project.AddItemFast("ProjectReference", reference);
        }

        public static void AddProjectReference(string projectFileName, string referencedProjectFileName)
        {
            var project = LoadProjectFromFile(projectFileName);
            AddProjectReference(project, referencedProjectFileName);
            project.Save();
        }

        public static void AddPackageReference(Project project, string packageId, string? version)
        {
            project.AddItemFast(
                "PackageReference",
                packageId,
                version != null 
                    ? new[] { new KeyValuePair<string, string>("Version", version) }
                    : null);
        }

        public static void AddPackageReference(string projectFileName, string packageId, string? version)
        {
            var project = LoadProjectFromFile(projectFileName);
            AddPackageReference(project, packageId, version);
            project.Save();
        }

        public static void AddAssemblyReference(Project project, string assemblyName)
        {
            project.AddItemFast(
                "Reference",
                assemblyName);
        }

        public static void AddAssemblyReference(string projectFileName, string assemblyName)
        {
            var project = LoadProjectFromFile(projectFileName);
            AddAssemblyReference(project, assemblyName);
            project.Save();
        }

        public static ProjectItem? FindProjectReference(Project project, string referencedProjectFileName)
        {
            var reference = NormalizePath(
                Path.GetRelativePath(project.DirectoryPath, referencedProjectFileName));
            return project.AllEvaluatedItems.FirstOrDefault(
                i => i.ItemType == "ProjectReference" && IsSamePath(i.UnevaluatedInclude, reference));
        }

        public static ProjectItem? FindProjectReference(string projectFileName, string referencedProjectFileName)
        {
            var project = LoadProjectFromFile(projectFileName);
            return FindProjectReference(project, referencedProjectFileName);
        }

        public static ProjectItem? FindAssemblyReference(Project project, string assemblyName)
        {
            return project.AllEvaluatedItems.FirstOrDefault(
                i => i.ItemType == "Reference" && string.Equals(i.UnevaluatedInclude, assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        public static ProjectItem? FindAssemblyReference(string projectFileName, string assemblyName)
        {
            var project = LoadProjectFromFile(projectFileName);
            return FindAssemblyReference(project, assemblyName);
        }

        public static ProjectItem? FindPackageReference(Project project, string packageId)
        {
            return project.AllEvaluatedItems.FirstOrDefault(
                i => i.ItemType == "PackageReference" && i.UnevaluatedInclude == packageId);
        }

        public static ProjectItem? FindPackageReference(string projectFileName, string packageId)
        {
            var project = LoadProjectFromFile(projectFileName);
            return FindPackageReference(project, packageId);
        }

        public static void RemoveChildren(this ProjectElementContainer container, IEnumerable<ProjectElement> elements)
        {
            foreach (var element in elements)
            {
                container.RemoveChild(element);
            }
        }

        public static void AddChildren(this ProjectElementContainer container, IEnumerable<ProjectElement> elements)
        {
            foreach (var element in elements)
            {
                container.AppendChild(element);
            }
        }

        public static string? GetUnevaluatedMetadataValue(this ProjectItem item, string metadataName)
        {
            var metadata = item.GetMetadata(metadataName);
            return metadata?.UnevaluatedValue;
        }

        public static ProjectMetadataElement? FindMetadata(this ProjectItemElement element, string metadataName)
        {
            return element.Metadata.FirstOrDefault(x => x.Name == metadataName);
        }

        public static string? GetMetadataValue(this ProjectItemElement element, string metadataName)
        {
            return element.FindMetadata(metadataName)?.Value;
        }

        public static void SetMetadataValue(
            this ProjectItemElement element,
            string metadataName,
            string? metadataValue,
            bool expressAsAttribute = false)
        {
            var metadata = element.FindMetadata(metadataName);
            if (metadata?.Value == metadataValue) return;
            metadata?.Parent.RemoveChild(metadata);
            if (metadataValue != null)
            {
                element.AddMetadata(metadataName, metadataValue, expressAsAttribute);
            }
        }

        private static bool _msBuildLocated;

        public static void LocateMSBuild()
        {
            if (_msBuildLocated) return;

            var sdkList = ProcessHelper.Run("dotnet", "--list-sdks");

            var sdks = Regex.Matches(sdkList, @"([\d\.]+) *\[(.*)\]")
                .Select(
                    m => new
                    {
                        Version = new Version(m.Groups[1].Value),
                        Path = Path.Combine(m.Groups[2].Value, m.Groups[1].Value)
                    })
                .OrderBy(x => x.Version)
                .ToList();

            // TODO: Check global.json and use specific SDK?
            var sdk = sdks.LastOrDefault() ?? throw new InvalidOperationException("Could not locate the .NET SDK");
            Environment.SetEnvironmentVariable("MSBuildSDKsPath", Path.Combine(sdk.Path, "Sdks"));
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdk.Path, "MSBuild.dll"));
            _msBuildLocated = true;
        }

        private static readonly Regex VSVersionRegex = new Regex(@"v\d+");

        /// <summary>
        /// Returns the root directory for the specified solution under the hidden .vs directory, eg. 
        /// <c>.vs/SolutionName</c>
        /// </summary>
        /// <param name="solutionFileName"></param>
        /// <returns></returns>
        public static string GetHiddenVSDirectoryRoot(string solutionFileName)
        {
            var hiddenDirectoryName = Path.GetExtension(solutionFileName)?.ToLower() switch
            {
                ".slnf" => Path.GetFileName(solutionFileName),
                _ => Path.ChangeExtension(Path.GetFileName(solutionFileName), null)
            };
            return Path.Combine(Path.GetDirectoryName(solutionFileName), ".vs", hiddenDirectoryName);
        }

        /// <summary>
        /// Returns a collection of directories matching the specified name
        /// in any of the version-specific hidden directories for the solution, eg.
        /// <c>.vs/SolutionName/v17/name</c>
        /// </summary>
        /// <param name="solutionFileName"></param>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetHiddenVSDirectories(string solutionFileName, string directoryName)
        {
            return GetHiddenVSDirectoryItems(solutionFileName, directoryName).Where(Directory.Exists);
        }

        /// <summary>
        /// Returns a collection of file names matching the specified globbing pattern
        /// in any of the version-specific hidden directories for the solution, eg.
        /// <c>.vs/SolutionName/v17/pattern</c>
        /// </summary>
        /// <param name="solutionFileName"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetHiddenVSFiles(string solutionFileName, string fileName)
        {
            return GetHiddenVSDirectoryItems(solutionFileName, fileName).Where(File.Exists);
        }

        private static IEnumerable<string> GetHiddenVSDirectoryItems(string solutionFileName, string name)
        {
            return Directory.GetDirectories(GetHiddenVSDirectoryRoot(solutionFileName))
                .Where(dir => VSVersionRegex.IsMatch(Path.GetFileName(dir)!))
                .Select(dir => Path.Combine(dir, name));
        }
    }

    public readonly record struct ProjectInfo(string ProjectFileName, string? SolutionFileName = null);
}