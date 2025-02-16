using BookAI.Services.Abstraction;
using BookAI.Services.Models;
using EpubCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookAI.Services.Tests;

public class EpubServiceTests
{
    private EpubService _systemUnderTest;
    private Mock<IHtmlService> _htmlServiceMock = new Mock<IHtmlService>();

    private readonly string _epubPath = Path.Combine("Resources", "epub30-spec.epub");

    [SetUp]
    public void Setup()
    {
        _systemUnderTest = new(_htmlServiceMock.Object, Mock.Of<IAIService>(), Mock.Of<EndnoteSequenceProvider>(), Mock.Of<ICalibreService>(), NullLogger<EpubService>.Instance);
    }

    [Test]
    public void GetTextChunks_OnValidEpub_ReturnsContinuousTextChunks()
    {
        var book = EpubReader.Read(File.OpenRead(_epubPath), false);

        var guids = new List<string>();

        var result = Enumerable.Range(0, 1000).Select(_ =>
        {
            var guid = Guid.NewGuid().ToString();
            guids.Add(guid);
            return new Paragraph { Text = guid };
        });

        _htmlServiceMock.Setup(s => s.GetPlainText(It.IsAny<string>()))
            .Returns(result);

        var chunks = _systemUnderTest.GetTextChunks(book);

        Assert.That(chunks.Any());
        Assert.That(guids.Any());
        Assert.That(chunks.Count(c => string.IsNullOrEmpty(c.Context)) <= 1);

        var chunkedGuids = chunks.SelectMany(c => c.Text.Split('\n')).ToList();
        for (var i = 0; i < guids.Count; i++)
        {
            Assert.That(chunkedGuids[i] == guids[i]);
        }
    }
}