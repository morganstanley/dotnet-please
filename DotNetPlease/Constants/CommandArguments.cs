namespace DotNetPlease.Constants
{
    public static class CommandArguments
    {
        public static class Projects
        {
            public const string Description =
                "An optional globbing pattern to search for projects (defaults to all projects, recursively).";
        }

        public static class ProjectsOrSolution
        {
            public const string Description =
                "An optional globbing pattern to search for projects or a solution (defaults to the solution in the working directory, or all projects recursively, if there's no single solution)";
        }

        public static class SolutionFileName
        {
            public const string Description =
                "The solution file name (defaults to the solution in the working directory, or the working directory itself)";
        }

        public static class RequiredSolutionFileName
        {
            public const string Description = "The solution file name (defaults to the solution in the working directory)";
        }
    }
}