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
using SystemConsole = System.Console;

namespace DotNetPlease.Services.Reporting.Console
{
    public class SystemConsoleReporter : IReporter
    {
        public IDisposable BeginScope(string scope)
        {
            if (_currentScope != null)
                throw new InvalidOperationException("Cannot create nested scope");

            lock (_lock)
            {
                _currentScope = scope;
                _currentScopeHasOutput = false;
                if (!SystemConsole.IsOutputRedirected)
                {
                    SystemConsole.Out.WriteLine(scope);
                }
                return new Scope(this);
            }
        }

        private void EndScope()
        {
            lock (_lock)
            {
                if (_currentScope != null && !_currentScopeHasOutput && !SystemConsole.IsOutputRedirected)
                {
                    var (left, top) = (SystemConsole.CursorLeft, SystemConsole.CursorTop);
                    SystemConsole.SetCursorPosition(0, top - 1);
                    SystemConsole.Write(new string(' ', _currentScope.Length));
                    SystemConsole.SetCursorPosition(0, top - 1);
                }

                _currentScope = null;
                _currentScopeHasOutput = false;
            }
        }

        public void Message(string message, MessageType type = MessageType.Information)
        {
            lock (_lock)
            {
                if (_currentScope != null)
                {
                    if (!_currentScopeHasOutput && SystemConsole.IsOutputRedirected)
                    {
                        WriteLine(SystemConsole.Out, _currentScope);
                    }

                    message = "    " + message;
                    _currentScopeHasOutput = true;
                }

                switch (type)
                {
                    case MessageType.Information:
                        WriteLine(SystemConsole.Out, message);
                        break;
                    case MessageType.Success:
                        WriteLine(SystemConsole.Out, message, ConsoleColor.Green);
                        break;
                    case MessageType.Debug:
                        WriteLine(SystemConsole.Out, message, ConsoleColor.DarkGray);
                        break;
                    case MessageType.Warning:
                        WriteLine(SystemConsole.Out, message, ConsoleColor.DarkYellow);
                        break;
                    case MessageType.Error:
                        WriteLine(SystemConsole.Error, message, ConsoleColor.Red);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }
        }
        private string? _currentScope;
        private bool _currentScopeHasOutput;
        private readonly object _lock = new object();

        private void WriteLine(
            TextWriter textWriter,
            string? text = null,
            ConsoleColor? foregroundColor = null,
            ConsoleColor? backgroundColor = null)
        {
            lock (_lock)
            {
                if (text == null)
                {
                    textWriter.WriteLine();
                    return;
                }



                if (foregroundColor.HasValue)
                {
                    SystemConsole.ForegroundColor = foregroundColor.Value;
                }

                if (backgroundColor.HasValue)
                {
                    SystemConsole.BackgroundColor = backgroundColor.Value;
                }

                if (textWriter == SystemConsole.Out)
                {
                    SystemConsole.WriteLine(text);
                }
                else
                {
                    textWriter.WriteLine(text);
                }

                if (foregroundColor.HasValue || backgroundColor.HasValue)
                {
                    SystemConsole.ResetColor();
                }
            }
        }

        private class Scope : IDisposable
        {
            private readonly SystemConsoleReporter _reporter;

            public Scope(SystemConsoleReporter reporter)
            {
                _reporter = reporter;
            }

            public void Dispose()
            {
                _reporter.EndScope();
            }
        }
    }

}