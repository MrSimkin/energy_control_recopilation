using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.Normalization;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.SolarOfThings;

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
        builder.Services.AddSingleton<BatteryConfigurationService>();
        builder.Services.AddSingleton<DiagnosticsFileWriter>();
        builder.Services.AddSingleton<ApiDiagnosticsStore>();
        builder.Services.AddSingleton<ISecretStore, DpapiFileSecretStore>();
        builder.Services.AddSingleton<IotOpenCredentialStore>();
        builder.Services.AddSingleton<SolarOfThingsApiClient>();
        builder.Services.AddSingleton<SolarOfThingsSessionManager>();
        builder.Services.AddSingleton<CommissioningProfileRepository>();
        builder.Services.AddSingleton<CommissioningService>();
        builder.Services.AddSingleton<HistoryRepository>();
        builder.Services.AddSingleton<HistoryIngestionService>();
        builder.Services.AddSingleton<NormalizationRepository>();
        builder.Services.AddSingleton<NormalizationService>();
        builder.Services.AddSingleton<InstallationHealthRepository>();
        builder.Services.AddSingleton<InstallationContextPolicyService>();
        builder.Services.AddSingleton<InstallationHealthService>();
        builder.Services.AddSingleton<CurrentStateSnapshotService>();
        builder.Services.AddSingleton<HouseholdOperatingStateService>();
        builder.Services.AddSingleton<HouseholdBehaviorRepository>();
        builder.Services.AddSingleton<HouseholdBehaviorService>();
        builder.Services.AddSingleton<HouseholdBehaviorStatisticsService>();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddTransient<CommissioningWindow>();
        builder.Services.AddTransient<DeveloperDiagnosticsWindow>();

        _host = builder.Build();

        var database = _host.Services.GetRequiredService<SqliteDatabase>();
        database.Initialize();

        var localization = _host.Services.GetRequiredService<LocalizationService>();
        localization.Initialize();

        var diagnostics = _host.Services.GetRequiredService<DiagnosticsFileWriter>();
        diagnostics.Write("Information", "ApplicationStarted", "Application infrastructure initialized.");

        var apiDiagnostics = _host.Services.GetRequiredService<ApiDiagnosticsStore>();
        apiDiagnostics.RecordLocal(
            "Application",
            "Startup",
            "SUCCESS",
            "Application started.",
            JsonSerializer.Serialize(new
            {
                schemaVersion = database.GetSchemaVersion(),
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString()
            }));

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
