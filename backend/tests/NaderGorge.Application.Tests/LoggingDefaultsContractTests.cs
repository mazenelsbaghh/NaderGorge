using System.Text.Json;

namespace NaderGorge.Application.Tests;

public sealed class LoggingDefaultsContractTests
{
    private const string EfCommandCategory =
        "Microsoft.EntityFrameworkCore.Database.Command";

    [Fact]
    public void DefaultsSuppressRoutineEfCommands_WhileDevelopmentKeepsThemVisible()
    {
        Assert.Equal("Warning", ReadLogLevel("appsettings.json"));
        Assert.Equal("Information", ReadLogLevel("appsettings.Development.json"));
    }

    private static string ReadLogLevel(string fileName)
    {
        var path = FindApiConfiguration(fileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement
            .GetProperty("Logging")
            .GetProperty("LogLevel")
            .GetProperty(EfCommandCategory)
            .GetString()
            ?? throw new InvalidDataException(
                $"{EfCommandCategory} must have a configured log level.");
    }

    private static string FindApiConfiguration(string fileName)
    {
        foreach (var start in new[]
                 {
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(
                    directory.FullName,
                    "backend",
                    "src",
                    "NaderGorge.API",
                    fileName);
                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            $"backend/src/NaderGorge.API/{fileName} is required.");
    }
}
