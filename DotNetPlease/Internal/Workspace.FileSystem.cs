﻿// Morgan Stanley makes this available to you under the Apache License,
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
using System.IO;
using DotNetPlease.Services.Reporting.Abstractions;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace
    {
        /// <summary>
        /// Create the specified directory, unless it's a dry-run
        /// </summary>
        /// <param name="path"></param>
        public void SafeCreateDirectory(string path)
        {
            if (IsDryRun) return;
            Directory.CreateDirectory(GetFullPath(path));
        }

        /// <summary>
        /// Delete the file, if it's not a dry-run.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>True if the operation succeeded.</returns>
        public bool SafeDeleteFile(string fileName)
        {
            fileName = GetFullPath(fileName);
            if (!File.Exists(fileName))
                return false;

            string relativePath = GetRelativePath(fileName);

            if (IsDryRun)
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

        /// <summary>
        /// Delete the specified directory if it's not a dry-run.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if the operation succeeded.</returns>
        public bool SafeDeleteDirectory(string path)
        {
            path = GetFullPath(path);

            if (!Directory.Exists(path))
                return false;

            string relativePath = GetRelativePath(path);

            if (IsDryRun)
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

        /// <summary>
        /// Move file if it's not a dry-run.
        /// </summary>
        /// <param name="oldPath"></param>
        /// <param name="newPath"></param>
        /// <param name="overwrite"></param>
        /// <returns>True if the operation succeeded.</returns>
        public bool SafeMoveFile(string oldPath, string newPath, bool overwrite = false)
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

            if (IsDryRun)
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

        /// <summary>
        /// Move a directory if it's not a dry-run
        /// </summary>
        /// <param name="oldPath"></param>
        /// <param name="newPath"></param>
        /// <returns>True if the operation succeeded.</returns>
        public bool SafeMoveDirectory(string oldPath, string newPath)
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

            if (IsDryRun)
            {
                Reporter.Success($"{verb} \"{oldRelativePath}\" to \"{newNameOrRelativePath}\"");
                return true;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
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