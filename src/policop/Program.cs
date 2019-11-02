using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Mono.Options;

namespace Microsoft.DotnetOrg.PolicyCop
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var commands = GetCommands();
            var commandName = args.FirstOrDefault();

            if (commandName == null ||
                commandName == "-?" ||
                commandName == "-h" ||
                commandName == "--help")
            {
                var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                Console.Error.WriteLine($"usage: {exeName} <command> [OPTIONS]+");
                Console.Error.WriteLine();

                var commandNameWidth = commands.Max(c => c.Name.Length) + 3;                
                foreach (var c in commands)
                    Console.Error.WriteLine($"  {c.Name.PadRight(commandNameWidth)}{c.Description}");
                return;
            }

            var command = commands.SingleOrDefault(c => c.Name == commandName);
            if (command == null)
            {
                Console.Error.WriteLine($"error: undefined command '{commandName}'");
                return;
            }

            var help = false;

            var options = new OptionSet();
            command.AddOptions(options);
            options.Add("h|?|help", null, v => help = true, true);
            options.Add(new ResponseFileSource());
            
            try
            {
                var unprocessed = options.Parse(args.Skip(1));

                if (help)
                {
                    var exeName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                    Console.Error.WriteLine(command.Description);
                    Console.Error.WriteLine($"usage: {exeName} {command.Name} [OPTIONS]+");
                    options.WriteOptionDescriptions(Console.Error);
                    return;
                }

                if (unprocessed.Any())
                {
                    foreach (var option in unprocessed)
                        Console.Error.WriteLine($"error: unrecognized argument {option}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return;
            }

            await command.ExecuteAsync();
        }

        private static IReadOnlyList<ToolCommand> GetCommands()
        {
            return typeof(Program).Assembly
                                  .GetTypes()
                                  .Where(t => typeof(ToolCommand).IsAssignableFrom(t) &&
                                              !t.IsAbstract && t.GetConstructor(Array.Empty<Type>()) != null)
                                  .Select(t => (ToolCommand)Activator.CreateInstance(t))
                                  .OrderBy(t => t.Name)
                                  .ToArray();
        }
    }
}
