using System.Collections;
using System.Diagnostics;
using System.IO.Pipelines;

namespace generator;

record Settings(string Source, string Dest, bool Serve = false);

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        PreviewTailwind(args);

        return await Bootstrapper
          .Factory
          .CreateWeb(args)
          .AddInputPath(new NormalizedPath("./input", PathKind.Relative))
          .AddInputPath(new NormalizedPath("../data", PathKind.Relative))
          .SetOutputPath(new NormalizedPath("../site", PathKind.Relative))
          .AddPipeline<BrewPipeline>()
          .RunAsync();
    }

    private static void PreviewTailwind(string[] args)
    {
        if (args.Any(k => k == "preview"))
        {
            try
            {
                var proc = new Process();
                proc.StartInfo.ArgumentList.AddRange("-w", "-m", "-o", "./input/public/main.css", "-i",
                "./input/public/app.css", "-c", "./tailwind.config.js");

                proc.StartInfo.FileName = "tailwindcss";
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;

                proc.Start();

                AppDomain.CurrentDomain.ProcessExit += (o, e) =>
                {
                    Console.WriteLine($"tailwind exit code {proc.ExitCode}");
                    proc.Kill();
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tailwindcss didn't launch: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }
    }
}


