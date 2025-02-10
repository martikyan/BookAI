using System.Reflection;
using System.Text;
using BookAI.Services.Models;
using EpubCore;
using EpubCore.Format;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class EpubService(HtmlService htmlService, AIService aiService, EndnoteSequence endnoteSequence, ILogger<EpubService> logger)
{
    public const string EndnotesBookFileName = "endnotes.html";
    private readonly Lock _lock = new();
    private readonly int _maxTotalLength = 4000;

    public async Task<EpubBook> ProcessBookAsync(Stream epubStream, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing EPUB book");

        var book = GetBook(epubStream);

        logger.LogInformation("Parsed book {Title}", book.Title);

        var chunks = GetTextChunks(book);
        var processedChunks = 0;

        await Parallel.ForEachAsync(chunks, async (chunk, _) =>
        {
            logger.LogInformation("Progress: {Progress:F0}%", 100.0 * processedChunks / chunks.Count);
if (processedChunks >= 10)
{
    return;
}
            try
            {
                await ProcessTextChunk(book, chunk, cancellationToken);
                Interlocked.Increment(ref processedChunks);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to process text chunk");
            }
        });

        logger.LogInformation("Finished processing EPUB book");

        return book;
    }

    private async Task ProcessTextChunk(EpubBook book, Chunk chunk, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing text chunk");

        var evalResult = await aiService.EvaluateConfusionAsync(chunk, cancellationToken);

        foreach (var res in evalResult.TextConfusionScores)
        {
            if (res.ConfusionScore > 6) // todo: make 5 configurable
            {
                var explanation = await aiService.ExplainAsync(res.Text, chunk, cancellationToken);

                lock (_lock)
                {
                    var seq = endnoteSequence.GetNext();
                    var endnotesChapter = GetEndnotesChapter(book);

                    chunk.EpubTextFile.TextContent = htmlService.AddReference(chunk.EpubTextFile.TextContent, res.Text, seq);
                    endnotesChapter.TextContent = htmlService.AddEndnote(explanation.Explanation, endnotesChapter.TextContent, seq);
                }
            }
        }
    }

    public EpubTextFile GetEndnotesChapter(EpubBook epubBook)
    {
        var lastHtml = epubBook.Resources.Html.Last();

        if (lastHtml.FileName == EndnotesBookFileName)
        {
            logger.LogDebug("Endnotes chapter found");
            return lastHtml;
        }

        logger.LogDebug("Creating a new endnotes chapter");
        var newFile = new EpubTextFile
        {
            FileName = EndnotesBookFileName,
            ContentType = EpubContentType.Xhtml11,
            TextContent = HtmlService.GetEmptyEndnotesContent(),
            Href = GetRelativePath(epubBook, EndnotesBookFileName),
            AbsolutePath = GetFilePath(epubBook, EndnotesBookFileName),
            FullFilePath = GetRelativePath(epubBook, EndnotesBookFileName)
        };

        epubBook.Resources.Html.Add(newFile);
        epubBook.TableOfContents.Add(new EpubChapter
        {
            Title = "Endnotes",
            Previous = epubBook.TableOfContents.Last(),
            AbsolutePath = GetFilePath(epubBook, EndnotesBookFileName),
            RelativePath = GetRelativePath(epubBook, EndnotesBookFileName)
        });
        epubBook.TableOfContents.SkipLast(1).Last().Next = epubBook.TableOfContents.Last();

        var manifestItem = new OpfManifestItem();
        var spineRef = new OpfSpineItemRef();
        var navPoint = new NcxNavPoint();

        SetInternalProperty(spineRef, "IdRef", EndnotesBookFileName);
        SetInternalProperty(spineRef, "Linear", true);
        SetInternalProperty(manifestItem, "Id", EndnotesBookFileName);
        SetInternalProperty(manifestItem, "Href", GetRelativePath(epubBook, EndnotesBookFileName));
        SetInternalProperty(manifestItem, "MediaType", "application/xhtml+xml");

        navPoint.NavLabelText = "Endnotes";
        navPoint.Class = "chapter";
        navPoint.ContentSrc = GetRelativePath(epubBook, EndnotesBookFileName);
        navPoint.Id = EndnotesBookFileName;
        navPoint.PlayOrder = epubBook.Format.Ncx.NavMap.NavPoints.Select(p => p.PlayOrder).Max() + 1;

        epubBook.Format.Opf.Manifest.Items.Add(manifestItem);
        epubBook.Format.Opf.Spine.ItemRefs.Add(spineRef);
        epubBook.Format.Ncx.NavMap.NavPoints.Add(navPoint);

        return newFile;
    }

    private void SetInternalProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }

    private EpubBook GetBook(Stream inputStream)
    {
        return EpubReader.Read(inputStream, false);
    }

    private IList<Chunk> GetTextChunks(EpubBook book)
    {
        var result = new List<Chunk>();
        // The rotating queue holds previous paragraphs (as plain text) that form the context.
        var contextQueue = new Queue<string>();

        // Loop over each HTML resource in the EPUB.
        foreach (var resource in book.Resources.Html)
        {
            // Get the paragraphs from the resource.
            var paragraphs = htmlService.GetPlainText(resource.TextContent);

            // Use a list to accumulate paragraphs for the current chunk.
            var currentChunkParagraphs = new List<string>();

            foreach (var paragraph in paragraphs)
            {
                string paraText = paragraph.Text;
                // Calculate lengths:
                int contextLength = GetTotalLength(contextQueue);
                int currentChunkLength = currentChunkParagraphs.Sum(s => s.Length + 1); // +1 for newline
                int newLength = contextLength + currentChunkLength + paraText.Length + 1;

                // If adding this paragraph keeps us within the limit, add it.
                if (newLength < _maxTotalLength)
                {
                    currentChunkParagraphs.Add(paraText);
                }
                else
                {
                    // Finalize the current chunk if there is any content.
                    if (currentChunkParagraphs.Any())
                    {
                        FinalizeChunk(result, resource, contextQueue, currentChunkParagraphs);
                        // Clear the current chunk list to start fresh.
                        currentChunkParagraphs.Clear();
                    }

                    // Now try adding the paragraph to the new (empty) chunk.
                    // (It might be very long – you could decide to truncate it if needed.)
                    if (paraText.Length + GetTotalLength(contextQueue) < _maxTotalLength)
                    {
                        currentChunkParagraphs.Add(paraText);
                    }
                    else
                    {
                        // If even a single paragraph (plus context) is too long,
                        // you might want to handle it specially.
                        // For now, we add it as its own chunk.
                        currentChunkParagraphs.Add(paraText);
                    }
                }
            }

            // If any paragraphs remain for the current chunk, finalize that chunk.
            if (currentChunkParagraphs.Any())
            {
                FinalizeChunk(result, resource, contextQueue, currentChunkParagraphs);
                currentChunkParagraphs.Clear();
            }
        }

        return result;
    }

    /// <summary>
    /// Finalizes the current chunk: creates a Chunk object with the accumulated main text
    /// and the current context, then updates the context with the new paragraphs.
    /// </summary>
    private void FinalizeChunk(IList<Chunk> result,
        EpubTextFile resource,
        Queue<string> contextQueue,
        List<string> currentChunkParagraphs)
    {
        string chunkText = string.Join("\n", currentChunkParagraphs);
        // For context we join the previous paragraphs (or you could join with commas as in the original code).
        string contextText = string.Join("\n", contextQueue);

        result.Add(new Chunk
        {
            Text = chunkText,
            Context = contextText,
            EpubTextFile = resource
        });

        // Update the context with the paragraphs from this chunk.
        foreach (var para in currentChunkParagraphs)
        {
            AppendToContextQueue(contextQueue, para);
        }
    }

    /// <summary>
    /// Returns the total length of the context text when all paragraphs are joined (with a space).
    /// </summary>
    private int GetTotalLength(Queue<string> contextQueue)
    {
        if (!contextQueue.Any())
            return 0;

        return contextQueue.Sum(s => s.Length);
    }

    /// <summary>
    /// Adds a paragraph to the context queue. If the total context length exceeds the limit,
    /// it dequeues (removes) the oldest paragraphs until the context fits.
    /// </summary>
    private void AppendToContextQueue(Queue<string> contextQueue, string paragraphText)
    {
        contextQueue.Enqueue(paragraphText);
        while (GetTotalLength(contextQueue) > 1000 && contextQueue.Any())
        {
            contextQueue.Dequeue();
        }
    }

    private string GetRelativePath(EpubBook book, string filename)
    {
        return Path.GetRelativePath("/", GetFilePath(book, filename));
    }

    private string GetFilePath(EpubBook book, string filename)
    {
        return Path.Combine(GetFilesPath(book), filename);
    }

    private string GetFilesPath(EpubBook book)
    {
        if (!book.TableOfContents.Any())
        {
            throw new InvalidOperationException("Table of contentx is required to get files path");
        }

        return Path.GetDirectoryName(book.TableOfContents[book.TableOfContents.Count / 2].AbsolutePath)!;
    }
}