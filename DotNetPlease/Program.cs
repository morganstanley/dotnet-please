using System;
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
    }
}