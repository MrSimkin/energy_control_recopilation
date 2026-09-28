using System.Windows;
using System.Windows.Controls;

namespace SolarOfThings.App.Controls;

public partial class QuickDatePicker : UserControl
{
    private bool _syncing;

    public QuickDatePicker()
    {
        InitializeComponent();
        MonthSelector.ItemsSource = Enumerable.Range(1, 12)
            .Select(month => month.ToString("00"))
            .ToArray();
        EnsureYearItems(DateTime.Today.Year);
        Loaded += (_, _) => SyncFromSelectedDate();
    }

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(QuickDatePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateChanged));

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public event EventHandler? SelectedDateChanged;

    private static void OnSelectedDateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (QuickDatePicker)d;
        control.SyncFromSelectedDate();
        control.SelectedDateChanged?.Invoke(control, EventArgs.Empty);
    }

    private void SyncFromSelectedDate()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            InnerDatePicker.SelectedDate = SelectedDate;
            if (SelectedDate.HasValue)
            {
                EnsureYearItems(SelectedDate.Value.Year);
                MonthSelector.SelectedIndex = SelectedDate.Value.Month - 1;
                YearSelector.SelectedItem = SelectedDate.Value.Year;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void InnerDatePicker_SelectedDateChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        SelectedDate = InnerDatePicker.SelectedDate;
    }

    private void DatePartSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_syncing ||
            MonthSelector.SelectedIndex < 0 ||
            YearSelector.SelectedItem is not int year)
        {
            return;
        }

        var month = MonthSelector.SelectedIndex + 1;
        var current = SelectedDate ?? DateTime.Today;
        var day = Math.Min(
            current.Day,
            DateTime.DaysInMonth(year, month));
        SelectedDate = new DateTime(year, month, day);
    }

    private void EnsureYearItems(int selectedYear)
    {
        var first = Math.Min(2000, selectedYear);
        var last = Math.Max(DateTime.Today.Year + 2, selectedYear);
        var years = Enumerable.Range(first, last - first + 1).ToArray();

        YearSelector.ItemsSource = years;
    }
}
