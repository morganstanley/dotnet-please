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

using DotNetPlease.Services.Reporting.Abstractions;
using System;
using System.IO;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace
    {
        public void CreateDirectory(string path)
        {
            if (IsStaging) return;
            Directory.CreateDirectory(GetFullPath(path));
        }

        public bool TryDeleteFile(string fileName)
        {
            fileName = GetFullPath(fileName);
            if (!File.Exists(fileName))
                return false;

            string relativePath = GetRelativePath(fileName);

            if (IsStaging)
            {
                Reporter.Success($"Delete {relativePath}");
                return true;
            }

            try
            {
                File.Delete(fileName);
                Reporter.Success($"Deleted {relativePath}");
                return true;
            }
            catch (Exception e)
            {
                Reporter.Error($"Failed to delete {relativePath} ({e.Message})");
                return false;
            }
        }

        public bool TryDeleteDirectory(string path)
        {
            path = GetFullPath(path);

            if (!Directory.Exists(path))
                return false;

            string relativePath = GetRelativePath(path);

            if (IsStaging)
            {
                Reporter.Success($"Delete {relativePath}");
                return true;
            }

            try
            {
                Directory.Delete(path, true);
                Reporter.Success($"Deleted {relativePath}");
                return true;
            }
            catch (Exception e)
            {
                Reporter.Error($"Failed to delete {relativePath} ({e.Message})");
                return false;
            }
        }

        public bool TryMoveFile(string oldPath, string newPath, bool overwrite = false)
        {
            oldPath = GetFullPath(oldPath);
            newPath = GetFullPath(newPath);

            if (!File.Exists(oldPath))
                return false;

            var renaming = IsSamePath(Path.GetDirectoryName(oldPath)!, Path.GetDirectoryName(newPath)!);
            var oldRelativePath = GetRelativePath(oldPath);
            var newNameOrRelativePath = renaming ? Path.GetFileName(newPath) : GetRelativePath(newPath);
            var (verb, completedVerb) = renaming
                ? (verb: "Rename", completedVerb: "Renamed")
                : (verb: "Move", completedVerb: "Moved");

            if (IsStaging)
            {
                if (File.Exists(newPath) && !overwrite)
                {
                    Reporter.Error($"Cannot {verb.ToLower()} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\" because the destination already exists");
                    return false;
                }
                Reporter.Success($"{verb} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\"");
                return true;
            }

            try
            {
                File.Move(oldPath, newPath, overwrite);
                Reporter.Success($"{completedVerb} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\"");
                return true;
            }
            catch (Exception e)
            {
                Reporter.Error($"Failed to {verb.ToLower()} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\" ({e.Message})");
                return false;
            }
        }

        public bool TryMoveDirectory(string oldPath, string newPath)
        {
            oldPath = GetFullPath(oldPath);
            newPath = GetFullPath(newPath);

            if (!Directory.Exists(oldPath))
                return false;

            bool renaming = IsSamePath(Path.GetDirectoryName(oldPath)!, Path.GetDirectoryName(newPath)!);
            string oldRelativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), oldPath);
            string newNameOrRelativePath = renaming ? Path.GetFileName(newPath) : Path.GetRelativePath(Directory.GetCurrentDirectory(), newPath);
            var (verb, completedVerb) = renaming
                ? (verb: "Rename", completedVerb: "Renamed")
                : (verb: "Move", completedVerb: "Moved");

            if (IsStaging)
            {
                Reporter.Success($"{verb} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\"");
                return true;
            }

            try
            {
                Directory.Move(oldPath, newPath);
                Reporter.Success($"{completedVerb} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\"");
                return true;
            }
            catch (Exception e)
            {
                Reporter.Error($"Failed to {verb.ToLower()} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\" ({e.Message})");
                return false;
            }
        }
    }
}