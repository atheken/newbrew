namespace generator;

record Settings(string Source, string Dest, bool Serve = false);

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await Bootstrapper
          .Factory
          .CreateWeb(args)
          .AddInputPath(new NormalizedPath("./input", PathKind.Relative))
          .AddInputPath(new NormalizedPath("../data", PathKind.Relative))
          .SetOutputPath(new NormalizedPath("../site", PathKind.Relative))
          .AddPipeline<BrewPipeline>()
          .RunAsync();
    }
}


