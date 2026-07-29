using OpenCvSharp;
using System.Diagnostics.CodeAnalysis;

namespace CaptchaGridAnalyzer.Diagnostics;

/// <summary>
/// Diagnostic tool: saves debug images showing what the grid detector sees.
/// Excluded from coverage: a manual, human-in-the-loop debugging aid (dumps
/// charts/text for a developer to look at via the CLI's --diag flag), not part
/// of the analysis pipeline's own behavior.
/// </summary>
[ExcludeFromCodeCoverage]
public static class GridDiagnostic
{
    public static void Run(string imagePath, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (src.Empty()) throw new Exception($"Cannot load: {imagePath}");

        string name = Path.GetFileNameWithoutExtension(imagePath);

        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.ImWrite(Path.Combine(outputDir, $"{name}_1_gray.png"), gray);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        Cv2.ImWrite(Path.Combine(outputDir, $"{name}_2_binary.png"), binary);

        SaveProjectionImage(binary, Path.Combine(outputDir, $"{name}_3_col_projection.png"), isVertical: true);
        SaveProjectionImage(binary, Path.Combine(outputDir, $"{name}_4_row_projection.png"), isVertical: false);

        using var enhanced = EnhanceLines(binary);
        Cv2.ImWrite(Path.Combine(outputDir, $"{name}_5_enhanced.png"), enhanced);

        SaveSumData(binary, Path.Combine(outputDir, $"{name}_sums.txt"));

        // Save grayscale means (what GridDetector uses)
        SaveGrayscaleMeans(gray, Path.Combine(outputDir, $"{name}_gray_means.txt"));

        Console.WriteLine($"Diagnostic images saved to: {outputDir}");
        Console.WriteLine($"  {name}_1_gray.png        - grayscale");
        Console.WriteLine($"  {name}_2_binary.png      - binarized (Otsu)");
        Console.WriteLine($"  {name}_3_col_projection  - column sums (vertical lines)");
        Console.WriteLine($"  {name}_4_row_projection  - row sums (horizontal lines)");
        Console.WriteLine($"  {name}_5_enhanced.png    - morphological enhancement");
        Console.WriteLine($"  {name}_sums.txt          - binary sum data");
        Console.WriteLine($"  {name}_gray_means.txt    - grayscale mean per row/col (used by detector)");
    }

    private static void SaveGrayscaleMeans(Mat gray, string path)
    {
        int w = gray.Width, h = gray.Height;

        var colMeans = new double[w];
        for (int x = 0; x < w; x++)
        {
            double s = 0;
            for (int y = 0; y < h; y++) s += gray.At<byte>(y, x);
            colMeans[x] = s / h;
        }

        var rowMeans = new double[h];
        for (int y = 0; y < h; y++)
        {
            double s = 0;
            for (int x = 0; x < w; x++) s += gray.At<byte>(y, x);
            rowMeans[y] = s / w;
        }

        using var sw = new StreamWriter(path);
        sw.WriteLine($"Image: {w}x{h}");
        sw.WriteLine($"ColMean: max={colMeans.Max():F1} min={colMeans.Min():F1} mean={colMeans.Average():F1}");
        sw.WriteLine($"RowMean: max={rowMeans.Max():F1} min={rowMeans.Min():F1} mean={rowMeans.Average():F1}");
        sw.WriteLine();
        sw.WriteLine("=== GRAYSCALE COL MEANS (white separator = high value ~250+) ===");
        for (int x = 0; x < w; x++)
            sw.WriteLine($"col {x,4}: {colMeans[x]:F1}");
        sw.WriteLine();
        sw.WriteLine("=== GRAYSCALE ROW MEANS (white separator = high value ~250+) ===");
        for (int y = 0; y < h; y++)
            sw.WriteLine($"row {y,4}: {rowMeans[y]:F1}");
    }

    private static int[] ComputeColumnSums(Mat binary)
    {
        int w = binary.Width, h = binary.Height;
        var sums = new int[w];
        for (int col = 0; col < w; col++)
            for (int row = 0; row < h; row++)
                if (binary.At<byte>(row, col) > 0) sums[col]++;
        return sums;
    }

    private static int[] ComputeRowSums(Mat binary)
    {
        int w = binary.Width, h = binary.Height;
        var sums = new int[h];
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
                if (binary.At<byte>(row, col) > 0) sums[row]++;
        return sums;
    }

    private static void SaveProjectionImage(Mat binary, string path, bool isVertical)
    {
        int w = binary.Width, h = binary.Height;
        int perpLen = isVertical ? h : w;
        int[] sums = isVertical ? ComputeColumnSums(binary) : ComputeRowSums(binary);
        int length = sums.Length;

        int maxVal = sums.Max() > 0 ? sums.Max() : 1;
        int chartH = 200;
        using var chart = new Mat(chartH + 20, length, MatType.CV_8UC3, Scalar.White);

        // Draw bars
        for (int i = 0; i < length; i++)
        {
            int barH = (int)((double)sums[i] / maxVal * chartH);
            Cv2.Line(chart, new Point(i, chartH), new Point(i, chartH - barH), Scalar.Blue);
        }

        // Red line: fixed threshold 40%
        double t40 = perpLen * 0.4;
        int yRed = chartH - (int)(t40 / maxVal * chartH);
        Cv2.Line(chart, new Point(0, yRed), new Point(length - 1, yRed), Scalar.Red, 1);

        // Green line: adaptive threshold (mean + stddev)
        double mean = sums.Average();
        double variance = sums.Select(v => Math.Pow(v - mean, 2)).Average();
        double std = Math.Sqrt(variance);
        double adaptiveThr = Math.Max(t40, mean + std);
        int yGreen = chartH - (int)(adaptiveThr / maxVal * chartH);
        Cv2.Line(chart, new Point(0, yGreen), new Point(length - 1, yGreen), Scalar.Green, 1);

        Cv2.ImWrite(path, chart);
    }

    private static Mat EnhanceLines(Mat binary)
    {
        var enhanced = new Mat();
        var hKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(binary.Width / 30, 1));
        var vKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, binary.Height / 30));
        using var hLines = new Mat();
        using var vLines = new Mat();
        Cv2.MorphologyEx(binary, hLines, MorphTypes.Open, hKernel);
        Cv2.MorphologyEx(binary, vLines, MorphTypes.Open, vKernel);
        Cv2.Add(hLines, vLines, enhanced);
        hKernel.Dispose();
        vKernel.Dispose();
        return enhanced;
    }

    private static void SaveSumData(Mat binary, string path)
    {
        int w = binary.Width, h = binary.Height;
        int[] colSums = ComputeColumnSums(binary);
        int[] rowSums = ComputeRowSums(binary);

        double colMean = colSums.Average();
        double colStd = Math.Sqrt(colSums.Select(v => Math.Pow(v - colMean, 2)).Average());
        double rowMean = rowSums.Average();
        double rowStd = Math.Sqrt(rowSums.Select(v => Math.Pow(v - rowMean, 2)).Average());

        using var sw = new StreamWriter(path);
        sw.WriteLine($"Image: {w}x{h}");
        sw.WriteLine($"ColSums (binary): max={colSums.Max()} mean={colMean:F1} std={colStd:F1}  threshold_40%={h * 0.4:F0}  adaptive={Math.Max(h * 0.4, colMean + colStd):F0}");
        sw.WriteLine($"RowSums (binary): max={rowSums.Max()} mean={rowMean:F1} std={rowStd:F1}  threshold_40%={w * 0.4:F0}  adaptive={Math.Max(w * 0.4, rowMean + rowStd):F0}");
        sw.WriteLine();

        // Also compute grayscale means (what GridDetector uses)
        // binary here is binarized-inverted; we need gray means
        // Note: binary sums = 0 means white row in original (mean ~255 in gray)
        sw.WriteLine("=== GRAYSCALE MEANS (estimated from binary inverse) ===");
        sw.WriteLine("Note: gray_mean ≈ 255 - (binary_sum / height * 255) for cols");
        sw.WriteLine("  cols with binary_sum=0 → gray_mean≈255 (pure white = separator)");
        sw.WriteLine();

        sw.WriteLine("=== COLUMN SUMS binary-inverted (x: sum) - sum=0 means white separator ===");
        for (int col = 0; col < w; col++)
            sw.WriteLine($"col {col,4}: {colSums[col],6}");
        sw.WriteLine();
        sw.WriteLine("=== ROW SUMS binary-inverted (y: sum) - sum=0 means white separator ===");
        for (int row = 0; row < h; row++)
            sw.WriteLine($"row {row,4}: {rowSums[row],6}");
    }
}
