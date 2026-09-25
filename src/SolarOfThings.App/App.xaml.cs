using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

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
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        var database = _host.Services.GetRequiredService<SqliteDatabase>();
        database.Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
