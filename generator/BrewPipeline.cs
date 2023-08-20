namespace generator;

public class BrewPipeline : Pipeline
{

    public BrewPipeline()
    {
        InputModules = new ModuleList
        {
            new ReadFiles("../data/tap/**/*.json")
        };

        ProcessModules = new ModuleList {
            new FormulaeDocModule()
        };

        OutputModules = new ModuleList{
            new WriteFiles()
        };
    }
}
