using Microsoft.Extensions.Configuration;
using System.IO;

namespace RealEstateAgencyApp.Services;

public sealed class AppConfig
{
    public string DatabasePath { get; }

    private AppConfig(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public static AppConfig Load()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        string configuredPath = configuration["Database:Path"] ?? "../../../../RealEstate.accdb";
        string fullPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));

        // Если в корне проекта есть пользовательская база Access,
        // используем ее приоритетно, чтобы работать с реальными данными.
        string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        if (Directory.Exists(projectRoot))
        {
            string? userDb = Directory
                .GetFiles(projectRoot, "*.accdb")
                .FirstOrDefault(path => !string.Equals(Path.GetFileName(path), "RealEstate.accdb", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(userDb))
            {
                fullPath = userDb;
            }
        }

        return new AppConfig(fullPath);
    }
}
