using BookAI.Services.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class HtmlService(ILogger<HtmlService> logger)
{
    public string AddReference(string html, string sentence, string sequence)
    {
        logger.LogDebug("Adding explanation to the HTML");

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        var node = htmlDocument.DocumentNode.SelectNodes($"//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), translate('{sentence.Trim('\'')}', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'))]")?.LastOrDefault();
        if (node == null)
        {
            logger.LogWarning("Could not find sentence {Sentence} in the HTML {HTML}", sentence, html);
            throw new InvalidOperationException("Failed to find the referenced sentence");
        }

        node.ChildNodes.Add(HtmlNode.CreateNode($"<a href=\"{EpubService.EndnotesBookFileName}#{sequence}\">[{sequence}]</a>"));
        return htmlDocument.DocumentNode.OuterHtml;
    }

    public IEnumerable<Paragraph> GetPlainText(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        // todo: fix the case when paragraphs are not html paragraph tags
        var paragraphNodes = htmlDocument.DocumentNode.SelectNodes("//p");

        if (paragraphNodes == null || paragraphNodes.Count == 0)
        {
            yield break;
        }

        foreach (var paragraphNode in paragraphNodes)
        {
            if (string.IsNullOrWhiteSpace(paragraphNode.InnerText))
            {
                continue;
            }

            yield return new Paragraph { Text = paragraphNode.InnerText };
        }
    }

    public string AddEndnote(string endnote, string endnotesChapterTextContent, string sequence)
    {
        logger.LogDebug("Adding endnote to the chapter HTML");

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(endnotesChapterTextContent);

        var endnotes = htmlDocument.GetElementbyId("endnotes");
        endnotes.ChildNodes.Add(HtmlNode.CreateNode($"<p id=\"{sequence}\">{sequence}: {endnote}</p>")); // todo: make sequence to reference back the original sentence
        return htmlDocument.DocumentNode.OuterHtml;
    }

    public static string GetEmptyEndnotesContent()
    {
        return """
               <!DOCTYPE html>
               <html>
               <head>
               <h1>Generated endnotes.</h1>
               </head>
               <body id="endnotes">
               </body>
               """;
    }
}