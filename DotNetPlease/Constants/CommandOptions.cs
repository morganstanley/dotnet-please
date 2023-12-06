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

namespace DotNetPlease.Constants
{
    public static class CommandOptions
    {
        public static class Stage
        {
            public const string Alias = "--stage";
            public const string Description = "Don't apply changes, just list what needs to be done";
        }

        public static class Workspace
        {
            public const string Alias = "--workspace";

            public const string Description =
                "Specify the solutions or projects to work on. When omitted, the workspace is inferred "
                + "from the directory hierarchy, resolving a single solution, a project file, or all projects "
                + "under the current working directory, recursively.";
        }
    }
}
