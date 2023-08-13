namespace generator;

using System.Collections.Concurrent;
using System.IO;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Xml;

public class FormulaeDocModule : IModule
{
    private static IDictionary<NormalizedPath, Formula> readFormula = new ConcurrentDictionary<NormalizedPath, Formula>();
    private static IDictionary<(int, Formula[]), Document> output =
        new ConcurrentDictionary<(int, Formula[]), Document>();

    public async Task<IEnumerable<IDocument>> ExecuteAsync(IExecutionContext context)
    {
        var json = (await context.Inputs.ParallelSelectAsync(async (i) =>
        {
            var path = i.Source;
            if (!readFormula.TryGetValue(path, out var retval))
            {
                try
                {
                    retval = JsonSerializer.Deserialize<Formula>(await i.GetContentStringAsync());
                    readFormula[path] = retval!;
                }
                catch (Exception)
                {
                    Console.WriteLine($"The file `{i}` could not be parsed.");
                    return null;
                }
            }
            return retval;
        })).Where(f => f != null).Select(k => k!).ToList();

        // We want to ensure that the last feed file always has at <pageSize> items, this is so that new subscribers see.. something.
        // to do this, we order descending, and then _decrement_ to calculate the the page in which a given item falls.
        var pageSize = 50;
        var items = json.Count();
        var docs = json.OrderByDescending(k => k?.date_added)
            .GroupBy(k => (int)Math.Floor(items-- / (float)pageSize)).ToArray();

        var results = docs.AsParallel().Select(k =>
        {
            var key = (k.Key, k.ToArray());
            if (!output.TryGetValue(key, out var doc))
            {
                var isLatest = k.Key + 1 == docs.Length;
                var page = isLatest ? "atom" : k.Key.ToString();
                var pageData = new PageData<Formula>(docs.Length, k.Key, isLatest, k.ToArray());
                Statiq.Common.StringContent s = null;
                doc = new Document($"{k.First()!.tap}/{page}.xml",
                s ??= new Statiq.Common.StringContent(RenderPage(pageData)));
                output[key] = doc;
            }
            return doc;
        }).ToArray();

        return results;
    }

    private string RenderPage(PageData<Formula> pageData)
    {
        var ms = new MemoryStream();
        var xm = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Indent = true
        });

        var feed = new SyndicationFeed
        {
            Title = new TextSyndicationContent($"New Brew for `{pageData.items.First().tap}`"),
            Description = new TextSyndicationContent($"New formulae added to the homebrew tap: `{pageData.items.First().tap}`."),
            Items = pageData.items.Select(k =>
            {
                var link = new SyndicationLink(new Uri(k.homepage));
                var item = new SyndicationItem()
                {
                    Title = new TextSyndicationContent(k.name),
                    Id = $"{k.tap}/{k.name}",
                    LastUpdatedTime = k.date_added,
                    Summary = new TextSyndicationContent(k.desc)
                };
                item.Links.Add(link);
                return item;
            }),
            LastUpdatedTime = pageData.items.Max(k => k.date_added),
            TimeToLive = TimeSpan.FromMinutes(15)
        };

        var links = new List<SyndicationLink>();
        var page = pageData.isLatest ? "atom" : pageData.current.ToString();

        links.Add(new SyndicationLink(new Uri($"./{page}.xml", UriKind.Relative)) { RelationshipType = "self" });

        if (pageData.current != 0)
        {
            links.Add(new SyndicationLink(new Uri($"./{pageData.current - 1}.xml", UriKind.Relative)) { RelationshipType = "prev" });
        }
        if (!pageData.isLatest)
        {
            var next = pageData.current + 1 == pageData.totalPages ? "./atom.xml" : $"./{pageData.current + 1}.xml";
            links.Add(new SyndicationLink(new Uri(next, UriKind.Relative)) { RelationshipType = "next" });
        }

        feed.Links.AddRange(links);

        feed.SaveAsAtom10(xm);
        xm.Flush();

        return Encoding.UTF8.GetString(ms.GetBuffer());
    }
}