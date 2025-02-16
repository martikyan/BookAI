using BookAI.Services.Abstraction;
using BookAI.Services.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class HtmlService(ILogger<HtmlService> logger) : IHtmlService
{
    static HtmlService()
    {
        HtmlNode.ElementsFlags["br"] = HtmlElementFlag.Empty;
        HtmlNode.ElementsFlags["link"] = HtmlElementFlag.Closed;
    }

    public string? AddReference(string html, string sentence, string sequence)
    {
        logger.LogInformation("Placing explanation of '{Sentence}' to HTML", sentence);

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

        var nodeToInsert = HtmlNode.CreateNode($"<a id=\"{sequence}\" href=\"{EpubService.EndnotesBookFileName}#{sequence}\" style=\"font-size: 0.8em; vertical-align: super;\"><span>[{sequence}</span><span style=\"font-size: 0.8em; vertical-align: super;\">AI]</span>");

        for (var wordsToPreserve = 10; wordsToPreserve >= 1; wordsToPreserve--)
        {
            sentence = Trim(sentence, wordsToPreserve);
            if (node.InnerHtml.Contains(sentence, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        if (!node.InnerHtml.Contains(sentence, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Couldn't find sentence {Sentence} in the HTML", sentence);
            return null;
        }
        
        var insertionIndex = node.InnerHtml.IndexOf(sentence, StringComparison.OrdinalIgnoreCase) + sentence.Length;

        const int maxShift = 4;
        var minHtml = $"{node.InnerHtml[..(insertionIndex)]}{nodeToInsert.OuterHtml}{node.InnerHtml[(insertionIndex)..]}";
        var minErrors = GetHtmlErrorsCount(minHtml);
        for (var i = -maxShift / 2; i < maxShift / 2; i++)
        {
            try
            {
                var currentHtml = $"{node.InnerHtml[..(insertionIndex + i)]}{nodeToInsert.OuterHtml}{node.InnerHtml[(insertionIndex + i)..]}";
                var currentErrors = GetHtmlErrorsCount(currentHtml);
                if (currentErrors < minErrors)
                {
                    logger.LogInformation("Found HTML position with less errors {PrevMin} vs {Min}", minErrors, currentErrors);
                    minHtml = currentHtml;
                    minErrors = currentErrors;
                }
                else if (currentErrors == minErrors && i <= 0) // prefer index closer to 0 if min errors are the same
                {
                    minHtml = currentHtml;
                    minErrors = currentErrors;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Couldn't place the reference");
            }
        }

        node.InnerHtml = minHtml;
        logger.LogInformation("Placed sentence with resulting HTML {HTML}", minHtml);
        return htmlDocument.DocumentNode.OuterHtml;
    }

    private string Trim(string sentence, int wordsToPreserve)
    {
        sentence = sentence.Trim();
        if (sentence.Length < 20)
        {
            return sentence;
        }

        var currentSpaces = 0;
        for (var i = 1; i < sentence.Length; i++)
        {
            if (char.IsWhiteSpace(sentence[sentence.Length - i]))
            {
                currentSpaces++;
            }

            if (currentSpaces >= wordsToPreserve)
            {
                return sentence.Substring(sentence.Length - i).Trim();
            }
        }

        return sentence;
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
        endnotes.ChildNodes.Add(HtmlNode.CreateNode(
            $"<p id=\"{sequence}\"><a href=\"{referenceFileName}#{sequence}\" style=\"font-size: 0.8em; vertical-align: super;\"><span>[{sequence}</span><span style=\"font-size: 0.8em; vertical-align: super;\">AI]</span></a> {endnote}</p>"
        ));
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

    public static int GetHtmlErrorsCount(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        var parseErrors = htmlDocument.ParseErrors;
        return parseErrors.Count();
    }
}