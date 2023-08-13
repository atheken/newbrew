namespace generator;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Statiq.Common;


/// <summary>
/// Reads the content of files from the file system into the content of new documents.
/// </summary>
/// <remarks>
/// This module will be executed once and input documents will be ignored if search patterns are specified. Otherwise, if a delegate
/// is specified, the module will be executed once per input document and the resulting output documents will be
/// aggregated. In either case, the input documents will not be returned as output of this module.
/// <see cref="IDocument.Source"/> will be set to the absolute path of the file
/// (use <see cref="NormalizedPath.GetRelativeInputPath()"/> to get a source path relative to the input folders).
/// <see cref="IDocument.Destination"/> will be set to the relative path of the file (so that <see cref="WriteFiles"/> will write it
/// to the same relative path in the output folder).
/// </remarks>
/// <category name="Input/Output" />
public class LazyReadFilesModule : ParallelConfigModule<IEnumerable<string>>
{
    private static IDictionary<NormalizedPath, StringContent> cachedContent = new ConcurrentDictionary<NormalizedPath, StringContent>();

    /// <summary>
    /// Reads all files that match the specified globbing patterns and/or absolute paths.
    /// </summary>
    /// <param name="patterns">
    /// The globbing patterns and/or absolute paths to read.
    /// If a null or empty array is specified, all files will be read.
    /// If the array contains a single pattern as null or an empty string pattern, no files will be read.
    /// </param>
    public LazyReadFilesModule(params string[] patterns)
        : base(patterns, false)
    {
    }

    protected override async Task<IEnumerable<IDocument>> ExecuteConfigAsync(IDocument input, IExecutionContext context, IEnumerable<string> value)
    {
        var files = context.FileSystem.GetInputFiles(value);
        return (await Task.WhenAll(files.AsParallel()
            .Select(async (file) =>
            {
                var key = file.Path;
                if (!cachedContent.TryGetValue(key, out var content))
                {
                    content = new StringContent(await file.ReadAllTextAsync());
                    cachedContent[key] = content;
                }

                return context.CloneOrCreateDocument(input, file.Path, file.Path.GetRelativeInputPath(), content);
            })))
            .OrderBy(x => x.Source);
    }
}
