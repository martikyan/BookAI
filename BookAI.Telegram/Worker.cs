namespace BookAI.Telegram;
#pragma warning disable SKEXP0001

internal sealed class Worker(
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await ChatLoopAsync(stoppingToken);

            await Task.Delay(1000, stoppingToken);
        }
    }

    /// <summary>
    ///     Contains the main chat loop for the application.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken" /> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the chat loop is shut down.</returns>
    private async Task ChatLoopAsync(CancellationToken cancellationToken)
    {
    }

    /// <summary>
    ///     Load all configured PDFs into the vector store.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken" /> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the loading is complete.</returns>
    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var file = File.OpenRead("/users/user/Downloads/The_Hitchhiker_39_s_Guide_to_the_G_-_Douglas_Adams_Non-Illustrated.epub");
            Console.WriteLine("Loading EPUB into vector store");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load PDFs: {ex}");
            throw;
        }
    }
}