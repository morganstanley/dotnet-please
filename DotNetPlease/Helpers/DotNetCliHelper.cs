using System.IO;

namespace DotNetPlease.Helpers
{
    public static class DotNetCliHelper
    {
        public static void CreateSolution(string solutionFileName)
        {
            var outputDirectory = Path.GetDirectoryName(solutionFileName);
            var solutionName = Path.ChangeExtension(Path.GetFileName(solutionFileName), null);
            ProcessHelper.Run("dotnet", $"new sln --name \"{solutionName}\" --output \"{outputDirectory}\"");
        }

        public static void CreateProject(string projectFileName, string projectTemplateName = "classlib")
        {
            var projectName = Path.ChangeExtension(Path.GetFileName(projectFileName), null);
            var projectDirectory = Path.GetDirectoryName(projectFileName);
            ProcessHelper.Run(
                "dotnet",
                $"new {projectTemplateName} --name \"{projectName}\" --output \"{projectDirectory}\"");
        }

        public static void AddProjectToSolution(string projectFileName, string solutionFileName)
        {
            ProcessHelper.Run("dotnet", $"sln \"{solutionFileName}\" add \"{projectFileName}\"");
        }

        public static void AddProjectToSolution(string projectFileName, string solutionFileName, string solutionFolder)
        {
            Directory.CreateDirectory(solutionFolder);
            ProcessHelper.Run(
                "dotnet",
                $"sln \"{solutionFileName}\" add \"{projectFileName}\" --solution-folder \"{solutionFolder}\"");
        }
    }
}