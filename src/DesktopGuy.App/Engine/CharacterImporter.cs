using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopGuy.App.Characters;

namespace DesktopGuy.App.Engine;

/// <summary>Thrown for any problem the user can actually fix (bad name, bad file, sheet too small, ...).</summary>
public sealed class CharacterImportException : Exception
{
    public CharacterImportException(string message) : base(message)
    {
    }
}

/// <summary>
/// Turns a green-screen sprite sheet PNG (the kind produced by the AI-art
/// prompt template in the README) into a ready-to-use character folder,
/// entirely in-app - no Python, no external image tools. Deliberately
/// narrower than the hand-processed pipeline used for the built-in
/// characters: it assumes an evenly-spaced grid and a solid green
/// background, and just chroma-keys + despills + re-grids onto the
/// standard 84x84 layout. A messier upload (drawn-in grid lines, an
/// off-hue background, a non-green-adjacent color needing special
/// handling) still needs the manual, AI-assisted cleanup process
/// documented in the README - this covers the common case cheaply.
/// </summary>
public static class CharacterImporter
{
    public const int FrameSize = 84;
    private const int Margin = 4;
    private const int MinCellSize = 24;

    // (animation name, fps, loop) for each row of the standard layout,
    // in order - matches every built-in character's row order exactly.
    // Rows beyond what the source sheet actually has are simply skipped.
    private static readonly (string Name, double Fps, bool Loop)[] StandardRows =
    {
        ("idle", 2, true),
        ("walk", 6, true),
        ("sleep", 1.5, true),
        ("drag", 4, true),
        ("wake", 2, false),
        ("dance", 5, true),
        ("watch", 2, true),
        ("answerCall", 4, false),
        ("openMail", 4, false),
        ("hacking", 3, true),
        ("pickUp", 8, false),
        ("hot", 3, true),
        ("sunny", 2, true),
        ("rainy", 3, true),
        ("cold", 3, true),
        ("eating", 3, false),
        ("playing", 4, false),
        ("lowBattery", 2, false),
        ("snapshot", 8, false),
    };

    public static string[] StarterPhrases =
    {
        "Hi there!",
        "Don't forget to stretch a little~",
        "You're doing great today.",
        "Comfy over here.",
        "Take a sip of water, maybe?",
        "I like it when you're around.",
        "Keep going, you've got this.",
        "Nothing wrong with a little break.",
    };

    /// <summary>
    /// Validates and imports a new character. Throws CharacterImportException
    /// with a message safe to show directly to the user for anything wrong
    /// with the inputs; any other exception is an unexpected failure (I/O,
    /// a corrupt image) and should be shown with a more generic wrapper.
    /// Leaves no partial folder behind if anything fails partway through.
    /// </summary>
    public static string Import(string displayName, string sourceImagePath, int columns, int rows)
    {
        string trimmedName = displayName.Trim();
        if (trimmedName.Length == 0)
        {
            throw new CharacterImportException("Give the character a name first.");
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in trimmedName)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
            {
                throw new CharacterImportException(
                    $"The name can't contain '{c}' - it's used as the folder name on disk.");
            }
        }

        if (columns < 1 || rows < 1)
        {
            throw new CharacterImportException("Columns and rows both need to be at least 1.");
        }

        if (!File.Exists(sourceImagePath))
        {
            throw new CharacterImportException("That image file couldn't be found.");
        }

        string folderName = trimmedName;
        string targetFolder = Path.Combine(CharacterLoader.CharactersRootFolder, folderName);
        if (Directory.Exists(targetFolder))
        {
            throw new CharacterImportException(
                $"A character folder called \"{folderName}\" already exists. Pick a different name.");
        }

        BitmapSource source;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(sourceImagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        }
        catch (Exception ex)
        {
            throw new CharacterImportException(
                $"Couldn't read that file as an image: {ex.Message}");
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int cellWidth = width / columns;
        int cellHeight = height / rows;

        if (cellWidth - Margin * 2 < MinCellSize || cellHeight - Margin * 2 < MinCellSize)
        {
            throw new CharacterImportException(
                $"That image is too small for a {columns}x{rows} grid - each cell would only be " +
                $"about {cellWidth}x{cellHeight}px. Double check the column/row counts, or use a bigger image.");
        }

        byte[] pixels = ChromaKeyAndDespill(source, width, height);

        int usableRows = Math.Min(rows, StandardRows.Length);
        var frames = new byte[FrameSize * columns * 4 * FrameSize * usableRows];
        var sheetStride = FrameSize * columns * 4;

        for (int row = 0; row < usableRows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int srcX = col * cellWidth + Margin;
                int srcY = row * cellHeight + Margin;
                int srcW = cellWidth - Margin * 2;
                int srcH = cellHeight - Margin * 2;

                ResizeCellInto(
                    pixels, width, srcX, srcY, srcW, srcH,
                    frames, sheetStride, col * FrameSize, row * FrameSize);
            }
        }

        int sheetWidth = FrameSize * columns;
        int sheetHeight = FrameSize * usableRows;
        var sheetBitmap = BitmapSource.Create(
            sheetWidth, sheetHeight, 96, 96, PixelFormats.Bgra32, null, frames, sheetStride);

        try
        {
            Directory.CreateDirectory(targetFolder);

            string sheetPath = Path.Combine(targetFolder, "spritesheet.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(sheetBitmap));
            using (var stream = File.Create(sheetPath))
            {
                encoder.Save(stream);
            }

            string json = BuildCharacterJson(trimmedName, columns, usableRows);
            File.WriteAllText(Path.Combine(targetFolder, "character.json"), json);

            File.Copy(sourceImagePath, Path.Combine(targetFolder, "spritesheet_raw.png"), overwrite: true);
        }
        catch (Exception ex)
        {
            TryDeleteFolder(targetFolder);
            throw new CharacterImportException($"Couldn't write the new character's files: {ex.Message}");
        }

        return folderName;
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup - a leftover partial folder is a much
            // smaller problem than masking the real error above.
        }
    }

    /// <summary>
    /// Removes the green background and cleans up any green-tinted fringe
    /// left on anti-aliased edges, over the whole source image at once -
    /// same two techniques used for every hand-processed character sheet:
    /// a green-dominant pixel is background (alpha to 0); on whatever's
    /// left, if green is still the strongest channel, clamp it down to
    /// the stronger of red/blue, which pulls a faint green edge tint back
    /// to neutral without touching real orange/black/beige-type palettes.
    /// </summary>
    private static byte[] ChromaKeyAndDespill(BitmapSource source, int width, int height)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            int b = pixels[i];
            int g = pixels[i + 1];
            int r = pixels[i + 2];

            bool isBackground = g > r + 30 && g > b + 30 && g > 150;
            if (isBackground)
            {
                pixels[i + 3] = 0;
                continue;
            }

            if (g > r && g > b)
            {
                pixels[i + 1] = (byte)Math.Max(r, b);
            }
        }

        return pixels;
    }

    /// <summary>
    /// Nearest-neighbor resize of one source cell into its slot in the
    /// destination sheet buffer. Nearest-neighbor (not a smoother filter)
    /// deliberately matches CroppedBitmap/RenderOptions behavior used
    /// elsewhere in the app for sprite frames, and avoids blurring a
    /// freshly-transparent edge into a faint colored halo.
    /// </summary>
    private static void ResizeCellInto(
        byte[] src, int srcWidth, int srcX, int srcY, int srcW, int srcH,
        byte[] dst, int dstStride, int dstX, int dstY)
    {
        for (int y = 0; y < FrameSize; y++)
        {
            int sy = srcY + y * srcH / FrameSize;
            for (int x = 0; x < FrameSize; x++)
            {
                int sx = srcX + x * srcW / FrameSize;
                int srcOffset = (sy * srcWidth + sx) * 4;
                int dstOffset = (dstY + y) * dstStride + (dstX + x) * 4;
                dst[dstOffset] = src[srcOffset];
                dst[dstOffset + 1] = src[srcOffset + 1];
                dst[dstOffset + 2] = src[srcOffset + 2];
                dst[dstOffset + 3] = src[srcOffset + 3];
            }
        }
    }

    /// <summary>
    /// Hand-formatted to match the compact, single-line-per-animation style
    /// every other character.json in the repo uses - System.Text.Json's
    /// WriteIndented option produces a much more verbose multi-line layout
    /// that would stick out from every hand-written file next to it.
    /// </summary>
    private static string BuildCharacterJson(string displayName, int columns, int rows)
    {
        string id = displayName.ToLowerInvariant().Replace(' ', '-');
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"id\": \"{JsonEscape(id)}\",\n");
        sb.Append($"  \"displayName\": \"{JsonEscape(displayName)}\",\n");
        sb.Append("  \"spriteSheet\": \"spritesheet.png\",\n");
        sb.Append($"  \"frameSize\": {{ \"width\": {FrameSize}, \"height\": {FrameSize} }},\n");
        sb.Append("  \"scale\": 1.75,\n");
        sb.Append("  \"smoothTransitions\": false,\n");
        sb.Append("  \"animations\": {\n");
        for (int row = 0; row < rows; row++)
        {
            var (name, fps, loop) = StandardRows[row];
            string fpsText = fps % 1 == 0
                ? ((int)fps).ToString(CultureInfo.InvariantCulture)
                : fps.ToString(CultureInfo.InvariantCulture);
            string comma = row < rows - 1 ? "," : "";
            sb.Append($"    \"{name}\": {{ \"row\": {row}, \"frameCount\": {columns}, \"fps\": {fpsText}, \"loop\": {(loop ? "true" : "false")} }}{comma}\n");
        }

        sb.Append("  },\n");
        sb.Append("  \"behavior\": {\n");
        sb.Append("    \"idleTimeoutSeconds\": 90,\n");
        sb.Append("    \"walkSpeedPxPerSec\": 45,\n");
        sb.Append("    \"walkIntervalMinSeconds\": 10,\n");
        sb.Append("    \"walkIntervalMaxSeconds\": 30,\n");
        sb.Append("    \"walkDurationMinSeconds\": 2,\n");
        sb.Append("    \"walkDurationMaxSeconds\": 5,\n");
        sb.Append("    \"speechIntervalMinSeconds\": 40,\n");
        sb.Append("    \"speechIntervalMaxSeconds\": 120,\n");
        sb.Append("    \"speechDurationSeconds\": 4.5,\n");
        sb.Append("    \"idleSurpriseIntervalMinSeconds\": 90,\n");
        sb.Append("    \"idleSurpriseIntervalMaxSeconds\": 240\n");
        sb.Append("  },\n");
        sb.Append("  \"phrases\": [\n");
        for (int i = 0; i < StarterPhrases.Length; i++)
        {
            string comma = i < StarterPhrases.Length - 1 ? "," : "";
            sb.Append($"    \"{JsonEscape(StarterPhrases[i])}\"{comma}\n");
        }

        sb.Append("  ]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
