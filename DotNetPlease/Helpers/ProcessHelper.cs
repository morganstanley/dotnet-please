using System.Diagnostics;

namespace DotNetPlease.Helpers
{
    public static class ProcessHelper
    {
        public static string Run(string command, string? arguments)
        {
            var startInfo = new ProcessStartInfo(command, arguments)
            {
                RedirectStandardOutput = true
            };

            var process = Process.Start(startInfo)!;
            process.WaitForExit();

            return process.StandardOutput.ReadToEnd();
        }
    }
}