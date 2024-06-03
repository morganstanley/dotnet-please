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

using DotNetPlease.Helpers;
using FluentAssertions.Equivalency;

namespace DotNetPlease.TestUtils
{
    public class PathEquivalencyStep : IEquivalencyStep
    {
        public EquivalencyResult Handle(Comparands comparands, IEquivalencyValidationContext context, IEquivalencyValidator nestedValidator)
        {
            if ((comparands.Subject is null || comparands.Subject is not string)
                   || (comparands.Expectation is null || comparands.Expectation is not string))
            {
                return EquivalencyResult.ContinueWithNext;
            }
            
            return FileSystemHelper.PathComparer.Equals((string)comparands.Subject, (string)comparands.Expectation) ? EquivalencyResult.AssertionCompleted : EquivalencyResult.ContinueWithNext;
        }
    }

    public static class EquivalencyAssertionOptionsPathExtensions
    {
        public static EquivalencyAssertionOptions<string> CompareAsFileName(this EquivalencyAssertionOptions<string> options)
        {
            return options.Using(new PathEquivalencyStep());
        }
    }
}