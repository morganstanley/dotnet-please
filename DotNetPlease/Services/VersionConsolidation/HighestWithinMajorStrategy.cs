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
    /// Strategy that updates to a higher version only if it's within the same major version.
    /// This ensures that major version changes are never introduced through consolidation,
    /// helping maintain API compatibility.
    /// </summary>
    public class HighestWithinMajorStrategy : IVersionConsolidationStrategy
    {
        public bool ShouldUpdate(NuGetVersion centralVersion, NuGetVersion referencedVersion)
        {
            return referencedVersion > centralVersion && referencedVersion.Major == centralVersion.Major;
        }
    }
}
