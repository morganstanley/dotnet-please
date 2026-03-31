// Morgan Stanley makes this available to you under the Apache License,
// Version 2.0 (the "License"). You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0.
// 
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership. Unless required by agreed to in writing,
// software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NuGet.Versioning;

namespace DotNetPlease.Services.VersionConsolidation
{
    /// <summary>
    /// Defines the contract for version consolidation strategies when multiple versions of a package exist.
    /// </summary>
    public interface IVersionConsolidationStrategy
    {
        /// <summary>
        /// Determines whether the central package version should be updated to the referenced version.
        /// </summary>
        /// <param name="centralVersion">The current version in the central package file.</param>
        /// <param name="referencedVersion">The version found in a package reference.</param>
        /// <returns>True if the central version should be updated; otherwise, false.</returns>
        bool ShouldUpdate(NuGetVersion centralVersion, NuGetVersion referencedVersion);
    }
}
