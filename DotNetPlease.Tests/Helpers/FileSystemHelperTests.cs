using FluentAssertions;
using Xunit;

namespace DotNetPlease.Helpers
{
    using static FileSystemHelper;

    public class FileSystemHelperTests
    {
        [Fact]
        public void IsSamePath_ignores_casing()
        {
            IsSamePath("foo/bar", "Foo/BAR").Should().BeTrue();
        }

        [Fact]
        public void IsSamePath_ignores_differences_in_path_separator()
        {
            IsSamePath("foo/bar", "foo\\bar").Should().BeTrue();
        }

        [Fact]
        public void IsSamePath_ignores_differences_in_trailing_path_separator()
        {
            IsSamePath("foo/bar", "foo/bar/").Should().BeTrue();
            IsSamePath("foo/bar", "foo/bar\\").Should().BeTrue();
        }

        [Fact]
        public void NormalizePath_replaces_backslash_with_forward_slash()
        {
            NormalizePath("foo\\bar/baz").Should().Be("foo/bar/baz");
        }

        [Fact]
        public void NormalizePath_removes_trailing_path_separator()
        {
            NormalizePath("foo/bar/").Should().Be("foo/bar");
            NormalizePath("foo\\bar\\").Should().Be("foo/bar");
        }
    }
}