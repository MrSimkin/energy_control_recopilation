using System.IO;
using System.Diagnostics;
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
        var startupWatch = Stopwatch.StartNew();
        var performance = new UiPerformanceRecorder();

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
        var importWatch = Stopwatch.StartNew();
        var importResult = await Task.Run(() =>
            DataImportService.ApplyPendingImport(
                appPaths,
                message => RenderStartupStatus(startupStatus, message)));
        performance.Record("Startup.ImportCheck", importWatch.Elapsed);

        RenderStartupStatus(startupStatus, "Inicializando base de datos...");
        await Dispatcher.Yield(DispatcherPriority.Background);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(appPaths);
        builder.Services.AddSingleton(performance);
        builder.Services.AddSingleton<SqliteDatabase>();
        builder.Services.AddSingleton<DatabaseBackupService>();
        builder.Services.AddSingleton<FullBackupService>();
        builder.Services.AddSingleton<CompleteBackupInventoryService>();
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
        var fullBackupService = _host.Services.GetRequiredService<FullBackupService>();
        // Never migrate existing user data without a completed, fully
        // verified backup. The user may postpone the update and keep the
        // previous installation; no schema write happens in that case.
        if (File.Exists(database.DatabasePath))
        {
            int priorSchemaVersion = 0;
            try { priorSchemaVersion = database.GetSchemaVersion(); }
            catch { /* Inability to read the schema must fail closed below. */ }
            if (priorSchemaVersion < SqliteDatabase.CurrentSchemaVersion)
            {
                var confirmed = MessageBox.Show(
                    "Esta versión necesita actualizar la estructura de la base de datos. " +
                    "Primero crearemos un respaldo COMPLETO verificado (base, boletas y tarifas).\n\n" +
                    "¿Continuar? No = posponer la actualización y conservar los datos.",
                    "Respaldo completo antes de migración",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirmed != MessageBoxResult.Yes)
                {
                    Shutdown(0);
                    return;
                }
                RenderStartupStatus(startupStatus,
                    "Creando respaldo completo verificado antes de migrar...");
                try
                {
                    await Task.Run(() => fullBackupService.Create(
                        ProductInfo.ProductVersion, ProductInfo.BuildNumber,
                        ProductInfo.SourceRevision));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "La migración NO comenzó: no fue posible completar y verificar " +
                        "un respaldo íntegro. Se conservan los datos existentes.\n\n" +
                        ex.Message,
                        "Protección de datos", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown(-1);
                    return;
                }
            }
        }

        RenderStartupStatus(startupStatus, "Inicializando base de datos...");
        var databaseWatch = Stopwatch.StartNew();
        await Task.Run(database.Initialize);
        performance.Record("Startup.SQLiteInitialize", databaseWatch.Elapsed);

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

        var constructorWatch = Stopwatch.StartNew();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        performance.Record("Startup.MainWindowConstruction", constructorWatch.Elapsed);
        MainWindow = mainWindow;

        startupWindow.Hide();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Show();
        performance.Record("Startup.WindowShown", startupWatch.Elapsed);
        mainWindow.Activate();
        mainWindow.Focus();
        startupWindow.Close();

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Full backups are user-initiated. Show the skippable weekly reminder
        // only after the main window becomes usable. Do not create daily DB copies.
        _ = mainWindow.Dispatcher.BeginInvoke(
            new Action(async () => await mainWindow.ShowWeeklyBackupReminderIfDueAsync()),
            DispatcherPriority.Background);
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
