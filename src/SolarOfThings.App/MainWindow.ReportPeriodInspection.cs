using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SolarOfThings.Core.Reporting;

namespace SolarOfThings.App;

public partial class MainWindow
{
    private sealed record ReportPeriodIssueDisplayRow(
        ReportPeriodIssue Issue, string LocalLabel, string State,
        string Solar, string House, string Grid, string Battery);

    private EnergyReportRequest? _reportInspectedRequest;
    private string? _reportInspectedDevice;
    private IReadOnlyList<ReportPeriodIssueDisplayRow> _reportInspectedRows =
        Array.Empty<ReportPeriodIssueDisplayRow>();

    private void ClearReportPreviewInspection()
    {
        _reportInspectedRows = Array.Empty<ReportPeriodIssueDisplayRow>();
        _reportInspectedRequest = null;
        _reportInspectedDevice = null;
        if (ReportPreviewIssueGrid is null ||
            ReportPreviewIssueAnalyzeButton is null ||
            ReportPreviewIssueSummaryText is null)
            return;

        ReportPreviewIssueGrid.ItemsSource = null;
        ReportPreviewIssueAnalyzeButton.IsEnabled = false;
        ReportPreviewIssueSummaryText.Text = string.Empty;
    }

    private void RenderReportPreviewInspection(
        ReportPeriodInspectionSummary inspection, EnergyReportRequest request)
    {
        var culture = _localization.CurrentLanguage.StartsWith("es",
            StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("es-CL")
            : CultureInfo.GetCultureInfo("en-US");
        _reportInspectedRequest = request;
        _reportInspectedDevice = request.DeviceId;
        _reportInspectedRows = inspection.LatestAffected
            .Select(issue => new ReportPeriodIssueDisplayRow(
                issue,
                issue.SourceRow.LocalLabel,
                _localization.GetString(issue.AvailableEnergySignals == 0
                    ? "Reports.IssueState.None" : "Reports.IssueState.Partial"),
                FormatCoverage(issue.PvCoveragePercent, culture),
                FormatCoverage(issue.HouseCoveragePercent, culture),
                FormatCoverage(issue.GridCoveragePercent, culture),
                FormatCoverage(issue.BatteryCoveragePercent, culture)))
            .ToArray();

        ReportPreviewIssueGrid.ItemsSource = _reportInspectedRows;
        ReportPreviewIssueAnalyzeButton.IsEnabled = false;
        ReportPreviewIssueSummaryText.Text = inspection.AffectedBuckets == 0
            ? _localization.GetString("Reports.IssueNone")
            : string.Format(_localization.GetString("Reports.IssueSummary"),
                inspection.AffectedBuckets, inspection.ObservedBuckets,
                _reportInspectedRows.Count);
    }

    private static string FormatCoverage(double? value, CultureInfo culture) =>
        value.HasValue ? value.Value.ToString("N1", culture) + " %" : "—";

    private void ReportPreviewIssueGrid_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (ReportPreviewIssueAnalyzeButton is null)
            return;
        ReportPreviewIssueAnalyzeButton.IsEnabled =
            !_windowClosed && ReportsContent.Visibility == Visibility.Visible &&
            ReportPreviewIssueGrid.SelectedItem is ReportPeriodIssueDisplayRow &&
            _reportInspectedDevice is not null &&
            string.Equals(_reportInspectedDevice, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal) &&
            _reportInspectedRequest == GetCurrentReportRequest();
    }

    private void ReportPreviewIssueAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_windowClosed || ReportsContent.Visibility != Visibility.Visible ||
            ReportPreviewIssueGrid.SelectedItem is not ReportPeriodIssueDisplayRow selected ||
            _reportInspectedRequest is null || _reportInspectedDevice is null ||
            !string.Equals(_reportInspectedDevice, _profiles.Get()?.DeviceId,
                StringComparison.Ordinal) ||
            _reportInspectedRequest != GetCurrentReportRequest())
            return;

        ReportPeriodInspectionSelection target;
        try
        {
            target = ReportPeriodInspectionPolicy.SelectForAnalysis(
                selected.Issue, _reportInspectedRequest);
        }
        catch (InvalidOperationException)
        {
            ReportPreviewStatusText.Text =
                _localization.GetString("Reports.IssueSelectionInvalid");
            return;
        }

        // Select BEFORE opening Analysis, so its existing background refresh
        // loads the chosen station-local bucket exactly once.
        _analysisPresetGeneration++;
        _analysisRefreshGeneration++;
        _analysisPresetLoading = false;
        _analysisRangeInitializationPending = false;
        _analysisCustomRangePendingApply = false;
        _suppressAnalysisRangeSelection = true;
        try
        {
            AnalysisRangePresetSelector.SelectedValue = "custom";
            AnalysisFromDatePicker.SelectedDate =
                target.From.ToDateTime(TimeOnly.MinValue);
            AnalysisToDatePicker.SelectedDate =
                target.To.ToDateTime(TimeOnly.MinValue);
            AnalysisAggregationSelector.SelectedValue =
                target.Aggregation.ToString();
        }
        finally { _suppressAnalysisRangeSelection = false; }
        ShowPage("Analysis");
    }
}
