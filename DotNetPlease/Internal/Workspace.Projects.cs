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

using DotNetPlease.Helpers;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace
    {
        public List<string> GetProjects(string? searchPattern = null)
        {
            if (searchPattern != null)
            {
                return GetProjectsFromGlob(searchPattern, WorkingDirectory, allowSolutions: true);
            }

            var solutionFileName = FindSolutionFileName();

            return solutionFileName != null 
                ? GetProjectsFromSolution(solutionFileName) 
                : GetProjectsFromDirectory(WorkingDirectory, recursive: true);
        }

        public List<Project> LoadProjects(string? searchPattern)
        {
            return MSBuildHelper.LoadProjects(GetProjects(searchPattern));
        }

        public string? FindProject(string projectNameOrRelativePath, string? solutionFileName = null)
        {
            solutionFileName ??= FindSolutionFileName();

            if (solutionFileName != null)
            {
                solutionFileName = GetFullPath(solutionFileName);
                var solution = SolutionFile.Parse(solutionFileName);
                var project = FindProjectInSolution(solution, projectNameOrRelativePath);
                return project?.AbsolutePath;
            }

            if (!IsProjectFileName(projectNameOrRelativePath))
            {
                return GetProjects()
                    .FirstOrDefault(
                        p => string.Equals(
                            projectNameOrRelativePath,
                            GetProjectNameFromFileName(p),
                            StringComparison.OrdinalIgnoreCase));
            }

            var projectFileName = GetFullPath(projectNameOrRelativePath);
            return File.Exists(projectFileName) ? projectFileName : null;

        }

        public string? FindSolutionFileName(string? fileName = null)
        {
            if (fileName != null)
            {
                fileName = GetFullPath(fileName);
                return File.Exists(fileName) && IsSolutionFileName(fileName)
                    ? fileName
                    : null;
            }
            var solutionFileNames = GetSolutionsFromDirectory(WorkingDirectory);
            return solutionFileNames.Count == 1 
                ? solutionFileNames[0] : 
                null;
        }
    }
}