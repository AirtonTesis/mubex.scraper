namespace CaptchaGridAnalyzer.Models;

/// <summary>
/// Represents a separator band with its start and end positions.
/// </summary>
public record SeparatorBand
{
    public int Start { get; init; }
    public int End { get; init; }
    public int Center => (Start + End) / 2;
    public int Width => End - Start + 1;

    public SeparatorBand(int start, int end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// Contains detailed grid information including separator positions for accurate cell extraction.
/// </summary>
public record GridInfo
{
    /// <summary>
    /// Gets the number of rows in the grid.
    /// </summary>
    public int Rows { get; init; }

    /// <summary>
    /// Gets the number of columns in the grid.
    /// </summary>
    public int Columns { get; init; }

    /// <summary>
    /// Gets the total number of cells in the grid (Rows * Columns).
    /// </summary>
    public int TotalCells => Rows * Columns;

    /// <summary>
    /// Gets the horizontal separator bands (between rows).
    /// Length = Rows - 1. Each band has Start/End positions.
    /// </summary>
    public List<SeparatorBand> RowSeparators { get; init; } = new();

    /// <summary>
    /// Gets the vertical separator bands (between columns).
    /// Length = Columns - 1. Each band has Start/End positions.
    /// </summary>
    public List<SeparatorBand> ColSeparators { get; init; } = new();

    /// <summary>
    /// Gets the Y coordinate where grid content starts (after header).
    /// </summary>
    public int ContentTop { get; init; }

    /// <summary>
    /// Gets the Y coordinate where grid content ends (before footer).
    /// </summary>
    public int ContentBottom { get; init; }

    /// <summary>
    /// Gets the X coordinate where grid content starts (after left border).
    /// </summary>
    public int ContentLeft { get; init; }

    /// <summary>
    /// Gets the X coordinate where grid content ends (before right border).
    /// </summary>
    public int ContentRight { get; init; }

    /// <summary>
    /// Gets the total image height.
    /// </summary>
    public int ImageHeight { get; init; }

    /// <summary>
    /// Gets the total image width.
    /// </summary>
    public int ImageWidth { get; init; }

    /// <summary>
    /// Gets the X coordinate where the captcha widget starts within the image.
    /// Equal to 0 unless the input is a larger screenshot with the widget
    /// occupying only a smaller, centered region.
    /// </summary>
    public int WidgetLeft { get; init; }

    /// <summary>
    /// Gets the Y coordinate where the captcha widget starts within the image.
    /// Equal to 0 unless the input is a larger screenshot with the widget
    /// occupying only a smaller, centered region.
    /// </summary>
    public int WidgetTop { get; init; }

    /// <summary>
    /// Gets the X coordinate where the captcha widget ends within the image
    /// (inclusive). Equal to ImageWidth - 1 unless the input is a larger
    /// screenshot with the widget occupying only a smaller, centered region.
    /// </summary>
    public int WidgetRight { get; init; }

    /// <summary>
    /// Gets the Y coordinate where the captcha widget ends within the image
    /// (inclusive). Equal to ImageHeight - 1 unless the input is a larger
    /// screenshot with the widget occupying only a smaller, centered region.
    /// </summary>
    public int WidgetBottom { get; init; }

    /// <summary>
    /// Gets the header height in pixels (above grid content).
    /// </summary>
    public int HeaderHeight { get; init; }

    /// <summary>
    /// Gets the Y coordinate where the footer starts (after last separator).
    /// </summary>
    public int FooterStartY { get; init; }

    /// <summary>
    /// Gets the footer height in pixels (below grid content).
    /// </summary>
    public int FooterHeight { get; init; }

    /// <summary>
    /// Gets the left border width in pixels (left of grid content).
    /// </summary>
    public int LeftBorderWidth { get; init; }

    /// <summary>
    /// Gets the right border width in pixels (right of grid content).
    /// </summary>
    public int RightBorderWidth { get; init; }

    /// <summary>
    /// Gets the cell boundaries as [yStart, yEnd) pairs, excluding separator widths.
    /// Cells do NOT include the separator pixels themselves.
    /// </summary>
    public (int Start, int End)[] GetRowBounds() => GetBounds(Rows, RowSeparators, ContentTop, ContentBottom);

    /// <summary>
    /// Gets the cell boundaries as [xStart, xEnd) pairs, excluding separator widths.
    /// Cells do NOT include the separator pixels themselves.
    /// </summary>
    public (int Start, int End)[] GetColBounds() => GetBounds(Columns, ColSeparators, ContentLeft, ContentRight);

    /// <summary>
    /// Divides [contentStart, contentEnd] into <paramref name="count"/> cells around
    /// the given separators, excluding the separator pixels themselves. Shared by
    /// <see cref="GetRowBounds"/> and <see cref="GetColBounds"/>, which only differ
    /// in which axis (row/column) they operate on.
    /// </summary>
    private static (int Start, int End)[] GetBounds(
        int count, List<SeparatorBand> separators, int contentStart, int contentEnd)
    {
        var bounds = new (int Start, int End)[count];

        if (count == 0) return bounds;

        // Filter separators to only those within the content region.
        // When header/footer or a border is detected, the content bounds may exclude
        // the first/last separators (they're header/footer/border boundaries, not
        // grid separators).
        var activeSeps = separators
            .Where(s => s.End >= contentStart && s.Start <= contentEnd)
            .ToList();

        if (activeSeps.Count == 0)
        {
            bounds[0] = (contentStart, contentEnd);
            return bounds;
        }

        // First cell: content start to just before the first active separator
        bounds[0] = (contentStart, activeSeps[0].Start);

        // Middle cells: between active separators
        for (int i = 1; i < count - 1 && i < activeSeps.Count; i++)
        {
            bounds[i] = (activeSeps[i - 1].End + 1, activeSeps[i].Start);
        }

        // Last cell: from after the last active separator to content end
        bounds[count - 1] = (activeSeps[activeSeps.Count - 1].End + 1, contentEnd);

        return bounds;
    }

    /// <summary>
    /// Initializes a new instance of GridInfo.
    /// </summary>
    public GridInfo(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
    }
}
