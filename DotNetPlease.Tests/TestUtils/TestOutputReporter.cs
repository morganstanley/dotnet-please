using DotNetPlease.Services.Reporting.Abstractions;
using DotNetPlease.Utilities;
using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace DotNetPlease.TestUtils
{
    public class TestOutputReporter : IReporter, IDisposable
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

        public List<MessageItem> Messages { get; } = new List<MessageItem>();

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