using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SolarOfThings.Core.History;

namespace SolarOfThings.App;

public partial class MainWindow
{
    private sealed record CoverageIssueDisplayRow(
        DateOnly LocalDay, string DateDisplay, string Status,
        string StatusDisplay, int Frames, int Pages, int Retries,
        string UpdatedDisplay);

    private IReadOnlyList<CoverageIssueDisplayRow> _coverageIssueRows =
        Array.Empty<CoverageIssueDisplayRow>();
    private string? _coverageIssueDeviceId;

    private void ClearDataCoverageIssues()
    {
        _coverageIssueRows = Array.Empty<CoverageIssueDisplayRow>();
        _coverageIssueDeviceId = null;
        if (DataIssueDaysGrid is null || DataIssueDayAnalyzeButton is null ||
            DataIssueDaysStatusText is null)
            return;
        DataIssueDaysGrid.ItemsSource = null;
        DataIssueDayAnalyzeButton.IsEnabled = false;
        DataIssueDaysStatusText.SetResourceReference(
            TextBlock.TextProperty, "Data.IssueDaysLoading");
    }

    private void ShowDataCoverageIssues(
        string deviceId, IReadOnlyList<HistoryCoverageIssueDay> source)
    {
        var spanish = _localization.CurrentLanguage.StartsWith("es",
            StringComparison.OrdinalIgnoreCase);
        _coverageIssueDeviceId = deviceId;
        _coverageIssueRows = source.Select(day =>
        {
            var updated = DateTimeOffset.TryParse(day.UpdatedUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out var timestamp)
                ? timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'",
                    CultureInfo.InvariantCulture)
                : "—";
            return new CoverageIssueDisplayRow(
                day.LocalDay,
                day.LocalDay.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                day.Status,
                _localization.GetString("Data.IssueStatus." + day.Status),
                day.FrameCount, day.PageCount, day.RetryCount, updated);
        }).ToArray();

        ApplyDataCoverageIssueFilter();
        DataIssueDaysStatusText.Text = _coverageIssueRows.Count == 0
            ? _localization.GetString("Data.IssueDaysNone")
            : string.Format(
                _localization.GetString("Data.IssueDaysShowing"),
                _coverageIssueRows.Count);
    }

    private void DataIssueDayFilter_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || DataIssueDaysGrid is null)
            return;
        ApplyDataCoverageIssueFilter();
    }

    private void ApplyDataCoverageIssueFilter()
    {
        if (DataIssueDaysGrid is null || DataIssueDayAnalyzeButton is null)
            return;
        var filter = DataIssueDayFilter?.SelectedValue?.ToString() ?? "all";
        DataIssueDaysGrid.ItemsSource = filter == "all"
            ? _coverageIssueRows
            : _coverageIssueRows.Where(item => item.Status == filter).ToArray();
        DataIssueDayAnalyzeButton.IsEnabled = false;
    }

    private void DataIssueDaysGrid_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (DataIssueDayAnalyzeButton is null)
            return;
        DataIssueDayAnalyzeButton.IsEnabled =
            !_windowClosed &&
            DataContent.Visibility == Visibility.Visible &&
            DataIssueDaysGrid.SelectedItem is CoverageIssueDisplayRow &&
            _coverageIssueDeviceId is not null &&
            string.Equals(_coverageIssueDeviceId, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal);
    }

    private void DataIssueDayAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_windowClosed || DataContent.Visibility != Visibility.Visible ||
            DataIssueDaysGrid.SelectedItem is not CoverageIssueDisplayRow day ||
            _coverageIssueDeviceId is null ||
            !string.Equals(_coverageIssueDeviceId, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal))
            return;

        // Set the station-local civil date BEFORE navigating; ShowPage
        // schedules just one analysis refresh with the chosen range.
        _analysisPresetGeneration++;
        _analysisRefreshGeneration++;
        _analysisPresetLoading = false;
        _analysisCustomRangePendingApply = false;
        _analysisRangeInitializationPending = false;
        _suppressAnalysisRangeSelection = true;
        try
        {
            AnalysisRangePresetSelector.SelectedValue = "custom";
            var date = day.LocalDay.ToDateTime(TimeOnly.MinValue);
            AnalysisFromDatePicker.SelectedDate = date;
            AnalysisToDatePicker.SelectedDate = date;
            AnalysisAggregationSelector.SelectedValue = "Hour";
        }
        finally { _suppressAnalysisRangeSelection = false; }
        ShowPage("Analysis");
    }
}
