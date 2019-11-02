using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.DotnetOrg.DevOps;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var client = new DevOpsClient("terrajobst", "github-policy-checker", "6vvvugnuquk57zphxjfsnozrzxoechujcqivqfdczqyxodnvhb3a");

                var builds = await client.GetBuildsAsync("19", resultFilter: "succeeded", reasonFilter: "schedule,manual");
                var latestBuild = builds.FirstOrDefault();
                if (latestBuild != null)
                {
                    using (var stream = await client.GetArtifactFileAsync(latestBuild.Id, "drop", "dotnet.json"))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            Console.WriteLine(json.Substring(0, 100));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
