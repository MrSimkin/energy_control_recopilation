using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace SolarOfThings.Core.SqlExplorer;

/// <summary>
/// Forward-only XLSX writer, using Open Packaging Convention + SpreadsheetML.
/// Does not build an in-memory workbook or a shared string table.
/// The caller owns safe read-only SQL execution and atomic file publication.
/// </summary>
internal static class StreamingSqlXlsxWriter
{
    private const string SpreadsheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string PackageRelationsNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string DocumentRelationsNs =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string ContentTypesNs =
        "http://schemas.openxmlformats.org/package/2006/content-types";
    private const int MaximumExcelRows = 1_048_576;
    private const int MaximumExcelColumns = 16_384;
    private const int MaximumExcelTextCharacters = 32_767;

    public static int Write(string filename, IReadOnlyList<string> headers,
        Func<IReadOnlyList<SqlCell>?> nextRow, int maxDataRows,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(nextRow);
        if (headers.Count is < 1 or > MaximumExcelColumns)
            throw new ArgumentOutOfRangeException(nameof(headers));
        if (maxDataRows is < 0 or >= MaximumExcelRows)
            throw new ArgumentOutOfRangeException(nameof(maxDataRows));
        cancellation.ThrowIfCancellationRequested();

        using var disk = new FileStream(filename, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 64 * 1024);
        using var zip = new ZipArchive(disk, ZipArchiveMode.Create);
        WritePart(zip, "[Content_Types].xml", writer =>
        {
            writer.WriteStartElement("Types", ContentTypesNs);
            WriteEmpty(writer, "Default", ContentTypesNs,
                ("Extension", "rels"),
                ("ContentType", "application/vnd.openxmlformats-package.relationships+xml"));
            WriteEmpty(writer, "Default", ContentTypesNs,
                ("Extension", "xml"), ("ContentType", "application/xml"));
            WriteEmpty(writer, "Override", ContentTypesNs,
                ("PartName", "/xl/workbook.xml"),
                ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"));
            WriteEmpty(writer, "Override", ContentTypesNs,
                ("PartName", "/xl/worksheets/sheet1.xml"),
                ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"));
            writer.WriteEndElement();
        });
        WritePart(zip, "_rels/.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelationsNs);
            WriteEmpty(writer, "Relationship", PackageRelationsNs,
                ("Id", "rId1"),
                ("Type", DocumentRelationsNs + "/officeDocument"),
                ("Target", "xl/workbook.xml"));
            writer.WriteEndElement();
        });
        WritePart(zip, "xl/workbook.xml", writer =>
        {
            writer.WriteStartElement("workbook", SpreadsheetNs);
            writer.WriteStartElement("sheets", SpreadsheetNs);
            writer.WriteStartElement("sheet", SpreadsheetNs);
            writer.WriteAttributeString("name", "SQL");
            writer.WriteAttributeString("sheetId", "1");
            writer.WriteAttributeString("r", "id", DocumentRelationsNs, "rId1");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        });
        WritePart(zip, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartElement("Relationships", PackageRelationsNs);
            WriteEmpty(writer, "Relationship", PackageRelationsNs,
                ("Id", "rId1"),
                ("Type", DocumentRelationsNs + "/worksheet"),
                ("Target", "worksheets/sheet1.xml"));
            writer.WriteEndElement();
        });

        var count = 0;
        WritePart(zip, "xl/worksheets/sheet1.xml", writer =>
        {
            writer.WriteStartElement("worksheet", SpreadsheetNs);
            writer.WriteStartElement("sheetData", SpreadsheetNs);
            writer.WriteStartElement("row", SpreadsheetNs);
            writer.WriteAttributeString("r", "1");
            for (var col = 0; col < headers.Count; col++)
                WriteTextCell(writer, CellRef(col, 1), headers[col]);
            writer.WriteEndElement();

            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var row = nextRow();
                if (row is null) break;
                if (count >= maxDataRows)
                    throw new InvalidOperationException(
                        "Excel data-row limit exceeded; incomplete workbook will not be published.");
                if (row.Count != headers.Count)
                    throw new InvalidDataException("Query result changed its column count.");
                var rowNumber = checked(count + 2);
                writer.WriteStartElement("row", SpreadsheetNs);
                writer.WriteAttributeString("r",
                    rowNumber.ToString(CultureInfo.InvariantCulture));
                for (var col = 0; col < row.Count; col++)
                {
                    var cell = row[col];
                    if (cell.IsNull) continue; // blank, NEVER zero
                    var position = CellRef(col, rowNumber);
                    if (cell.Type == "REAL" &&
                        double.TryParse(cell.Text, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var real) &&
                        double.IsFinite(real))
                        WriteNumberCell(writer, position,
                            real.ToString("R", CultureInfo.InvariantCulture));
                    else if (cell.Type == "INTEGER" &&
                        long.TryParse(cell.Text, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var integer) &&
                        integer is >= -9_007_199_254_740_991L and <= 9_007_199_254_740_991L)
                        WriteNumberCell(writer, position,
                            integer.ToString(CultureInfo.InvariantCulture));
                    else
                        WriteTextCell(writer, position, cell.Text ?? "");
                }
                writer.WriteEndElement();
                count++;
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        });
        return count;
    }

    private static string CellRef(int column, int row)
    {
        var number = column + 1;
        var prefix = "";
        while (number > 0)
        {
            number--;
            prefix = (char)('A' + number % 26) + prefix;
            number /= 26;
        }
        return prefix + row.ToString(CultureInfo.InvariantCulture);
    }

    private static void WriteNumberCell(XmlWriter writer, string reference,
        string number)
    {
        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", reference);
        writer.WriteStartElement("v", SpreadsheetNs);
        writer.WriteString(number);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteTextCell(XmlWriter writer, string reference,
        string text)
    {
        if (text.Length > MaximumExcelTextCharacters)
            throw new InvalidDataException(
                "Excel permits at most 32,767 text characters in a cell.");
        writer.WriteStartElement("c", SpreadsheetNs);
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is", SpreadsheetNs);
        writer.WriteStartElement("t", SpreadsheetNs);
        writer.WriteAttributeString("xml", "space",
            "http://www.w3.org/XML/1998/namespace", "preserve");
        // Inline strings are TEXT, never formulas. Retain user data exactly,
        // including leading '=' and '+'; XmlWriter XML-escapes dangerous markup.
        writer.WriteString(text);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WritePart(ZipArchive zip, string name, Action<XmlWriter> produce)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var xml = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            CloseOutput = false,
            ConformanceLevel = ConformanceLevel.Document
        });
        xml.WriteStartDocument();
        produce(xml);
        xml.WriteEndDocument();
        xml.Flush();
    }

    private static void WriteEmpty(XmlWriter writer, string tag,
        string xmlNamespace, params (string Name, string Value)[] attributes)
    {
        writer.WriteStartElement(tag, xmlNamespace);
        foreach (var attribute in attributes)
            writer.WriteAttributeString(attribute.Name, attribute.Value);
        writer.WriteEndElement();
    }
}
