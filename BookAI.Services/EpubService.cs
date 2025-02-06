using System.Net.Mime;
using System.Reflection;
using System.Text;
using BookAI.Services.Models;
using EpubCore;
using EpubCore.Format;
using HtmlAgilityPack;

namespace BookAI.Services;

public class EpubService(HtmlService htmlService, AIService aiService, EndnoteSequence endnoteSequence)
{
    public const string EndnotesBookFileName = "endnotes.htm";
    public readonly string RelativePath = $"OEBPS/{EndnotesBookFileName}";
    public readonly string AbsolutePath = $"/OEBPS/{EndnotesBookFileName}";

    public async Task<string> SimplifyAsync(Stream epubStream, CancellationToken cancellationToken)
    {
        var book = GetBook(epubStream);
        var chunks = GetTextChunk(book);
        foreach (var chunk in chunks)
        {
            await ProcessTextChunk(book, chunk, cancellationToken);
        }

        return null; // todo: return the updated Epub file stream
    }

    private async Task ProcessTextChunk(EpubBook book, Chunk chunk, CancellationToken cancellationToken)
    {
        var evalResult = await aiService.EvaluateStraightforwardnessAsync(chunk, cancellationToken);

        if (evalResult != null)
        {
            foreach (var res in evalResult.SentenceRatings)
            {
                if (res.Straightforwardness < 10 && !string.IsNullOrEmpty(res.Explanation))
                {
                    var explanation = await aiService.ExplainAsync(res.Sentence, chunk, cancellationToken);

                    var endnotesChapter = GetEndnotesChapter(book);
                    var seq = endnoteSequence.GetNext();

                    chunk.EpubTextFile.TextContent = htmlService.AddExplanation(chunk.EpubTextFile.TextContent, res.Sentence, explanation, seq);
                    endnotesChapter.TextContent = htmlService.AddEndnote(explanation.Explanation, endnotesChapter.TextContent, seq);
                }
            }
        }
    }

    public EpubTextFile GetEndnotesChapter(EpubBook epubBook)
    {
        var lastHtml = epubBook.Resources.Html.Last();

        if (lastHtml.FileName == "bookai_endnotes.html")
        {
            return lastHtml;
        }

        var newFile = new EpubTextFile
        {
            FileName = EndnotesBookFileName,
            ContentType = EpubContentType.Xhtml11,
            TextContent = htmlService.GetEmptyEndnotesContent(),
            Href = RelativePath, AbsolutePath = AbsolutePath, FullFilePath = RelativePath, 
        };

        epubBook.Resources.Html.Add(newFile);
        epubBook.TableOfContents.Add(new EpubChapter()
        {
            Title = "Endnotes",
            Previous = epubBook.TableOfContents.Last(),
            AbsolutePath = AbsolutePath,
            RelativePath = RelativePath,
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

    public IEnumerable<Chunk> GetTextChunk(EpubBook book)
    {
        Paragraph? previous = null;
        foreach (var resource in book.Resources.Html)
        {
            var paragraphs = htmlService.GetPlainText(resource.TextContent);

            var chunk = new StringBuilder();

            foreach (var paragraph in paragraphs)
            {
                if (chunk.Length + paragraph.Text.Length < 4000)
                {
                    chunk.Append(paragraph.Text);
                    chunk.Append('\n');
                }
                else
                {
                    yield return new Chunk
                    {
                        Text = chunk.ToString(),
                        Context = previous?.Text,
                        EpubTextFile = resource,
                    };

                    previous = paragraph;
                    chunk = new StringBuilder(); // empty current chunk reference
                    chunk.Append(paragraph.Text);
                    chunk.Append('\n');
                }
            }
        }
    }
}