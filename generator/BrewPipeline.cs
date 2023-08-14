using Statiq.Common;
using Statiq.Core;
using Statiq.Razor;

namespace generator;

public class BrewPipeline : Pipeline
{

    public BrewPipeline()
    {
        InputModules = new ModuleList
        {
            new LazyReadFilesModule("./tap/**/*.json")
        };

        ProcessModules = new ModuleList {
            new FormulaeDocModule()
        };

        OutputModules = new ModuleList{
            new WriteFiles()
        };
    }
}
