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