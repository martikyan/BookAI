namespace BookAI.Services.Abstraction;

public interface ICalibreService
{
    Task<Stream> ConvertOrFixEpubAsync(Stream inputStream);
}