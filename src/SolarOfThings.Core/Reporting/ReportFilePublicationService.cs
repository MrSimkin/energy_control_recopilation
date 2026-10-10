using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SolarOfThings.Core.Reporting;

/// <summary>
/// A report is first generated alongside its chosen destination. On failure
/// the previously saved destination is retained; only a finished and nonempty
/// stage may be published over it. This is not a backup or rollback service.
/// </summary>
public static class ReportFilePublicationService
{
    public static string CreateStagingPath(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var full = Path.GetFullPath(destinationPath);
        var extension = Path.GetExtension(full);
        if (!AllowedExtension(extension))
            throw new ArgumentException("Only PDF and XLSX reports can be published.", nameof(destinationPath));

        var directory = Path.GetDirectoryName(full)
            ?? throw new ArgumentException("Report destination has no folder.", nameof(destinationPath));
        return Path.Combine(directory, $".report-{Guid.NewGuid():N}.inprogress{extension}");
    }

    public static void Publish(string stagedPath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var staged = Path.GetFullPath(stagedPath);
        var destination = Path.GetFullPath(destinationPath);
        if (!AllowedExtension(Path.GetExtension(staged)) ||
            !string.Equals(Path.GetExtension(staged), Path.GetExtension(destination),
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(staged), Path.GetDirectoryName(destination),
                StringComparison.OrdinalIgnoreCase) ||
            !HasGeneratedStageName(Path.GetFileName(staged),
                Path.GetExtension(staged)))
            throw new InvalidOperationException("Staged report must be in its destination folder.");

        if (!File.Exists(staged) || new FileInfo(staged).Length == 0)
            throw new IOException("Report output is missing or empty.");
        // Refuse unexpected filesystem indirections before touching an
        // existing export. Never replace a symlink masquerading as a report.
        if ((File.GetAttributes(staged) & FileAttributes.ReparsePoint) != 0 ||
            (File.Exists(destination) &&
             (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidOperationException("Report path must be a regular file.");

        RequirePlausibleReportFormat(staged);
        // Same-directory rename: the old destination is not touched until
        // the completed PDF/XLSX envelope has passed structural sanity checks.
        // A PDF signature or ZIP directory check does not replace end-user QA.
        File.Move(staged, destination, overwrite: true);
    }

    public static void DiscardStaging(string? stagedPath)
    {
        if (string.IsNullOrWhiteSpace(stagedPath)) return;
        var full = Path.GetFullPath(stagedPath);
        // Cleanup must NEVER accept the final user-selected report path
        // or an arbitrary PDF/XLSX. Only our generated sibling stage.
        if (!AllowedExtension(Path.GetExtension(full)) ||
            !HasGeneratedStageName(Path.GetFileName(full), Path.GetExtension(full)))
            throw new InvalidOperationException(
                "Refusing to discard anything but a generated incomplete report stage.");
        if (!File.Exists(full)) return;
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing to clean up a redirected report stage.");
        File.Delete(full);
    }

    private static bool HasGeneratedStageName(string name, string extension)
    {
        const string prefix = ".report-";
        var suffix = ".inprogress" + extension;
        return name.StartsWith(prefix, StringComparison.Ordinal) &&
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            name.Length == prefix.Length + 32 + suffix.Length &&
            Guid.TryParseExact(name.Substring(prefix.Length, 32), "N", out _);
    }

    private static void RequirePlausibleReportFormat(string path)
    {
        if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < 16)
                throw new InvalidDataException("Report is too short to be a PDF.");
            Span<byte> signature = stackalloc byte[5];
            stream.ReadExactly(signature);
            if (!signature.SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("Report output lacks PDF header.");
            var tail = (int)Math.Min(stream.Length, 4096);
            stream.Seek(-tail, SeekOrigin.End);
            var buffer = new byte[tail];
            stream.ReadExactly(buffer);
            if (!Encoding.ASCII.GetString(buffer).Contains("%%EOF",
                    StringComparison.Ordinal))
                throw new InvalidDataException("Report output lacks PDF end marker.");
            return;
        }

        // .xlsx must at least be a readable OOXML ZIP with workbook,
        // package metadata and an actual worksheet. This is NOT a claim
        // that all cell styles/formulas are valid.
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var contentTypes = archive.GetEntry("[Content_Types].xml");
            var relationships = archive.GetEntry("_rels/.rels");
            var workbook = archive.GetEntry("xl/workbook.xml");
            if (contentTypes is null || relationships is null || workbook is null ||
                !archive.Entries.Any(x => x.FullName.StartsWith(
                    "xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                    x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    "Report XLSX lacks mandatory workbook parts.");

            // A valid ZIP directory alone is insufficient: a truncated,
            // malformed or substituted workbook.xml can still be opened as
            // a ZIP. Validate small mandatory OOXML metadata without
            // materializing potentially million-row worksheets.
            RequireXmlRoot(contentTypes, "Types");
            RequireXmlRoot(relationships, "Relationships");
            RequireXmlRoot(workbook, "workbook");
        }
        catch (InvalidDataException)
        {
            throw;
        }
    }

    private static void RequireXmlRoot(ZipArchiveEntry entry, string expected)
    {
        // Bound decompressed metadata so ZIP bombs cannot force unbounded
        // allocation during the pre-publication verification.
        if (entry.Length is < 1 or > 4_194_304)
            throw new InvalidDataException("Report OOXML metadata is missing or oversized.");
        try
        {
            using var data = entry.Open();
            using var reader = XmlReader.Create(data, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4_194_304
            });
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element ||
                !string.Equals(reader.LocalName, expected, StringComparison.Ordinal))
                throw new InvalidDataException("Report OOXML metadata has the wrong root.");
            while (reader.Read()) { } // check complete XML rather than only prefix
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Report OOXML metadata is malformed.", ex);
        }
    }

    private static bool AllowedExtension(string? extension) =>
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
}
