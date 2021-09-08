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


using System.Diagnostics;

namespace DotNetPlease.Services.Reporting.Abstractions
{
    public static class ReporterExtensions
    {
        public static void Info(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Information);

        [Conditional("DEBUG")]
        public static void Debug(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Debug);

        public static void Success(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Success);

        public static void Warning(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Warning);

        public static void Error(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Error);

    }
}