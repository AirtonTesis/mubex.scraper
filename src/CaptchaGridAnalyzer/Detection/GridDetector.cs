using CaptchaGridAnalyzer.Exceptions;
using CaptchaGridAnalyzer.Models;
using OpenCvSharp;

namespace CaptchaGridAnalyzer.Detection;

/// <summary>
/// Detects CAPTCHA grid structure using UNIFORMITY-BASED separator detection.
///
/// Key insight: Real grid separators have UNIFORM spacing between them.
/// Header/footer bands and border artifacts break this uniformity.
///
/// Pipeline:
///   1. Detect row separators → determine contentTop/contentBottom (header/footer)
///   2. Detect column bands → determine contentLeft/contentRight (borders)
///   3. Use row spacing to determine cell size (rows work reliably)
///   4. Divide content region equally into NxN cells (all same size)
///
/// This ensures all quadrants are square and identical in size.
/// </summary>
public class GridDetector
{
    private readonly int _minGridSize;
    private readonly int _maxGridSize;

    /// <summary>Gray mean threshold for white separator lines.</summary>
    internal const double WhiteThreshold = 210.0;

    /// <summary>Max separator width in pixels. Separators are narrow (1-12px).</summary>
    internal const int MaxSeparatorWidth = 12;

    /// <summary>Header/footer threshold: region > factor × maxSpacing → header/footer.</summary>
    internal const double HeaderFooterThreshold = 1.1;

    public GridDetector(int minGridSize = 2, int maxGridSize = 10)
    {
        _minGridSize = minGridSize;
        _maxGridSize = maxGridSize;
    }

    public GridInfo DetectGrid(Mat image)
    {
        // ── Step 0: Locate the captcha widget within the full image ──
        // Most inputs are already tightly cropped to the widget, but some are full
        // page screenshots with the widget occupying only a small, centered region
        // surrounded by page background. Everything below assumes the image IS the
        // widget, so detect and crop to that region first (a no-op returning the
        // full image when no such background margin is found) and translate all
        // coordinates back to the original image at the end.
        Rect widgetBounds = DetectWidgetBounds(image);
        using var widgetImage = new Mat(image, widgetBounds).Clone();

        using var gray = ToGray(widgetImage);
        int w = widgetImage.Width, h = widgetImage.Height;

        double[] colMeans = MeanPerColumn(gray);
        double[] rowMeans = MeanPerRow(gray);
        double[] rowSaturation = MeanSaturationPerRow(widgetImage);

        // ── Step 1: Find white bands ──
        var allRowBands = FindWhiteBands(rowMeans);
        var allColBands = FindWhiteBands(colMeans);

        // ── Step 2: Filter row bands to narrow only (row separators are narrow) ──
        // Also drop any band flush against the image edge (start==0 or end==h-1):
        // a real internal grid separator always has actual content on both sides of
        // it, so an edge-touching band is just margin/padding noise, not a
        // separator - keeping it in the candidate pool lets it masquerade as one
        // (e.g. it can pair up with the real header/grid boundary band and win the
        // uniform-subset search below purely by coincidence).
        // Also drop any band immediately followed by a plain (bright, low-texture)
        // stretch: a real internal separator always leads into an actual grid cell,
        // which - being a photo - has meaningfully more row-to-row variation than a
        // UI background. A band that instead leads into a plain stretch is more
        // likely a stray dip inside a footer's background (e.g. an icon glyph
        // briefly darkening a row) than a genuine grid line, and keeping it as a
        // candidate can let the uniform-subset search mistake it for the last real
        // separator - swallowing most of the actual footer into the final "row".
        var narrowRowBands = allRowBands
            .Where(b => (b.end - b.start + 1) <= MaxSeparatorWidth)
            .Where(b => b.start > 0 && b.end < h - 1)
            .Where(b => !LooksLikePlainBackground(rowMeans, b.end + 1, h))
            .ToList();

        // ── Step 3: Find most uniform row subset (columns use ALL bands for border detection) ──
        int minBands = Math.Max(1, _minGridSize - 1);
        var bestRowSubset = FindMostUniformSubset(narrowRowBands, h, minBands);

        // ── Step 4: Detect header/footer using row separators (ROWS ARE RELIABLE) ──
        double maxRowSpacing = ComputeMaxSpacing(bestRowSubset);
        // The smallest gap between consecutive separators is the most trustworthy
        // single-cell-height estimate. Averaging all gaps instead would get skewed
        // by any gap that actually spans several undetected cells (e.g. a bright
        // photo background hiding the grid lines in that stretch), inflating the
        // estimate and undercounting rows.
        double minRowSpacing = ComputeMinSpacing(bestRowSubset);

        // A header/footer band isn't always taller than a normal grid row (e.g. a
        // solid-color instruction banner can be about the same height as a photo
        // cell), so size alone can't distinguish it from a real row. Compare the
        // candidate band against an actual content row from the same image (rather
        // than judging it in isolation, which would false-positive on a uniformly
        // flat/synthetic test image) using two independent signals: a solid-color
        // banner holds a much longer run of (nearly) identical gray-level means than
        // real photo content, AND/OR uses a much more saturated color than real
        // photos typically average out to (even when a small thumbnail is embedded
        // in it, which can defeat the flatness signal alone).
        bool hasRowHeader = bestRowSubset.Count > 0 &&
            (bestRowSubset[0].start > maxRowSpacing * HeaderFooterThreshold ||
             (bestRowSubset.Count >= 2 && IsBannerBand(rowMeans, rowSaturation,
                 0, bestRowSubset[0].start,
                 bestRowSubset[0].end + 1, bestRowSubset[1].start)));

        // Two different reasons can flag the trailing band as a footer, and they
        // imply two different boundaries:
        //  - "size": the gap after the last separator is unusually large, which
        //    usually means it's hiding one more real grid row before the footer
        //    actually starts - extrapolate past that row using minRowSpacing.
        //  - "banner": the candidate segment itself was directly confirmed to look
        //    like a plain/colored UI band (vs. real content) - it already *is* the
        //    footer, so the boundary is right after the last separator with no
        //    extra row to skip past. Applying the size-based extrapolation here
        //    too would overshoot past the image bottom.
        bool hasRowFooterBySize = bestRowSubset.Count > 0 &&
            (h - 1 - bestRowSubset[^1].end) > maxRowSpacing * HeaderFooterThreshold;
        bool hasRowFooterByBanner = bestRowSubset.Count >= 2 && IsBannerBand(rowMeans, rowSaturation,
            bestRowSubset[^1].end + 1, h,
            bestRowSubset[^2].end + 1, bestRowSubset[^1].start);
        bool hasRowFooter = hasRowFooterBySize || hasRowFooterByBanner;

        int contentTop = hasRowHeader ? bestRowSubset[0].end + 1 : 0;
        int headerHeight = hasRowHeader ? bestRowSubset[0].start : 0;
        int contentBottom;
        int footerStartY;
        int footerHeight;

        if (hasRowFooterByBanner)
        {
            contentBottom = bestRowSubset[^1].end;
        }
        else if (hasRowFooterBySize)
        {
            contentBottom = bestRowSubset[^1].end + (int)Math.Round(minRowSpacing);
        }
        else
        {
            contentBottom = h - 1;
        }

        contentBottom = Math.Min(contentBottom, h - 1);
        if (hasRowFooter)
        {
            footerStartY = contentBottom + 1;
            footerHeight = h - footerStartY;
        }
        else
        {
            footerStartY = h;
            footerHeight = 0;
        }

        // ── Step 4b: Cross-check header/footer directly from the saturation profile ──
        // The checks above assume the banner is separated from the grid by a white
        // gap (so the first/last detected separator band marks the boundary). Some
        // CAPTCHA layouts butt the banner directly against the grid with no gap at
        // all, in which case the "first separator" found above is actually a real
        // internal grid line - blindly treating it as the header would swallow one
        // or more real rows. Scanning the saturation profile inward from each edge
        // finds the true banner/content transition regardless of whether a gap
        // exists, so trust it whenever it reports a *tighter* (smaller) band than
        // the separator-based estimate above.
        int scanHeaderHeight = DetectBannerHeight(rowSaturation, fromTop: true, SaturationMinAbsolute);
        if (scanHeaderHeight > 0 && scanHeaderHeight < contentBottom &&
            (!hasRowHeader || scanHeaderHeight < headerHeight))
        {
            headerHeight = scanHeaderHeight;
            contentTop = scanHeaderHeight;
        }

        int scanFooterHeight = DetectBannerHeight(rowSaturation, fromTop: false, SaturationMinAbsolute);
        if (scanFooterHeight > 0 && (h - scanFooterHeight) > contentTop &&
            (!hasRowFooter || scanFooterHeight < footerHeight))
        {
            footerHeight = scanFooterHeight;
            footerStartY = h - scanFooterHeight;
            contentBottom = footerStartY - 1;
        }

        // A footer is very often achromatic (gray icons on white), so neither the
        // separator math above nor the saturation scan can reliably find it - and a
        // stray narrow band deep inside it (an icon glyph dip) can trick the
        // separator math into thinking hardly any footer remains at all. Directly
        // grow a window in from the bottom edge for as long as it keeps looking like
        // a plain (bright, low-variance) background; real grid content - being a
        // photo - reliably breaks that the moment it's included, giving a precise
        // boundary regardless of what the separator search concluded. This is a
        // stronger, more direct signal than either check above, so it wins when it
        // fires - UNLESS it comes back suspiciously close to (or bigger than) a full
        // grid cell, which real UI footers never are (they're consistently well
        // under a cell's height in every sample seen) - that's a sign it swallowed a
        // real, visually plain photo row (a road, a wall, a floor) instead.
        int scanFooterHeightPlain = DetectPlainFooterHeight(rowMeans);
        bool scanFooterPlausible = minRowSpacing <= 0 || scanFooterHeightPlain < minRowSpacing * FooterPlainMaxCellFraction;
        if (scanFooterHeightPlain > 0 && scanFooterPlausible && (h - scanFooterHeightPlain) > contentTop)
        {
            footerHeight = scanFooterHeightPlain;
            footerStartY = h - scanFooterHeightPlain;
            contentBottom = footerStartY - 1;
        }

        // ── Step 5: Detect left border using ALL column bands ──
        // Use ALL bands (including wide ones) for border detection.
        // Borders are white areas at the image edges.
        int contentLeft = 0;
        int leftBorderWidth = 0;

        if (allColBands.Count > 0)
        {
            // Left border: first band starts at or near position 0
            var firstColBand = allColBands[0];
            if (firstColBand.start <= 2) // Band touches or nearly touches left edge
            {
                contentLeft = firstColBand.end + 1;
                leftBorderWidth = firstColBand.end + 1;
            }
        }

        // ── Step 6: Compute grid dimensions from ROW data (reliable) ──
        // Content height from row detection
        int contentH = contentBottom - contentTop + 1;

        // Cell size from row spacing (rows are reliable)
        int cellSize = (int)Math.Round(minRowSpacing);

        int gridRows;
        if (cellSize > 0)
        {
            // Grid rows from content height / cell size
            gridRows = Math.Max(_minGridSize, (int)Math.Round((double)contentH / cellSize));
            gridRows = Math.Min(gridRows, _maxGridSize);
        }
        else
        {
            // No reliable spacing signal (e.g. blank/uniform image with no detected
            // separators) - minRowSpacing is 0, so fall back to the minimum grid size
            // and derive cellSize from it instead of dividing by zero.
            gridRows = _minGridSize;
            cellSize = Math.Max(1, contentH / gridRows);
        }

        // NxN: cols = rows (CAPTCHA grids are always square)
        int gridCols = gridRows;

        // Compute contentW from grid dimensions and cell size
        // This ensures square cells regardless of column detection
        int contentW = gridCols * cellSize;

        // contentRight derived from contentLeft + contentW (reliable row data)
        int contentRight = contentLeft + contentW - 1;
        int rightBorderWidth = Math.Max(0, w - contentRight - 1);
        contentRight = Math.Min(contentRight, w - 1);

        // ── Step 7: Generate EQUAL separators within content region ──
        // This ensures all cells are the same size (square)
        int rowSepCount = gridRows - 1;
        int colSepCount = gridCols - 1;

        var gridRowSeps = GenerateEqualSeparators(contentTop, contentBottom, rowSepCount);
        var gridColSeps = GenerateEqualSeparators(contentLeft, contentRight, colSepCount);

        // ── Step 8: Validate and return ──
        ValidateGridStructure(gridRows, gridCols);

        // Everything above was computed relative to widgetImage (Step 0's crop) -
        // translate positions back into the original image's coordinate space,
        // since that's what QuadrantExtractor extracts pixels from.
        int offsetX = widgetBounds.X;
        int offsetY = widgetBounds.Y;

        return new GridInfo(gridRows, gridCols)
        {
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            WidgetLeft = offsetX,
            WidgetTop = offsetY,
            WidgetRight = offsetX + w - 1,
            WidgetBottom = offsetY + h - 1,
            ContentLeft = contentLeft + offsetX,
            ContentRight = contentRight + offsetX,
            ContentTop = contentTop + offsetY,
            ContentBottom = contentBottom + offsetY,
            HeaderHeight = headerHeight,
            FooterStartY = footerStartY + offsetY,
            FooterHeight = footerHeight,
            LeftBorderWidth = leftBorderWidth,
            RightBorderWidth = rightBorderWidth,
            ColSeparators = gridColSeps.Select(b => new SeparatorBand(b.start + offsetX, b.end + offsetX)).ToList(),
            RowSeparators = gridRowSeps.Select(b => new SeparatorBand(b.start + offsetY, b.end + offsetY)).ToList(),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Widget localization (finds the captcha within a larger screenshot, if any)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>How many consecutive rows/cols a page-background margin must hold before it counts as real (vs. a text glyph or JPEG-noise blip).</summary>
    internal const int BackgroundMarginSustainRun = 15;

    /// <summary>How much variation is allowed within the edge sample itself before it's rejected as not a stable, uniform background to begin with.</summary>
    internal const double BackgroundStabilityTolerance = 12.0;

    /// <summary>
    /// How far a row/col mean may drift from the edge reference and still count as
    /// "the same background". Deliberately looser than the stability tolerance
    /// above: the widget's own white/near-white elements (card padding, a mostly-
    /// white footer with small icon glyphs) can sit within ~14 units of a light
    /// page background, and must NOT be mistaken for real content - only an
    /// actual photo (which differs by 30+ units) should count as a departure.
    /// </summary>
    internal const double BackgroundDepartureTolerance = 20.0;

    /// <summary>A page background is neutral; a colored banner that happens to be locally flat in grayscale is not - require the edge sample to also be near-grayscale.</summary>
    internal const double BackgroundMarginMaxSaturation = 0.15;

    /// <summary>A margin must cover at least this fraction of the image's width/height (summed over both opposite sides) before it's trusted as "the widget is smaller than the image" rather than the thin margins/borders already handled elsewhere.</summary>
    internal const double WidgetMarginMinFraction = 0.08;

    /// <summary>
    /// Detects the captcha widget's bounding box within <paramref name="image"/>.
    /// Returns the full image bounds (a no-op) unless a substantial, neutral
    /// (non-colored) background margin is found on both sides of an axis - the
    /// signature of a full-page screenshot with the widget occupying only a
    /// smaller, centered region, as opposed to an already-tightly-cropped input
    /// (which may still have a thin margin of its own, handled by the header/
    /// footer/border logic elsewhere and deliberately left untouched here).
    /// </summary>
    internal static Rect DetectWidgetBounds(Mat image)
    {
        int w = image.Width, h = image.Height;
        using var gray = ToGray(image);
        double[] colMeans = MeanPerColumn(gray);
        double[] rowMeans = MeanPerRow(gray);
        double[] colSaturation = MeanSaturationPerColumn(image);
        double[] rowSaturation = MeanSaturationPerRow(image);

        int left = DetectBackgroundMargin(colMeans, colSaturation, fromStart: true);
        int right = DetectBackgroundMargin(colMeans, colSaturation, fromStart: false);
        int top = DetectBackgroundMargin(rowMeans, rowSaturation, fromStart: true);
        int bottom = DetectBackgroundMargin(rowMeans, rowSaturation, fromStart: false);

        bool cropWidth = (left + right) >= w * WidgetMarginMinFraction;
        bool cropHeight = (top + bottom) >= h * WidgetMarginMinFraction;

        int x = cropWidth ? left : 0;
        int y = cropHeight ? top : 0;
        int width = w - x - (cropWidth ? right : 0);
        int height = h - y - (cropHeight ? bottom : 0);

        if (width <= 0 || height <= 0) return new Rect(0, 0, w, h);
        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// Scans inward from the start (or end) of <paramref name="means"/> for how
    /// far a stable, neutral background swatch extends before a sustained
    /// departure from it begins. Returns 0 if the edge itself isn't a stable,
    /// near-grayscale swatch to begin with (e.g. real content starts immediately,
    /// or the "edge" is actually a colored banner that happens to be flat).
    /// </summary>
    internal static int DetectBackgroundMargin(double[] means, double[] saturation, bool fromStart)
    {
        double[] orientedMeans = fromStart ? means : means.Reverse().ToArray();
        double[] orientedSat = fromStart ? saturation : saturation.Reverse().ToArray();

        int n = orientedMeans.Length;
        int sustain = BackgroundMarginSustainRun;
        if (n < sustain * 2) return 0;

        if (StdDev(orientedMeans, 0, sustain) > BackgroundStabilityTolerance) return 0;
        if (Mean(orientedSat, 0, sustain) > BackgroundMarginMaxSaturation) return 0;
        double reference = Mean(orientedMeans, 0, sustain);

        double[] smoothed = MovingAverage(orientedMeans, 5);
        for (int i = sustain; i <= n - sustain; i++)
        {
            if (AllOutsideTolerance(smoothed, i, i + sustain, reference, BackgroundDepartureTolerance)) return i;
        }
        return 0;
    }

    internal static bool AllOutsideTolerance(double[] values, int start, int endExclusive, double reference, double tolerance) =>
        AllSatisfy(values, start, endExclusive, v => Math.Abs(v - reference) > tolerance);

    // ─────────────────────────────────────────────────────────────────────────
    // Equal separator generation (ensures all cells are same size)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates N equally-spaced separators within [contentStart, contentEnd].
    /// Each separator is 1 pixel wide (thin white line).
    /// This ensures all cells between separators have identical dimensions.
    /// </summary>
    internal static List<(int start, int end)> GenerateEqualSeparators(int contentStart, int contentEnd, int count)
    {
        var result = new List<(int, int)>();
        if (count <= 0) return result;

        int span = contentEnd - contentStart + 1;
        for (int i = 1; i <= count; i++)
        {
            int pos = contentStart + (int)Math.Round((double)i / (count + 1) * span);
            // Clamp to valid range
            pos = Math.Max(contentStart + 1, Math.Min(pos, contentEnd - 1));
            result.Add((pos, pos)); // 1-pixel separator
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Signal computation
    // ─────────────────────────────────────────────────────────────────────────

    internal static double[] MeanPerColumn(Mat gray) => MeanAlongAxis(gray, alongColumns: true);

    internal static double[] MeanPerRow(Mat gray) => MeanAlongAxis(gray, alongColumns: false);

    /// <summary>Average grayscale value per column (or per row), shared by <see cref="MeanPerColumn"/>/<see cref="MeanPerRow"/> which only differ in which axis is scanned.</summary>
    internal static double[] MeanAlongAxis(Mat gray, bool alongColumns)
    {
        int w = gray.Width, h = gray.Height;
        int outerLen = alongColumns ? w : h;
        int innerLen = alongColumns ? h : w;

        var sig = new double[outerLen];
        for (int outer = 0; outer < outerLen; outer++)
        {
            double s = 0;
            for (int inner = 0; inner < innerLen; inner++)
                s += alongColumns ? gray.At<byte>(inner, outer) : gray.At<byte>(outer, inner);
            sig[outer] = s / innerLen;
        }
        return sig;
    }

    /// <summary>Average HSV saturation per row, 0 (grayscale) to 1 (fully saturated). Zero for non-color images.</summary>
    internal static double[] MeanSaturationPerRow(Mat image) => MeanSaturationAlongAxis(image, alongColumns: false);

    /// <summary>Average HSV saturation per column, 0 (grayscale) to 1 (fully saturated). Zero for non-color images.</summary>
    internal static double[] MeanSaturationPerColumn(Mat image) => MeanSaturationAlongAxis(image, alongColumns: true);

    /// <summary>Shared by <see cref="MeanSaturationPerRow"/>/<see cref="MeanSaturationPerColumn"/>, which only differ in which axis is scanned.</summary>
    internal static double[] MeanSaturationAlongAxis(Mat image, bool alongColumns)
    {
        int w = image.Width, h = image.Height;
        int outerLen = alongColumns ? w : h;
        int innerLen = alongColumns ? h : w;

        var sat = new double[outerLen];
        int channels = image.Channels();
        if (channels != 3 && channels != 4) return sat;

        for (int outer = 0; outer < outerLen; outer++)
        {
            double s = 0;
            for (int inner = 0; inner < innerLen; inner++)
            {
                int x = alongColumns ? outer : inner;
                int y = alongColumns ? inner : outer;

                int b, g, r;
                if (channels == 4)
                {
                    var px = image.At<Vec4b>(y, x);
                    b = px.Item0; g = px.Item1; r = px.Item2;
                }
                else
                {
                    var px = image.At<Vec3b>(y, x);
                    b = px.Item0; g = px.Item1; r = px.Item2;
                }
                int max = Math.Max(b, Math.Max(g, r));
                int min = Math.Min(b, Math.Min(g, r));
                s += max == 0 ? 0 : (double)(max - min) / max;
            }
            sat[outer] = s / innerLen;
        }
        return sat;
    }

    /// <summary>Minimum flat-run fraction for a band to even be considered a plateau.</summary>
    internal const double PlateauMinFraction = 0.15;

    /// <summary>How much flatter than a real content row a band must be to count as a banner.</summary>
    internal const double PlateauRelativeFactor = 1.8;

    /// <summary>Minimum average saturation for a band to even be considered a colored banner.</summary>
    internal const double SaturationMinAbsolute = 0.35;

    /// <summary>How much more saturated than a real content row a band must be to count as a banner.</summary>
    internal const double SaturationRelativeFactor = 1.8;

    /// <summary>
    /// True if [candidateStart, candidateEnd) looks like a solid-color UI banner
    /// rather than real grid content, judged against an actual content row from the
    /// same image (rather than an absolute threshold, which would false-positive on
    /// synthetic/low-detail images where every cell looks similarly plain).
    /// </summary>
    internal static bool IsBannerBand(double[] rowMeans, double[] rowSaturation,
        int candidateStart, int candidateEnd, int baselineStart, int baselineEnd)
    {
        return IsFlatBanner(rowMeans, candidateStart, candidateEnd, baselineStart, baselineEnd)
            || IsSaturatedBanner(rowSaturation, candidateStart, candidateEnd, baselineStart, baselineEnd)
            || IsLowVarianceBand(rowMeans, candidateStart, candidateEnd, baselineStart, baselineEnd);
    }

    /// <summary>
    /// True if [candidateStart, candidateEnd) holds a much longer run of (nearly)
    /// identical consecutive means than the baseline content row does.
    /// </summary>
    internal static bool IsFlatBanner(double[] means, int candidateStart, int candidateEnd,
        int baselineStart, int baselineEnd)
    {
        double candidateFraction = MaxRunFraction(means, candidateStart, candidateEnd);
        if (candidateFraction < PlateauMinFraction) return false;
        if (baselineEnd <= baselineStart) return false;

        double baselineFraction = MaxRunFraction(means, baselineStart, baselineEnd);
        // If the baseline "real content" row is itself already fairly flat (e.g. a
        // low-detail/low-texture image where every cell looks plain), the relative
        // comparison below isn't meaningful evidence of a banner - it just means
        // this row happened to be even flatter than an already-flat one.
        if (baselineFraction > PlateauMinFraction) return false;

        return candidateFraction > baselineFraction * PlateauRelativeFactor;
    }

    /// <summary>
    /// True if [candidateStart, candidateEnd) averages a much more saturated color
    /// than the baseline content row - true even when part of the band (e.g. a small
    /// embedded example thumbnail) breaks up the flat-run signal above.
    /// </summary>
    internal static bool IsSaturatedBanner(double[] rowSaturation, int candidateStart, int candidateEnd,
        int baselineStart, int baselineEnd)
    {
        if (candidateEnd <= candidateStart || baselineEnd <= baselineStart) return false;

        double candidateSaturation = Mean(rowSaturation, candidateStart, candidateEnd);
        if (candidateSaturation < SaturationMinAbsolute) return false;

        double baselineSaturation = Mean(rowSaturation, baselineStart, baselineEnd);
        return candidateSaturation > baselineSaturation * SaturationRelativeFactor;
    }

    /// <summary>Real UI footers seen so far are 52-76% of a full cell's height; cap plausibility comfortably above that so a full swallowed row (~100%) is rejected.</summary>
    internal const double FooterPlainMaxCellFraction = 0.85;

    /// <summary>Minimum length for a candidate band to be judged on variance (too short is unreliable).</summary>
    internal const int LowVarianceMinLength = 5;

    /// <summary>Candidate must be at most this fraction of the baseline's row-to-row variability to count as a plain background.</summary>
    internal const double LowVarianceRelativeFactor = 0.6;

    /// <summary>
    /// A single baseline row's variance is noisy on its own (e.g. a low-detail/flat
    /// test image can have one content row that's coincidentally very uniform too),
    /// so also require the candidate to be bright - real UI footers/headers are
    /// white or a bold color, unlike (say) a dark, flat photo background.
    /// </summary>
    internal const double LowVarianceMinBrightness = 150.0;

    /// <summary>
    /// True if [candidateStart, candidateEnd) is bright AND much more uniform
    /// row-to-row than the baseline content row, even though neither its color nor
    /// its exact-repeat run length gives it away (e.g. a plain white/gray UI footer
    /// whose brightness dips slightly at icon glyphs - never flat, never colorful,
    /// but still far steadier than real photo content like a floor or wall).
    /// </summary>
    internal static bool IsLowVarianceBand(double[] rowMeans, int candidateStart, int candidateEnd,
        int baselineStart, int baselineEnd)
    {
        if (candidateEnd - candidateStart < LowVarianceMinLength) return false;
        if (baselineEnd <= baselineStart) return false;

        if (Mean(rowMeans, candidateStart, candidateEnd) < LowVarianceMinBrightness) return false;

        double baselineStdDev = StdDev(rowMeans, baselineStart, baselineEnd);
        if (baselineStdDev <= 0) return false;

        double candidateStdDev = StdDev(rowMeans, candidateStart, candidateEnd);
        return candidateStdDev < baselineStdDev * LowVarianceRelativeFactor;
    }

    internal static double StdDev(double[] values, int start, int endExclusive)
    {
        double mean = Mean(values, start, endExclusive);
        double sumSquares = 0;
        for (int i = start; i < endExclusive; i++)
        {
            double diff = values[i] - mean;
            sumSquares += diff * diff;
        }
        return Math.Sqrt(sumSquares / (endExclusive - start));
    }

    /// <summary>Below this row-to-row variability, a bright stretch reads as a plain UI background rather than real photo content.</summary>
    internal const double ContentMinStdDev = 25.0;

    /// <summary>
    /// True if [start, endExclusive) is bright and low-variance throughout - the
    /// signature of a plain white/gray UI background (footer, panel) rather than
    /// real grid content, which - being a photo - always has meaningfully more
    /// row-to-row variation even when it's bright (e.g. a sunlit wall or floor).
    /// </summary>
    internal static bool LooksLikePlainBackground(double[] rowMeans, int start, int endExclusive)
    {
        if (endExclusive - start < LowVarianceMinLength) return false;
        if (Mean(rowMeans, start, endExclusive) < BrightnessMinAbsolute) return false;
        return StdDev(rowMeans, start, endExclusive) < ContentMinStdDev;
    }

    /// <summary>
    /// Grows a window in from the bottom edge, one row at a time, for as long as it
    /// keeps looking like a plain background - i.e. finds the largest bottom suffix
    /// of the image that still reads as a single, uniform, bright band. Real grid
    /// content pulls the running mean/variance away from "plain" the moment enough
    /// of it is included, so this naturally stops right at the content/footer
    /// boundary without needing separator positions at all. Returns 0 if even the
    /// smallest window at the very edge doesn't look like a plain background.
    /// </summary>
    internal static int DetectPlainFooterHeight(double[] rowMeans)
    {
        int h = rowMeans.Length;
        if (h < LowVarianceMinLength) return 0;
        if (!LooksLikePlainBackground(rowMeans, h - LowVarianceMinLength, h)) return 0;

        int y = h - LowVarianceMinLength;
        while (y > 0 && LooksLikePlainBackground(rowMeans, y - 1, h))
        {
            y--;
        }
        return h - y;
    }

    /// <summary>How many consecutive rows a saturation drop must hold for before it counts as leaving the banner (vs. a text glyph blip).</summary>
    internal const int BannerSustainRows = 10;

    /// <summary>A drop below this fraction of the banner's own reference saturation signals real content has begun.</summary>
    internal const double BannerDropFactor = 0.5;

    /// <summary>Minimum average brightness for a band to even be considered a plain white/gray UI panel.</summary>
    internal const double BrightnessMinAbsolute = 180.0;

    /// <summary>How far in from the edge to look for the banner's reference window, in case a thin plain margin precedes it.</summary>
    internal const int MaxMarginSkip = 50;

    /// <summary>
    /// Scans inward from the top (or bottom) edge of the image looking for where a
    /// banner (colored, per <paramref name="signal"/> being row saturation, or plain
    /// white/gray, per it being row brightness) ends and real content begins, using
    /// the signal directly rather than separator positions. Unlike the
    /// separator-based checks above, this also works when the banner butts directly
    /// against the grid with no white gap between them. Returns 0 if the edge
    /// doesn't clear <paramref name="minAbsoluteReference"/> at all.
    /// </summary>
    internal static int DetectBannerHeight(double[] signal, bool fromTop, double minAbsoluteReference)
    {
        int h = signal.Length;
        int refCount = Math.Min(10, h);
        if (refCount == 0) return 0;

        // A thin plain margin (e.g. white padding) can precede the banner itself;
        // taking the reference from the raw edge would then dilute it below
        // minAbsoluteReference and make this scan silently never fire. Slide the
        // reference window inward until it clears the threshold, or give up within
        // a small budget (a real margin is only a few pixels, never dozens).
        int maxSkip = Math.Min(MaxMarginSkip, h - refCount);
        int marginSkip = -1;
        double reference = 0;
        for (int offset = 0; offset <= maxSkip; offset++)
        {
            int start = fromTop ? offset : h - refCount - offset;
            double candidate = Mean(signal, start, start + refCount);
            if (candidate >= minAbsoluteReference)
            {
                marginSkip = offset;
                reference = candidate;
                break;
            }
        }
        if (marginSkip < 0) return 0;

        double[] smoothed = MovingAverage(signal, 5);
        double threshold = reference * BannerDropFactor;
        int sustainRows = Math.Min(BannerSustainRows, h / 4);
        if (sustainRows <= 0) return 0;

        if (fromTop)
        {
            for (int y = marginSkip; y <= h - sustainRows; y++)
            {
                if (AllBelow(smoothed, y, y + sustainRows, threshold)) return y;
            }
        }
        else
        {
            for (int y = h - sustainRows - marginSkip; y >= 0; y--)
            {
                if (AllBelow(smoothed, y, y + sustainRows, threshold)) return h - y;
            }
        }

        return 0;
    }

    internal static bool AllBelow(double[] values, int start, int endExclusive, double threshold) =>
        AllSatisfy(values, start, endExclusive, v => v < threshold);

    /// <summary>True if every value in [start, endExclusive) satisfies <paramref name="predicate"/>. Shared by <see cref="AllOutsideTolerance"/>/<see cref="AllBelow"/>.</summary>
    internal static bool AllSatisfy(double[] values, int start, int endExclusive, Func<double, bool> predicate)
    {
        for (int i = start; i < endExclusive; i++)
            if (!predicate(values[i])) return false;
        return true;
    }

    /// <summary>Centered moving average, used to smooth out single-row noise (e.g. text glyphs) before scanning for a sustained trend change.</summary>
    internal static double[] MovingAverage(double[] values, int window)
    {
        int n = values.Length;
        var result = new double[n];
        int half = window / 2;
        for (int i = 0; i < n; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(n, i + half + 1);
            double sum = 0;
            for (int j = start; j < end; j++) sum += values[j];
            result[i] = sum / (end - start);
        }
        return result;
    }

    /// <summary>Longest run of consecutive (nearly) identical values within [start, endExclusive), as a fraction of its length.</summary>
    internal static double MaxRunFraction(double[] means, int start, int endExclusive, double tolerance = 0.5)
    {
        int length = endExclusive - start;
        if (length <= 1) return 0;

        int maxRun = 1, currentRun = 1;
        for (int i = start + 1; i < endExclusive; i++)
        {
            if (Math.Abs(means[i] - means[i - 1]) <= tolerance)
            {
                currentRun++;
                if (currentRun > maxRun) maxRun = currentRun;
            }
            else
            {
                currentRun = 1;
            }
        }

        return (double)maxRun / length;
    }

    internal static double Mean(double[] values, int start, int endExclusive)
    {
        double sum = 0;
        for (int i = start; i < endExclusive; i++) sum += values[i];
        return sum / (endExclusive - start);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // White band detection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Momentary dips below the white threshold this short (JPEG noise, anti-aliasing) don't split a band.</summary>
    internal const int WhiteBandGapTolerance = 2;

    internal static List<(int start, int end)> FindWhiteBands(double[] sig)
    {
        var bands = new List<(int start, int end)>();
        int bandStart = -1;
        int lastWhiteIndex = -1;
        int gapRun = 0;

        for (int i = 0; i < sig.Length; i++)
        {
            if (sig[i] >= WhiteThreshold)
            {
                if (bandStart < 0) bandStart = i;
                lastWhiteIndex = i;
                gapRun = 0;
            }
            else if (bandStart >= 0)
            {
                gapRun++;
                if (gapRun > WhiteBandGapTolerance)
                {
                    bands.Add((bandStart, lastWhiteIndex));
                    bandStart = -1;
                    gapRun = 0;
                }
            }
        }
        if (bandStart >= 0)
            bands.Add((bandStart, lastWhiteIndex));

        return bands;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Uniformity-based detection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spacing uniformity (coefficient of variation) is only a meaningful signal
    /// with at least 2 gaps, i.e. 3 bands. A 2-band subset has exactly one gap,
    /// whose "variance" is trivially zero - a vacuous "perfectly uniform" result
    /// that would otherwise always out-compete real, larger, genuinely uniform
    /// subsets in the search below.
    /// </summary>
    internal const int MinBandsForUniformity = 3;

    /// <summary>
    /// A real grid cell is never this small in practice. Content that hovers right
    /// at the white threshold (e.g. a bright wall with faint shadows) can fragment
    /// into a cluster of noise bands only a handful of pixels apart, which - having
    /// almost identical tiny gaps - looks spuriously "perfectly uniform" and would
    /// otherwise win over the real, more widely-spaced separators.
    /// </summary>
    internal const double MinPlausibleSpacing = 15.0;

    internal static List<(int start, int end)> FindMostUniformSubset(
        List<(int start, int end)> bands, int length, int minBands)
    {
        if (bands.Count == 0) return new List<(int, int)>();
        if (bands.Count <= minBands) return bands;

        int effectiveMinBands = Math.Max(minBands, MinBandsForUniformity);
        if (bands.Count < effectiveMinBands) return bands;

        List<(int, int)>? bestSubset = null;
        double bestCV = double.MaxValue;

        for (int start = 0; start <= bands.Count - effectiveMinBands; start++)
        {
            for (int end = start + effectiveMinBands - 1; end < bands.Count; end++)
            {
                var subset = bands.GetRange(start, end - start + 1);
                if (ComputeMinSpacing(subset) < MinPlausibleSpacing) continue;

                double cv = ComputeSpacingCV(subset);

                if (cv < bestCV - 0.01 || (bestSubset != null && Math.Abs(cv - bestCV) <= 0.01 && subset.Count > bestSubset.Count))
                {
                    bestCV = cv;
                    bestSubset = subset;
                }
            }
        }

        return bestSubset ?? bands;
    }

    /// <summary>Gaps between each consecutive pair of bands, shared by <see cref="ComputeSpacingCV"/>/<see cref="ComputeMaxSpacing"/>/<see cref="ComputeMinSpacing"/>.</summary>
    internal static List<double> ComputeSpacings(List<(int start, int end)> bands)
    {
        var spacings = new List<double>();
        for (int i = 1; i < bands.Count; i++)
            spacings.Add(bands[i].start - bands[i - 1].end);
        return spacings;
    }

    internal static double ComputeSpacingCV(List<(int start, int end)> bands)
    {
        if (bands.Count < 2) return 0;

        var spacings = ComputeSpacings(bands);
        double mean = spacings.Average();
        if (mean <= 0) return double.MaxValue;

        double variance = spacings.Sum(s => Math.Pow(s - mean, 2)) / spacings.Count;
        return Math.Sqrt(variance) / mean;
    }

    internal static double ComputeMaxSpacing(List<(int start, int end)> bands) =>
        bands.Count < 2 ? 0 : ComputeSpacings(bands).Max();

    internal static double ComputeMinSpacing(List<(int start, int end)> bands) =>
        bands.Count < 2 ? 0 : ComputeSpacings(bands).Min();

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    internal static Mat ToGray(Mat image)
    {
        var gray = new Mat();
        int ch = image.Channels();
        if (ch == 1) image.CopyTo(gray);
        else if (ch == 4) Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
        else Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    internal void ValidateGridStructure(int rows, int cols)
    {
        if (rows < _minGridSize || cols < _minGridSize)
            throw new GridNotDetectedException(
                $"Detected {rows}x{cols} below minimum {_minGridSize}x{_minGridSize}");
        if (rows > _maxGridSize || cols > _maxGridSize)
            throw new InvalidGridStructureException(rows, cols);
    }
}
