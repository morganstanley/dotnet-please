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
    /// Strategy that prevents downgrading the major version of packages.
    /// It will update to a higher version or a different minor/patch version within the same major,
    /// but will skip updating if the referenced version has a lower major version number.
    /// This is useful for enforcing minimum major version requirements.
    /// </summary>
    public class NoDowngradeMajorStrategy : IVersionConsolidationStrategy
    {
        public bool ShouldUpdate(NuGetVersion centralVersion, NuGetVersion referencedVersion)
        {
            // Skip update if major version is lower
            if (referencedVersion.Major < centralVersion.Major)
                return false;

            // Update if newer version or same major but higher overall version
            return referencedVersion > centralVersion;
        }
    }
}
