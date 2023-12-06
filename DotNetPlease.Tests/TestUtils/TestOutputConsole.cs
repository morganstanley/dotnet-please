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
