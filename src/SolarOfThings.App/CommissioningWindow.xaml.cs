using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.App;

public partial class CommissioningWindow : Window
{
    private readonly SolarOfThingsSessionManager _session;
    private readonly CommissioningService _commissioning;
    private readonly IotOpenCredentialStore _credentialStore;
    private readonly ApiDiagnosticsStore _diagnostics;
    private readonly IServiceProvider _services;

    private DiscoveryItem? _selectedStation;
    private IReadOnlyList<DiscoveryItem> _stations = [];

    public CommissioningWindow(
        SolarOfThingsSessionManager session,
        CommissioningService commissioning,
        IotOpenCredentialStore credentialStore,
        ApiDiagnosticsStore diagnostics,
        IServiceProvider services)
    {
        _session = session;
        _commissioning = commissioning;
        _credentialStore = credentialStore;
        _diagnostics = diagnostics;
        _services = services;

        InitializeComponent();

        AccountTextBox.Text = _session.Account ?? string.Empty;
        UpdateProtocolCredentialStatus();
    }

    private async void ConnectDiscover_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        ProgressListBox.Items.Clear();
        ResultTextBox.Clear();

        try
        {
            var timeZone = GetTimeZone();

            if (!_session.HasSession)
            {
                if (string.IsNullOrWhiteSpace(AccountTextBox.Text) ||
                    string.IsNullOrEmpty(PasswordInput.Password))
                {
                    throw new InvalidOperationException(
                        "Ingrese cuenta y contraseña, o cargue un par de tokens existente.");
                }

                AddProgress("Authenticate", "RUNNING", "Autenticando localmente con Solar of Things...");

                await _session.LoginAsync(
                    AccountTextBox.Text.Trim(),
                    PasswordInput.Password,
                    RememberCheckBox.IsChecked == true,
                    timeZone);

                AddProgress("Authenticate", "PASS", "Autenticación completada.");
            }

            var progress = new Progress<CommissioningProgress>(
                p => AddProgress(p.Step, p.Status, p.Message));

            var discovery = await _commissioning.DiscoverStationsAsync(
                timeZone,
                progress);

            _stations = discovery.Stations;
            StationComboBox.ItemsSource = _stations;

            if (_stations.Count > 0)
            {
                StationComboBox.SelectedIndex = 0;
            }

            ResultTextBox.Text =
                $"Sesión activa. Estaciones accesibles: {_stations.Count}.\r\n" +
                "Seleccione estación y dispositivo antes de ejecutar el commissioning completo.";
        }
        catch (Exception ex)
        {
            AddProgress("Connect", "FAIL", ex.Message);
            ResultTextBox.Text = ex.ToString();
            _diagnostics.RecordLocal(
                "CommissioningUI",
                "ConnectDiscover",
                "FAIL",
                ex.Message);
        }
        finally
        {
            PasswordInput.Password = string.Empty;
            SetBusy(false);
        }
    }

    private async void StationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StationComboBox.SelectedItem is not DiscoveryItem station)
        {
            return;
        }

        _selectedStation = station;
        CommissionButton.IsEnabled = false;
        DeviceComboBox.ItemsSource = null;

        try
        {
            var progress = new Progress<CommissioningProgress>(
                p => AddProgress(p.Step, p.Status, p.Message));

            var result = await _commissioning.DiscoverDevicesAsync(
                station,
                GetTimeZone(),
                progress);

            DeviceComboBox.ItemsSource = result.Devices;

            if (result.Devices.Count > 0)
            {
                DeviceComboBox.SelectedIndex = 0;
                CommissionButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            AddProgress("DeviceDiscovery", "FAIL", ex.Message);
            ResultTextBox.Text = ex.ToString();
        }
    }

    private async void Commission_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStation is null ||
            DeviceComboBox.SelectedItem is not DiscoveryItem device)
        {
            return;
        }

        SetBusy(true);

        try
        {
            var progress = new Progress<CommissioningProgress>(
                p => AddProgress(p.Step, p.Status, p.Message));

            var profile = await _commissioning.CommissionAsync(
                _selectedStation,
                device,
                GetTimeZone(),
                progress);

            var sb = new StringBuilder();
            sb.AppendLine("COMMISSIONING COMPLETADO");
            sb.AppendLine($"Estación: {profile.StationName} [{profile.StationId}]");
            sb.AppendLine($"Zona horaria: {profile.StationTimeZone}");
            sb.AppendLine($"Dispositivo: {profile.DeviceName} [{profile.DeviceId}]");
            sb.AppendLine($"Serie: {profile.SerialNumber ?? "-"}");
            sb.AppendLine($"Modelo: {profile.Model ?? "-"}");
            sb.AppendLine($"Fabricante: {profile.Manufacturer ?? "-"}");
            sb.AppendLine($"DTU/logger: {profile.DtuId ?? "-"}");
            sb.AppendLine($"Protocolo: {profile.GatherProtocolNumber ?? "-"}");
            sb.AppendLine($"Software/Firmware: {profile.SoftwareVersion ?? "-"}");
            sb.AppendLine($"dataSource: {profile.DataSource ?? "-"}");
            sb.AppendLine($"Atributos: {profile.GatherAttributeCount} ({profile.GatherAttributesStatus})");
            sb.AppendLine($"Estado actual: {profile.LatestStateStatus}");
            sb.AppendLine($"Flujo de energía: {profile.EnergyFlowStatus}");
            sb.AppendLine($"Historial: {profile.HistoryStatus}");
            sb.AppendLine($"Agregado: {profile.AggregateStatus}");
            sb.AppendLine($"Alarmas: {profile.AlarmStatus}");
            sb.AppendLine();
            sb.AppendLine("Use 'Copiar informe diagnóstico' para compartir evidencia técnica sanitizada.");

            ResultTextBox.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            AddProgress("Commissioning", "FAIL", ex.Message);
            ResultTextBox.Text = ex.ToString();
            _diagnostics.RecordLocal(
                "CommissioningUI",
                "Commission",
                "FAIL",
                ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveProtocolCredential_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _credentialStore.Save(
                AppIdTextBox.Text.Trim(),
                AppSecretInput.Password,
                secretIsEncrypted: true);

            AppSecretInput.Password = string.Empty;
            UpdateProtocolCredentialStatus();

            _diagnostics.RecordLocal(
                "ProtocolCredential",
                "LocalConfigure",
                "SUCCESS",
                "IoT Open client material configured in Windows-protected storage. Values are not logged.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Solar of Things", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (_credentialStore.TryImportFromEnvironment(persist: true, out var source))
        {
            UpdateProtocolCredentialStatus();
            _diagnostics.RecordLocal(
                "ProtocolCredential",
                "EnvironmentImport",
                "SUCCESS",
                $"Client material imported from {source} environment variables. Values are not logged.");
        }
        else
        {
            MessageBox.Show(
                "No se encontraron SOLAR_OF_THINGS_APP_ID + SOLAR_OF_THINGS_APP_SECRET_ENC/APP_SECRET.",
                "Solar of Things",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void UseTokenPair_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AccessTokenInput.Password))
        {
            MessageBox.Show(
                "Ingrese al menos el access token.",
                "Solar of Things",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _session.UseTokenPair(
            AccessTokenInput.Password,
            RefreshTokenInput.Password,
            RememberCheckBox.IsChecked == true,
            GetTimeZone());

        AccessTokenInput.Password = string.Empty;
        RefreshTokenInput.Password = string.Empty;

        AddProgress("Session", "PASS", "Par de tokens cargado localmente.");
    }

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_diagnostics.BuildSanitizedReport());
        AddProgress("Diagnostics", "PASS", "Informe diagnóstico sanitizado copiado al portapapeles.");
    }

    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var window = _services.GetRequiredService<DeveloperDiagnosticsWindow>();
        window.Owner = this;
        window.ShowDialog();
    }

    private void ForgetSession_Click(object sender, RoutedEventArgs e)
    {
        _session.ResetLocalSession(forgetRememberedCredentials: true);
        AddProgress("Session", "PASS", "Sesión local y credenciales recordadas eliminadas.");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void AddProgress(string step, string status, string message)
    {
        ProgressListBox.Items.Add($"[{status}] {step}: {message}");
        if (ProgressListBox.Items.Count > 0)
        {
            ProgressListBox.ScrollIntoView(ProgressListBox.Items[ProgressListBox.Items.Count - 1]);
        }
    }

    private string GetTimeZone()
    {
        return string.IsNullOrWhiteSpace(TimeZoneTextBox.Text)
            ? "America/Santiago"
            : TimeZoneTextBox.Text.Trim();
    }

    private void SetBusy(bool busy)
    {
        ConnectDiscoverButton.IsEnabled = !busy;
        CommissionButton.IsEnabled = !busy &&
                                     _selectedStation is not null &&
                                     DeviceComboBox.SelectedItem is not null;
    }

    private void UpdateProtocolCredentialStatus()
    {
        ProtocolCredentialStatus.Text = _credentialStore.TryRead(out _)
            ? "Configurada localmente"
            : "No configurada";
    }
}
