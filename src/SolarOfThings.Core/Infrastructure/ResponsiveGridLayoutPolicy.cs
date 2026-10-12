namespace SolarOfThings.Core.Infrastructure;

/// <summary>
/// Pure, deterministic breakpoint choice. Widths inside one breakpoint use
/// WPF's natural Grid sizing; changing only the pixel width must not trigger
/// costly reconstruction of all card Grid definitions.
/// </summary>
public static class ResponsiveGridLayoutPolicy
{
    public static int ColumnsForUsableWidth(double usableWidth)
    {
        if (!double.IsFinite(usableWidth) || usableWidth < 0)
            usableWidth = 0;
        return usableWidth switch
        {
            < 620 => 1,
            < 1040 => 2,
            _ => 4
        };
    }
}
