using FluentAssertions;
using Xunit;

namespace DotNetPlease.Helpers
{
    using static FileSystemHelper;

    public class FileSystemHelperTests
    {
        [Theory]
        [InlineData("foo/bar", "Foo/BAR")]
        public void IsSamePath_ignores_casing(string path1, string path2)
        {
            IsSamePath(path1, path2).Should().BeTrue();
        }

        [Theory]
        [InlineData("foo/bar", "foo\\bar")]
        public void IsSamePath_ignores_differences_in_path_separator(string path1, string path2)
        {
            IsSamePath(path1, path2).Should().BeTrue();
        }

        [Theory]
        [InlineData("foo/bar", "foo/bar/")]
        [InlineData("foo/bar", "foo/bar\\")]
        public void IsSamePath_ignores_differences_in_trailing_path_separator(string path1, string path2)
        {
            IsSamePath(path1, path2).Should().BeTrue();
        }

        [Theory]
        [InlineData("foo/bar/..", "foo")]
        [InlineData("foo/bar/./baz", "foo/bar/baz")]
        [InlineData("./foo/bar", "foo/bar")]
        public void IsSamePath_ignores_redundant_relative_jumps(string path1, string path2)
        {
            IsSamePath(path1, path2).Should().BeTrue();
        }

        [Theory]
        [InlineData("foo\\bar/baz", "foo/bar/baz")]
        public void NormalizePath_replaces_backslash_with_forward_slash(string oldPath, string newPath)
        {
            NormalizePath(oldPath).Should().Be(newPath);
        }

        [Theory]
        [InlineData("foo/bar/", "foo/bar")]
        [InlineData("foo\\bar\\", "foo/bar")]
        public void NormalizePath_removes_trailing_path_separator(string oldPath, string newPath)
        {
            NormalizePath(oldPath).Should().Be(newPath);
        }
    }
}