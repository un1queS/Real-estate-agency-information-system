using System.Windows;
using RealEstateAgencyApp.Data;
using RealEstateAgencyApp.Services;

namespace RealEstateAgencyApp;

public partial class App : Application
{
    public static RealEstateRepository? Repository { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            AppConfig config = AppConfig.Load();
            await AccessDbInitializer.EnsureDatabaseAsync(config.DatabasePath);
            string connectionString = AccessDbInitializer.BuildConnectionString(config.DatabasePath);
            Repository = new RealEstateRepository(connectionString);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка запуска приложения: {ex.Message}\nПроверьте установку Microsoft Access Database Engine.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }
}
