using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SolarOfThings.App.Localization;
using SolarOfThings.Core.Backup;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.Infrastructure;
using SolarOfThings.Core.Installation;
using SolarOfThings.Core.Normalization;
using SolarOfThings.Core.Reporting;
using SolarOfThings.Core.History;
using SolarOfThings.Core.Help;
using SolarOfThings.Core.Security;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Utility;

namespace SolarOfThings.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var startupWindow = CreateStartupWindow(out var startupStatus);
        startupWindow.Show();
        RenderStartupStatus(startupStatus, "Abriendo Solar Energy Monitor...");
        await Dispatcher.Yield(DispatcherPriority.Background);

        if (e.Args.Any(arg =>
                string.Equals(arg, "--apply-import", StringComparison.OrdinalIgnoreCase)))
        {
            RenderStartupStatus(startupStatus, "Preparando datos importados...");
            await Task.Delay(900);
        }

        var appPaths = new AppPaths();
        var importResult = await Task.Run(() =>
            DataImportService.ApplyPendingImport(
                appPaths,
                message => RenderStartupStatus(startupStatus, message)));

        RenderStartupStatus(startupStatus, "Inicializando base de datos...");
        await Dispatcher.Yield(DispatcherPriority.Background);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(appPaths);
        builder.Services.AddSingleton<SqliteDatabase>();
        builder.Services.AddSingleton<DatabaseBackupService>();
        builder.Services.AddSingleton<AppSettingsRepository>();
        builder.Services.AddSingleton<BatteryConfigurationService>();
        builder.Services.AddSingleton<DiagnosticsFileWriter>();
        builder.Services.AddSingleton<ApiDiagnosticsStore>();
        builder.Services.AddSingleton<InvestigationDiagnosticsService>();
        builder.Services.AddSingleton<PhaseDiagnosticsExportService>();
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
        builder.Services.AddSingleton<BatteryThresholdContextService>();
        builder.Services.AddSingleton<CurrentHouseholdSnapshotService>();
        builder.Services.AddSingleton<CurrentStateSnapshotService>();
        builder.Services.AddSingleton<HouseholdOperatingStateService>();
        builder.Services.AddSingleton<HouseholdBehaviorRepository>();
        builder.Services.AddSingleton<HouseholdBehaviorService>();
        builder.Services.AddSingleton<HouseholdBehaviorStatisticsService>();
        builder.Services.AddSingleton<EnergyRangeStatisticsService>();
        builder.Services.AddSingleton<TimeRangeSelectionService>();
        builder.Services.AddSingleton<PowerAggregationService>();
        builder.Services.AddSingleton<SocAggregationService>();
        builder.Services.AddSingleton<EnergyAggregationTableService>();
        builder.Services.AddSingleton<ReportPresetStore>();
        builder.Services.AddSingleton<FamilyReportAnalysisService>();
        builder.Services.AddSingleton<SourceAttributionService>();
        builder.Services.AddSingleton<EnergyReportExportService>();
        builder.Services.AddSingleton<UtilityMeterRepository>();
        builder.Services.AddSingleton<UtilityReconciliationService>();
        builder.Services.AddSingleton<UtilityGridImportStatisticalCompletionService>();
        builder.Services.AddSingleton<UtilityBillGapStatisticalCompletionService>();
        builder.Services.AddSingleton<UtilityBillTariffScenarioAnalysisService>();
        builder.Services.AddSingleton<UtilityBillReconciliationSummaryService>();
        builder.Services.AddSingleton<UtilityReconciliationReportService>();
        builder.Services.AddSingleton<UtilityBillAuditReportService>();
        builder.Services.AddSingleton<UtilityBillAuditAnnexExportService>();
        builder.Services.AddSingleton<TariffPublicationRepository>();
        builder.Services.AddSingleton<TariffRateCandidateRepository>();
        builder.Services.AddSingleton<EnelBt1TariffTextParser>();
        builder.Services.AddSingleton<EnelTariffNormalizationService>();
        builder.Services.AddSingleton<CneTariffEvidenceCaptureService>();
        builder.Services.AddSingleton<EnelTariffPdfImportService>();
        builder.Services.AddSingleton<EnelUtilityBillPdfImportService>();
        builder.Services.AddSingleton<UtilityBillPdfReviewMergeService>();
        builder.Services.AddSingleton<TariffPublicationVersionResolver>();
        builder.Services.AddSingleton<TariffBillRateVerificationService>();
        builder.Services.AddSingleton<UtilityBillAuditV2Service>();
        builder.Services.AddSingleton<EnelTariffCaptureService>();
        builder.Services.AddSingleton<DataImportService>();
        builder.Services.AddSingleton<HelpManualExportService>();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddTransient<CommissioningWindow>();
        builder.Services.AddTransient<EnelTariffBrowserWindow>();
        builder.Services.AddTransient<DeveloperDiagnosticsWindow>();

        _host = builder.Build();

        var database = _host.Services.GetRequiredService<SqliteDatabase>();
        var backupService = _host.Services
            .GetRequiredService<DatabaseBackupService>();
        // A schema migration is the only reason to make a full backup
        // synchronously before opening the main window. Daily snapshots
        // of an unchanged schema must never block a normal launch.
        if (File.Exists(database.DatabasePath))
        {
            var priorSchemaVersion = 0;
            try
            {
                priorSchemaVersion = database.GetSchemaVersion();
            }
            catch
            {
                // Treat an unreadable schema as requiring a preflight
                // snapshot, whose integrity check will fail safely if the
                // database is corrupt. Do not guess and migrate blindly.
            }

            if (priorSchemaVersion < SqliteDatabase.CurrentSchemaVersion)
            {
                RenderStartupStatus(startupStatus,
                    "Creando respaldo verificado antes de actualizar SQLite...");
                try
                {
                    await Task.Run(() =>
                        backupService.CreateVerifiedBackup("automatic"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "La actualización de la base no comenzó porque " +
                        "no fue posible verificar un respaldo previo.\n\n" +
                        ex.Message,
                        "Protección de datos",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown(-1);
                    return;
                }
            }
        }

        RenderStartupStatus(startupStatus, "Inicializando base de datos...");
        await Task.Run(database.Initialize);

        if (importResult.Applied)
        {
            var importedSession =
                _host.Services.GetRequiredService<SolarOfThingsSessionManager>();
            if (importedSession.HasSession ||
                importedSession.HasRememberedCredentials)
            {
                _host.Services
                    .GetRequiredService<AppSettingsRepository>()
                    .Set("session.auto-connect-on-startup", bool.TrueString);
            }
        }

        RenderStartupStatus(startupStatus, "Preparando interfaz...");
        await Dispatcher.Yield(DispatcherPriority.Background);

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
        MainWindow = mainWindow;

        startupWindow.Hide();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Show();
        mainWindow.Activate();
        mainWindow.Focus();
        startupWindow.Close();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Routine daily backup is a separate, background maintenance task.
        // It works on a consistent SQLite snapshot; it never replaces the
        // active DB and cannot freeze startup as earlier synchronous work did.
        _ = Task.Run(() =>
        {
            try
            {
                var created = backupService.CreateAutomaticBackupIfDue();
                if (created is not null)
                {
                    apiDiagnostics.RecordLocal(
                        "Backup", "AutomaticDaily", "SUCCESS",
                        "A verified automatic database snapshot was created.",
                        JsonSerializer.Serialize(new
                        {
                            schema = created.SchemaVersion,
                            size_bytes = created.SizeBytes,
                            sha256 = created.Sha256
                        }));
                }
            }
            catch (Exception ex)
            {
                // Non-migration maintenance failure must be discoverable
                // without closing a healthy application or leaking paths.
                try
                {
                    apiDiagnostics.RecordLocal(
                        "Backup", "AutomaticDaily", "FAILED",
                        "Automatic backup requires attention.",
                        JsonSerializer.Serialize(new
                        {
                            error_type = ex.GetType().Name
                        }));
                }
                catch
                {
                    // Logging failure must not alter active user data.
                }
            }
        });
    }

    private static Window CreateStartupWindow(out TextBlock statusText)
    {
        statusText = new TextBlock
        {
            Text = "Abriendo...",
            FontSize = 13,
            Foreground = Brushes.Gainsboro,
            Margin = new Thickness(0, 10, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };

        var progress = new ProgressBar
        {
            Height = 5,
            IsIndeterminate = true,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(22)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Solar Energy Monitor",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        panel.Children.Add(new TextBlock
        {
            Text = ProductInfo.Display,
            FontSize = 11,
            Foreground = Brushes.Gainsboro,
            Margin = new Thickness(0, 3, 0, 10)
        });
        panel.Children.Add(statusText);
        panel.Children.Add(progress);

        return new Window
        {
            Title = "Solar Energy Monitor",
            Width = 460,
            Height = 190,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(45, 52, 64)),
            Content = panel,
            ShowInTaskbar = true,
            Topmost = false
        };
    }

    private static void RenderStartupStatus(
        TextBlock statusText,
        string message)
    {
        if (statusText.Dispatcher.CheckAccess())
        {
            statusText.Text = message;
            statusText.Dispatcher.Invoke(
                () => { },
                DispatcherPriority.Render);
            return;
        }

        statusText.Dispatcher.Invoke(
            () => statusText.Text = message,
            DispatcherPriority.Render);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
