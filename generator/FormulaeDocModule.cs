namespace generator;
using System.IO;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Markdig.Renderers;

public class FormulaeDocModule : IModule
{

    public async Task<IEnumerable<IDocument>> ExecuteAsync(IExecutionContext context)
    {
        var jsonByTap = (await context.Inputs.ParallelSelectAsync(async (i) =>
        {
            try
            {
                return JsonSerializer.Deserialize<Formula>(await i.GetContentStringAsync());

            }
            catch (Exception)
            {
                Console.WriteLine($"The file `{i}` could not be parsed.");
                return null;
            }
        })).Where(f => f != null).Select(k => k!).ToLookup(k => k.tap);

        var results = new List<IDocument>();

        foreach (var json in jsonByTap)
        {
            var tap = json.Key;

            // We want to ensure that the last feed file always has at <pageSize> items, this is so that new subscribers see.. something.
            // to do this, we order descending, and then _decrement_ to calculate the the page in which a given item falls.
            var pageSize = 50;

            var items = json.Count();
            var docs = json.OrderByDescending(k => k?.date_added)
                .GroupBy(k => (int)Math.Floor(items-- / (float)pageSize)).ToArray();

            results.AddRange(docs.AsParallel().Select(k =>
            {
                var isLatest = k.Key + 1 == docs.Length;
                var page = isLatest ? "atom" : k.Key.ToString();
                var pageData = new PageData<Formula>(docs.Length, k.Key, isLatest, k.ToArray());
                var doc = new Document($"feeds/{tap}/{page}.xml", new Statiq.Common.StringContent(RenderPage(pageData)));

                return doc;
            }));

            // We want to randomize the
            // If the year is divisible by 4, it's a leap year (at least until 2100, 
            // so I'm willing to accept this as a good approximation of days in the year)
            var days = DateTime.UtcNow.Year % 4 == 0 ? 366 : 365;
            var selector = DateTime.UtcNow.DayOfYear;
            var count = 0;

            // we are selecting documents "randomly", but we want them to be semi-deterministic by day/run.
            var randomDocs = json.OrderBy(k => k.name)
                .Where(k => !string.IsNullOrWhiteSpace(k.readme) && count++ % days == selector)
                //randomize so that we churn through items over a few runs during the day.
                .OrderBy(k => Guid.NewGuid()).Take(5);
            var pageData = new PageData<Formula>(1, 0, true, randomDocs);
            var doc = new Document($"feeds/{tap}/random.xml",
                new Statiq.Common.StringContent(RenderPage(pageData, $"Random brews for `{tap}`")));
            results.Add(doc);

        }
        return results;
    }

    private string RenderPage(PageData<Formula> pageData, string? title = null)
    {
        var ms = new MemoryStream();
        var xm = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Indent = true
        });

        title ??= $"New Brew for `{pageData.items.First().tap}`";

        var feed = new SyndicationFeed
        {
            Title = new TextSyndicationContent(title),
            Description = new TextSyndicationContent($"New formulae added to the homebrew tap: `{pageData.items.First().tap}`."),
            Items = pageData.items.Select(k =>
            {
                var link = new SyndicationLink(new Uri(k.homepage));
                var item = new SyndicationItem()
                {
                    Title = new TextSyndicationContent(k.name),
                    Id = $"{k.tap}/{k.name}",
                    LastUpdatedTime = k.date_added,
                    Summary = GetReadmeOrDescriptionContent(k)
                };
                item.Links.Add(link);
                return item;
            }),
            LastUpdatedTime = DateTime.UtcNow,
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

    private static readonly Regex markdownLinkMatcher = new Regex(@"(?<prefix>\[[^\]]+]\(\s*(?!([a-z]+://)|#))([.]/)?(?<path>[^)]+)(?<suffix>\))", RegexOptions.ExplicitCapture);

    private TextSyndicationContent GetReadmeOrDescriptionContent(Formula k)
    {
        if (string.IsNullOrWhiteSpace(k.readme))
        {
            return new TextSyndicationContent(k.desc);
        }
        else
        {
            var content = markdownLinkMatcher.Replace(k.readme.Trim(), "${prefix}" + k.homepage + "${path}${suffix}");
            using var tw = new StringWriter();
            Markdig.Markdown.Convert(content, new HtmlRenderer(tw));
            tw.Flush();
            return new TextSyndicationContent(tw.GetStringBuilder().ToString(), TextSyndicationContentKind.Html);
        }
    }
}