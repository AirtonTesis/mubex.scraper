using CaptchaGridAnalyzer.Core;
using CaptchaGridAnalyzer.Diagnostics;
using CaptchaGridAnalyzer.Exceptions;
using OpenCvSharp;
using System.Diagnostics.CodeAnalysis;

namespace CaptchaGridAnalyzer;

/// <summary>
/// CLI composition root. Excluded from coverage: it's a thin arg-parsing/exit-code
/// wrapper around <see cref="CaptchaAnalyzer"/> (already covered end-to-end by the
/// integration tests), and its error paths call <see cref="Environment.Exit"/>,
/// which would tear down the test process itself if exercised directly.
/// </summary>
[ExcludeFromCodeCoverage]
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("CAPTCHA Grid Analyzer v1.0");
        Console.WriteLine("========================");
        Console.WriteLine();

        // Diagnostic mode: --diag <imagePath> [outputDir]
        if (args.Length >= 2 && args[0] == "--diag")
        {
            string diagImage = args[1];
            string diagOut = args.Length >= 3 ? args[2] : "./diag_output";
            GridDiagnostic.Run(diagImage, diagOut);
            return;
        }

        // Quick test mode: --test <imagePath>
        if (args.Length >= 2 && args[0] == "--test")
        {
            RunTest(args[1]);
            return;
        }

        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        string imagePath = args[0];
        string outputDirectory = args[1];

        try
        {
            using var analyzer = new CaptchaAnalyzer();

            Console.WriteLine($"Analyzing image: {imagePath}");
            Console.WriteLine($"Output directory: {outputDirectory}");
            Console.WriteLine();

            string result = analyzer.Analyze(imagePath, outputDirectory);

            Console.WriteLine("Analysis completed successfully!");
            Console.WriteLine();
            Console.WriteLine("JSON Result:");
            Console.WriteLine(result);

            string jsonFilePath = Path.Combine(outputDirectory, "analysis_result.json");
            File.WriteAllText(jsonFilePath, result);
            Console.WriteLine();
            Console.WriteLine($"Results saved to: {jsonFilePath}");
        }
        catch (Exceptions.FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (InvalidImageFormatException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (ImageLoadException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (GridNotDetectedException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (InvalidGridStructureException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (ExtractionBoundsException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (CaptchaAnalysisException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static void RunTest(string imagePath)
    {
        Console.WriteLine($"=== Quick Test: {imagePath} ===");
        try
        {
            Console.WriteLine("Step 1: Loading image...");
            using var img = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (img.Empty()) { Console.WriteLine("FAILED: empty image"); return; }
            Console.WriteLine($"  OK: {img.Width}x{img.Height} ch={img.Channels()}");

            Console.WriteLine("Step 2: Convert to gray...");
            using var gray = new Mat();
            if (img.Channels() == 4)
                Cv2.CvtColor(img, gray, ColorConversionCodes.BGRA2GRAY);
            else if (img.Channels() == 1)
                img.CopyTo(gray);
            else
                Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
            Console.WriteLine($"  OK: {gray.Width}x{gray.Height}");

            Console.WriteLine("Step 3: Row/Col sums...");
            int[] rowSums = new int[gray.Height];
            int[] colSums = new int[gray.Width];
            for (int y = 0; y < gray.Height; y++)
                for (int x = 0; x < gray.Width; x++)
                { rowSums[y] += gray.At<byte>(y, x); colSums[x] += gray.At<byte>(y, x); }
            Console.WriteLine($"  Zero-rows: {rowSums.Count(v => v == 0)}  Zero-cols: {colSums.Count(v => v == 0)}");

            Console.WriteLine("Step 4: Header/Footer detection...");
            // Compute row means
            double[] rowMeans = rowSums.Select(v => (double)v / gray.Width).ToArray();
            int h = gray.Height;
            int midStart = h / 3; int midEnd = h * 2 / 3;
            double contentMean = rowMeans.Skip(midStart).Take(midEnd - midStart).Average();
            double darkThr = contentMean * 0.40;
            Console.WriteLine($"  contentMean={contentMean:F1} darkThreshold={darkThr:F1}");

            int cropTop = 0;
            for (int y = 0; y < (int)(h * 0.30); y++)
            {
                if (rowMeans[y] <= darkThr) cropTop = y + 1;
                else break;
            }
            int cropBottom = h;
            for (int y = h - 1; y >= (int)(h - h * 0.30); y--)
            {
                if (rowMeans[y] <= darkThr) cropBottom = y;
                else break;
            }
            Console.WriteLine($"  cropTop={cropTop} cropBottom={cropBottom} gridHeight={cropBottom - cropTop}");

            Console.WriteLine("Step 5: Grid detector...");
            var detector = new CaptchaGridAnalyzer.Detection.GridDetector();

            // Active range info
            double[] colSig = colSums.Select(v => (double)v / gray.Height).ToArray();
            double[] rowSig = rowSums.Select(v => (double)v / gray.Width).ToArray();
            int xStart = 0; while (xStart < colSig.Length && colSig[xStart] < 1) xStart++;
            int xEnd = colSig.Length; while (xEnd > xStart && colSig[xEnd-1] < 1) xEnd--;
            int yStart = 0; while (yStart < rowSig.Length && rowSig[yStart] < 1) yStart++;
            int yEnd = rowSig.Length; while (yEnd > yStart && rowSig[yEnd-1] < 1) yEnd--;
            Console.WriteLine($"  Active cols: {xStart}-{xEnd}  Active rows: {yStart}-{yEnd}");

            var gridInfo = detector.DetectGrid(img);
            Console.WriteLine($"  Grid = {gridInfo.Rows}x{gridInfo.Columns}");
            Console.WriteLine($"  Content bounds: top={gridInfo.ContentTop} bottom={gridInfo.ContentBottom} left={gridInfo.ContentLeft} right={gridInfo.ContentRight}");
            Console.WriteLine($"  Header: {gridInfo.HeaderHeight}px  Footer: {gridInfo.FooterHeight}px (startY={gridInfo.FooterStartY})  Left border: {gridInfo.LeftBorderWidth}px  Right border: {gridInfo.RightBorderWidth}px");
            Console.WriteLine($"  Row separators: [{string.Join(", ", gridInfo.RowSeparators)}]");
            Console.WriteLine($"  Col separators: [{string.Join(", ", gridInfo.ColSeparators)}]");
            var rowBounds = gridInfo.GetRowBounds();
            var colBounds = gridInfo.GetColBounds();
            for (int i = 0; i < rowBounds.Length; i++)
                Console.WriteLine($"  Row {i}: y={rowBounds[i].Start}-{rowBounds[i].End} h={rowBounds[i].End - rowBounds[i].Start}");
            for (int i = 0; i < colBounds.Length; i++)
                Console.WriteLine($"  Col {i}: x={colBounds[i].Start}-{colBounds[i].End} w={colBounds[i].End - colBounds[i].Start}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: CaptchaGridAnalyzer <imagePath> <outputDirectory>");
        Console.WriteLine("       CaptchaGridAnalyzer --diag <imagePath> [outputDir]");
        Console.WriteLine("       CaptchaGridAnalyzer --test <imagePath>");
    }
}
