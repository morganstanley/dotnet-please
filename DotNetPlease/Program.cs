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

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotNetPlease
{
    class Program
    {
        static Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("dotnet-please from Morgan Stanley");
            Console.WriteLine(ReadVersion() ?? "<unknown version>");
            Console.WriteLine("Visit us at https://github.com/morganstanley");
            Console.WriteLine();
            try
            {
                var app = new App();
                return app.ExecuteAsync(app.PreprocessArguments(args));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return Task.FromResult(e.HResult);
            }
        }

        static string? ReadVersion()
        {
            return Assembly.GetEntryAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "PackageVersion")
                ?.Value;
        }
    }
}