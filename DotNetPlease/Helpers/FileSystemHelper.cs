using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DotNetPlease.Helpers
{
    public static class FileSystemHelper
    {
        private static readonly Regex DirectorySeparatorRegex = new Regex(@"[\\/]");

        public static bool IsSamePath(string path1, string path2)
        {
            return string.Equals(NormalizePath(path1), NormalizePath(path2), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePath(string? path) => DirectorySeparatorRegex.Replace(path ?? "", "/").TrimEnd('/');

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

        private class PathComparerImpl : StringComparer
        {
            public override int Compare(string? x, string? y)
            {
                if (string.IsNullOrWhiteSpace(x) && string.IsNullOrWhiteSpace(y))
                    return 0;
                return string.Compare(NormalizePath(x), NormalizePath(y), StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(string? x, string? y)
            {
                if (string.IsNullOrWhiteSpace(x))
                    return string.IsNullOrWhiteSpace(y);
                if (string.IsNullOrWhiteSpace(y))
                    return false;
                return IsSamePath(x, y);
            }

            public override int GetHashCode(string obj)
            {
                if (string.IsNullOrWhiteSpace(obj))
                    return 0;
                obj = NormalizePath(obj);
                return obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}