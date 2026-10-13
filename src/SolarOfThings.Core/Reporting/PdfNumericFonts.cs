using System.Text.RegularExpressions;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Fonts;

namespace SolarOfThings.Core.Reporting;

/// <summary>OFL IBM Plex Mono for numeric PDF runs, Windows resolver for existing proportional text.</summary>
public sealed class PdfNumericFonts : IFontResolver
{
    public const string Family = "IBM Plex Mono";
    private const string RegularFace = "solar-ibm-plex-mono-regular";
    private const string BoldFace = "solar-ibm-plex-mono-bold";
    private static readonly object Gate = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("PDF rendering requires Windows.");
        lock (Gate)
        {
            if (_initialized) return;
            foreach (var face in new[] { "IBMPlexMono-Regular.ttf", "IBMPlexMono-Bold.ttf" })
                if (!File.Exists(Path.Combine(FontDirectory, face)))
                    throw new FileNotFoundException("Packaged IBM Plex Mono font is missing.", face);
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            GlobalFontSettings.FontResolver = new PdfNumericFonts();
            _initialized = true;
        }
    }

    private static string FontDirectory => Path.Combine(
        AppContext.BaseDirectory, "assets", "fonts", "IBM-Plex-Mono");

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName.Equals(Family, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(isBold ? BoldFace : RegularFace);
        return PlatformFontResolver.ResolveTypeface(familyName, isBold, isItalic);
    }

    public byte[]? GetFont(string faceName) =>
        faceName switch
        {
            RegularFace => File.ReadAllBytes(Path.Combine(FontDirectory, "IBMPlexMono-Regular.ttf")),
            BoldFace => File.ReadAllBytes(Path.Combine(FontDirectory, "IBMPlexMono-Bold.ttf")),
            _ => null
        };

    private static readonly Regex NumericToken = new(
        @"(?<![\p{L}])[-+]?\d+(?:[.,:/\-]\d+)*(?:%)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void AddNumericText(Paragraph paragraph, string value)
    {
        var position = 0;
        foreach (Match match in NumericToken.Matches(value))
        {
            if (match.Index > position) paragraph.AddText(value[position..match.Index]);
            paragraph.AddFormattedText(match.Value).Font.Name = Family;
            position = match.Index + match.Length;
        }
        if (position < value.Length) paragraph.AddText(value[position..]);
    }
}
