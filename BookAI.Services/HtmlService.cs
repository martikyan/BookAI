using BookAI.Services.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class HtmlService(ILogger<HtmlService> logger)
{
    static HtmlService()
    {
        HtmlNode.ElementsFlags["br"] = HtmlElementFlag.Empty;
        HtmlNode.ElementsFlags["link"] = HtmlElementFlag.Closed;
    }

    public string AddReference(string html, string sentence, string sequence)
    {
        logger.LogDebug("Adding explanation to the HTML");

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        htmlDocument.OptionWriteEmptyNodes = true;

        HtmlNode? node = null;
        try
        {
            node = htmlDocument.DocumentNode.SelectNodes($"//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), translate('{sentence}', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'))]")?.LastOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Failed to find node using XPath");
        }

        if (node == null)
        {
            node = htmlDocument.DocumentNode;
        }

        var nodeToInsert = HtmlNode.CreateNode($"<a id=\"{sequence}\" href=\"{EpubService.EndnotesBookFileName}#{sequence}\">[{sequence}]</a>");

        var matchinBlocks = FuzzySharp.Levenshtein.GetMatchingBlocks(node.InnerHtml, sentence);
        var beginning = matchinBlocks.First().SourcePos;
        node.InnerHtml = $"{node.InnerHtml[..(beginning + sentence.Length)]}{nodeToInsert.OuterHtml}{node.InnerHtml[(beginning + sentence.Length)..]}";

        return htmlDocument.DocumentNode.OuterHtml;
    }

    public IEnumerable<Paragraph> GetPlainText(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        htmlDocument.OptionWriteEmptyNodes = true;

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

    public string AddEndnote(string endnote, string endnotesChapterTextContent, string sequence, string referenceFileName)
    {
        logger.LogDebug("Adding endnote to the chapter HTML");

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(endnotesChapterTextContent);
        htmlDocument.OptionWriteEmptyNodes = true;

        var endnotes = htmlDocument.GetElementbyId("endnotes");
        endnotes.ChildNodes.Add(HtmlNode.CreateNode($"<p id=\"{sequence}\"><a href=\"{referenceFileName}#{sequence}\">[{sequence}]</a> {endnote}</p>"));
        return htmlDocument.DocumentNode.OuterHtml;
    }

    public static string GetEmptyEndnotesContent()
    {
        return """
               <!DOCTYPE html>
               <html xmlns="http://www.w3.org/1999/xhtml">
               <head>
                   <meta charset="UTF-8">
                   <title>Generated Endnotes</title>
               </head>
               <body id="endnotes">
                   <h1>Generated Endnotes</h1>
               </body>
               </html>
               """;
    }
}