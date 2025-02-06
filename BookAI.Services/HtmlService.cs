using BookAI.Services.Models;
using HtmlAgilityPack;

namespace BookAI.Services;

public class HtmlService
{
    public string AddExplanation(string html, string resSentence, ExplanationResponse explanation, string sequence)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        var node = htmlDocument.DocumentNode.SelectNodes($"//*[contains(., '{resSentence}')]").LastOrDefault();

        if (node == null)
        {
            return null;
        }

        // <a href="ch2.xhtml#id53" class="a">[1]</a>
        node.ChildNodes.Add(HtmlNode.CreateNode($"<a href=\"{EpubService.EndnotesBookFileName}#{sequence}\">[{sequence}]</a>"));
        return htmlDocument.DocumentNode.OuterHtml;
    }

    public IEnumerable<Paragraph> GetPlainText(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            throw new ArgumentException("HTML content cannot be null or empty", nameof(html));
        }

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

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

            yield return new Paragraph { Text = paragraphNode.InnerText, HtmlNode = paragraphNode };
        }
    }

    public string AddEndnote(string endnote, string endnotesChapterTextContent, string sequence)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(endnotesChapterTextContent);

        var endnotes = htmlDocument.GetElementbyId("endnotes");
        endnotes.ChildNodes.Add(HtmlNode.CreateNode($"<p id=\"{sequence}\">{sequence}: {endnote}</p>"));
        return htmlDocument.DocumentNode.OuterHtml;
    }

    public string GetEmptyEndnotesContent()
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

public class Paragraph
{
    public string Text { get; set; }
    public HtmlNode HtmlNode { get; set; }
}