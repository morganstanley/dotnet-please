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

using System.Collections.Generic;
using System.Diagnostics;

namespace DotNetPlease.Helpers
{
    public static class ProcessHelper
    {
        public static string Run(string command, string? arguments, IDictionary<string, string?>? environmentVariables = null)
        {
            var startInfo = new ProcessStartInfo(command, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (environmentVariables != null)
            {
                foreach (var envVar in environmentVariables)
                {
                    if (envVar.Value is null)
                    {
                        startInfo.Environment.Remove(envVar.Key);
                    }
                    else
                    {
                        startInfo.Environment[envVar.Key] = envVar.Value;
                    }
                }
            }

            var process = Process.Start(startInfo)!;
            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            return output;
        }
    }
}