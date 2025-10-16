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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace DotNetPlease.Helpers
{
    public static class FileSystemHelper
    {
        private static readonly Regex DirectorySeparatorRegex = new(@"[\\/]");

        public static bool IsSamePath(string path1, string path2)
        {
            return PathComparer.Equals(path1, path2);
        }

        public static string NormalizePathSeparators(string? path) =>
            DirectorySeparatorRegex.Replace(path ?? "", "/").TrimEnd('/');

        public static string GetNormalizedRelativePath(string relativeTo, string path) =>
            NormalizePathSeparators(Path.GetRelativePath(relativeTo, path));

        public static readonly StringComparer PathComparer = new PathComparerImpl();

        public static IEnumerable<string> GetFileNamesFromGlob(string globbingPattern, string workingDirectory)
        {
            var matcher = new Matcher();
            foreach (var segment in globbingPattern.Split('|'))
            {
                matcher.AddInclude(segment);
            }

            return matcher.GetResultsInFullPath(workingDirectory);
        }

        public static void CopyDirectory(string path, string newPath, string? globbingPattern = null)
        {
            Directory.CreateDirectory(newPath);
            var fileNames = GetFileNamesFromGlob(globbingPattern ?? "**/*", path);
            foreach (var fileName in fileNames.Where(File.Exists))
            {
                var relativePath = Path.GetRelativePath(path, fileName);
                var destFileName = Path.Combine(newPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFileName));
                File.Copy(fileName, destFileName, overwrite: true);
            }
        }

        /// <summary>
        /// Find a file closest to the working directory in the directory tree (like Directory.Build.props)
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string? GetFilePathAbove(string fileName)
        {
            return GetFilePathAbove(fileName, Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Find a file closest to the specified directory in the directory tree (like Directory.Build.props)
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public static string? GetFilePathAbove(string fileName, string directoryPath)
        {
            while (directoryPath != null!)
            {
                var localFilePath = Path.Combine(directoryPath, fileName);
                if (File.Exists(localFilePath)) return localFilePath;
                directoryPath = Path.GetDirectoryName(directoryPath)!;
            }

            return null;
        }

        private class PathComparerImpl : StringComparer
        {
            public override int Compare(string? x, string? y)
            {
                if (string.IsNullOrWhiteSpace(x) && string.IsNullOrWhiteSpace(y))
                    return 0;
                return string.Compare(
                    Path.GetFullPath(NormalizePathSeparators(x)), 
                    Path.GetFullPath(NormalizePathSeparators(y)),
                    StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(string? x, string? y)
            {
                if (string.IsNullOrWhiteSpace(x))
                    return string.IsNullOrWhiteSpace(y);
                if (string.IsNullOrWhiteSpace(y))
                    return false;

                return string.Equals(
                    Path.GetFullPath(NormalizePathSeparators(x)),
                    Path.GetFullPath(NormalizePathSeparators(y)),
                    StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode(string obj)
            {
                if (string.IsNullOrWhiteSpace(obj))
                    return 0;
                obj = Path.GetFullPath(NormalizePathSeparators(obj));
                return obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}