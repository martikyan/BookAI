using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BookAI.Services;

public class CalibreService(ILogger<CalibreService> logger)
{
    public async Task<Stream> ConvertOrFixEpubAsync(Stream inputStream)
    {
        // Create a temporary file to store the initial EPUB.
        string originalTempFile = $"{Path.GetTempFileName()}.epub";
        try
        {
            using (var fs = File.OpenWrite(originalTempFile))
            {
                await inputStream.CopyToAsync(fs);
            }

            // Check if Calibre's ebook-convert command is available.
            var calibreInstalled = await IsCalibreInstalledAsync();

            // Set final file path to the original file by default.
            string finalFilePath = originalTempFile;
            string convertedTempFile = null;

            if (!calibreInstalled)
            {
                throw new InvalidOperationException("Calibre is not installed.");
            }

            // Create a temporary file with a .epub extension for the conversion output.
            convertedTempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".epub");

            // Set up the process info to run ebook-convert.
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ebook-convert",
                Arguments = $"\"{originalTempFile}\" \"{convertedTempFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            logger.LogInformation("Converting Epub to {Destination}", convertedTempFile);
            using var process = Process.Start(processStartInfo);
            await process.WaitForExitAsync();
            logger.LogInformation("Converting process exited with code {ExitCode}", process.ExitCode);

            // If conversion succeeds (exit code 0) and the file exists, use it.
            if (process.ExitCode == 0 && File.Exists(convertedTempFile))
            {
                finalFilePath = convertedTempFile;
            }
            else
            {
                logger.LogError("Failed to convert Epub to {Destination}", convertedTempFile);
                throw new InvalidOperationException("Could not convert the epub file.");
            }

            // Read the final EPUB file into a MemoryStream.
            var memoryStream = new MemoryStream();
            using (var fs = File.OpenRead(finalFilePath))
            {
                await fs.CopyToAsync(memoryStream);
            }

            memoryStream.Position = 0; // Reset stream position

            // Clean up temporary files.
            try
            {
                File.Delete(originalTempFile);
                if (convertedTempFile != null && File.Exists(convertedTempFile))
                {
                    File.Delete(convertedTempFile);
                }
            }
            catch
            {
                // Optionally log the exception, but we ignore deletion errors.
            }

            return memoryStream;
        }
        catch
        {
            // In case of any error, make sure to delete the temporary file.
            try
            {
                File.Delete(originalTempFile);
            }
            catch
            {
            }

            throw;
        }
    }

    /// <summary>
    /// Checks if the Calibre ebook-convert command is available.
    /// </summary>
    private async Task<bool> IsCalibreInstalledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ebook-convert",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            // Wait up to 3 seconds for the process to exit.
            await process.WaitForExitAsync(cancellationToken);
            logger.LogInformation("ebook-convert command version process exited with code {ExitCode}", process.ExitCode);
            return process.ExitCode == 0;
        }
        catch
        {
            // If any exception occurs (e.g. command not found), return false.
            return false;
        }
    }
}