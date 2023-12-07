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
using System.CommandLine;
using System.CommandLine.IO;
using Xunit.Abstractions;

namespace DotNetPlease.TestUtils;

public sealed class TestOutputConsole : IConsole, IDisposable
{
    private ITestOutputHelper _testOutputHelper;

    public TestOutputConsole(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
    }

    public IStandardStreamWriter Out => new StandardStreamWriter(_testOutputHelper);
    public bool IsOutputRedirected => true;
    public IStandardStreamWriter Error => new StandardStreamWriter(_testOutputHelper);
    public bool IsErrorRedirected => true;
    public bool IsInputRedirected => true;
    public void Dispose()
    {
        _testOutputHelper = null!;
    }

    private class StandardStreamWriter : IStandardStreamWriter
    {
        private readonly ITestOutputHelper? _testOutputHelper;

        public StandardStreamWriter(ITestOutputHelper? testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void Write(string value)
        {
            _testOutputHelper?.WriteLine(value);
        }
    }
}
