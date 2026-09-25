using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;

namespace SolarOfThings.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(_ => new AppPaths());
        builder.Services.AddSingleton<SqliteDatabase>();
        builder.Services.AddSingleton<AppSettingsRepository>();
        builder.Services.AddSingleton<DiagnosticsFileWriter>();
        builder.Services.AddSingleton<ISecretStore, DpapiFileSecretStore>();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        var database = _host.Services.GetRequiredService<SqliteDatabase>();
        database.Initialize();

        var localization = _host.Services.GetRequiredService<LocalizationService>();
        localization.Initialize();

        var diagnostics = _host.Services.GetRequiredService<DiagnosticsFileWriter>();
        diagnostics.Write("Information", "ApplicationStarted", "Application infrastructure initialized.");

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
