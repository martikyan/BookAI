using BookAI.Services.Models;

namespace BookAI.Services.Abstraction;

public interface IHtmlService
{
    string? AddReference(string html, string sentence, string sequence);
    IEnumerable<Paragraph> GetPlainText(string html);
    string AddEndnote(string endnote, string endnotesChapterTextContent, string sequence, string referenceFileName);
}