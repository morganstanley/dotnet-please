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
using DotNetPlease.Services.Reporting.Abstractions;
using DotNetPlease.Utilities;
using Xunit.Abstractions;

namespace DotNetPlease.TestUtils
{
    public sealed class TestOutputReporter : IReporter, IDisposable
    {
        private ITestOutputHelper _testOutputHelper;

        public TestOutputReporter(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        public IDisposable BeginScope(string scope)
        {
            return new NullDisposable();
        }

        public void Message(string message, MessageType type = MessageType.Information)
        {
            lock (Messages)
            {
                Messages.Add(new MessageItem(message, type));
            }
            _testOutputHelper?.WriteLine(message);
        }

        public void Dispose()
        {
            _testOutputHelper = null!;
        }

        public List<MessageItem> Messages { get; } = new();

        public readonly struct MessageItem
        {
            public MessageItem(string message, MessageType type)
            {
                Message = message;
                Type = type;
            }

            public string Message { get; }
            public MessageType Type { get; }
        }
    }
}