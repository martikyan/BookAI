using System.Reflection;
using System.Text;
using BookAI.Services.Models;
using EpubCore;
using EpubCore.Format;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class EpubService(HtmlService htmlService, AIService aiService, EndnoteSequence endnoteSequence, ILogger<EpubService> logger)
{
    public const string EndnotesBookFileName = "endnotes.htm";
    public readonly string RelativePath = $"OEBPS/{EndnotesBookFileName}";
    public readonly string AbsolutePath = $"/OEBPS/{EndnotesBookFileName}";

    public async Task<EpubBook> ProcessBookAsync(Stream epubStream, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing EPUB book");

        var book = GetBook(epubStream);
        
        logger.LogInformation("Parsed book {Title}", book.Title);

        var chunks = GetTextChunk(book);
        foreach (var (index, chunk) in chunks.Index())
        {
            logger.LogInformation("Progress: {Progress:F0}%", 100.0 * index / chunks.Count);

            try
            {
                await ProcessTextChunk(book, chunk, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to process text chunk");
            }
        }
        
        logger.LogInformation("Finished processing EPUB book");

        return book;
    }

    private async Task ProcessTextChunk(EpubBook book, Chunk chunk, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing text chunk");

        var evalResult = await aiService.EvaluateConfusionAsync(chunk, cancellationToken);

        foreach (var res in evalResult.TextConfusionScores)
        {
            if (res.ConfusionScore > 5) // todo: make 5 configurable
            {
                var explanation = await aiService.ExplainAsync(res.Text, chunk, cancellationToken);

                var endnotesChapter = GetEndnotesChapter(book);
                var seq = endnoteSequence.GetNext();

                chunk.EpubTextFile.TextContent = htmlService.AddReference(chunk.EpubTextFile.TextContent, res.Text, seq);
                endnotesChapter.TextContent = htmlService.AddEndnote(explanation.Explanation, endnotesChapter.TextContent, seq);
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
            Href = RelativePath,
            AbsolutePath = AbsolutePath,
            FullFilePath = RelativePath
        };

        epubBook.Resources.Html.Add(newFile);
        epubBook.TableOfContents.Add(new EpubChapter
        {
            Title = "Endnotes",
            Previous = epubBook.TableOfContents.Last(),
            AbsolutePath = AbsolutePath,
            RelativePath = RelativePath
        });
        epubBook.TableOfContents.SkipLast(1).Last().Next = epubBook.TableOfContents.Last();

        var manifestItem = new OpfManifestItem();
        var spineRef = new OpfSpineItemRef();
        var navPoint = new NcxNavPoint();

        SetInternalProperty(spineRef, "IdRef", EndnotesBookFileName);
        SetInternalProperty(spineRef, "Linear", true);
        SetInternalProperty(manifestItem, "Id", EndnotesBookFileName);
        SetInternalProperty(manifestItem, "Href", RelativePath);
        SetInternalProperty(manifestItem, "MediaType", "application/xhtml+xml");

        navPoint.NavLabelText = "Endnotes";
        navPoint.Class = "chapter";
        navPoint.ContentSrc = RelativePath;
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

    public EpubBook GetBook(Stream inputStream)
    {
        return EpubReader.Read(inputStream, false);
    }

    public IList<Chunk> GetTextChunk(EpubBook book)
    {
        var result = new List<Chunk>();
        Paragraph? previous = null;

        foreach (var resource in book.Resources.Html)
        {
            var paragraphs = htmlService.GetPlainText(resource.TextContent);

            var chunk = new StringBuilder();

            foreach (var paragraph in paragraphs)
                if (chunk.Length + paragraph.Text.Length < 4000) // todo: make this a config
                {
                    chunk.Append(paragraph.Text);
                    chunk.Append('\n');
                }
                else
                {
                    result.Add(new Chunk
                    {
                        Text = chunk.ToString(),
                        Context = previous?.Text,
                        EpubTextFile = resource
                    });

                    previous = paragraph;
                    chunk = new StringBuilder();
                    chunk.AppendLine(paragraph.Text);
                }
        }

        return result;
    }
}