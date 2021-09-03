/*
 * Morgan Stanley makes this available to you under the Apache License,
 * Version 2.0 (the "License"). You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0.
 *
 * See the NOTICE file distributed with this work for additional information
 * regarding copyright ownership. Unless required by applicable law or agreed
 * to in writing, software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
 * or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

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