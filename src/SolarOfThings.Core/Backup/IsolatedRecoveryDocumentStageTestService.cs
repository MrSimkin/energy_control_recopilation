using System.IO.Compression;
using System.Security.Cryptography;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST-ONLY document-evidence staging in a NEW disposable synthetic folder.
/// No database table is modified, no user data paths are opened, and the
/// returned files cannot be activated by this service.
/// </summary>
public sealed class IsolatedRecoveryDocumentStageTestService
{
    private const int MaximumDocuments = 2_000;
    private const long MaximumDocumentsBytes = 256L * 1024 * 1024;
    private const string StagePrefix = "recovery-documents-staged-";

    public SyntheticDocumentStageReceipt Stage(
        string completeBackupZip,
        string syntheticTargetDatabase,
        CancellationToken cancellationToken = default,
        bool simulateFailureAfterFirstFile = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = AuthorizeInputs(completeBackupZip, syntheticTargetDatabase);
        var manifest = FullBackupService.VerifyArchive(completeBackupZip);
        cancellationToken.ThrowIfCancellationRequested();
        var documents = manifest.Files
            .Where(x => IsDocument(x.RelativePath))
            .OrderBy(x => x.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (documents.Length > MaximumDocuments ||
            documents.Any(x => x.Size < 0 || x.Size > MaximumDocumentsBytes) ||
            documents.Sum(x => x.Size) > MaximumDocumentsBytes)
            throw new InvalidDataException("Synthetic document stage exceeds bounded limits.");

        var stage = Path.Combine(root, StagePrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var finished = false;
        try
        {
            var results = new List<SyntheticStagedDocument>(documents.Length);
            using var archive = ZipFile.OpenRead(completeBackupZip);
            for (var i = 0; i < documents.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = documents[i];
                var category = item.RelativePath.StartsWith(
                    "documents/Bills/", StringComparison.Ordinal) ? "Bills" : "Tariffs";
                var folder = Path.Combine(stage, category);
                Directory.CreateDirectory(folder);
                // Generated destination names: no arbitrary archived path is
                // ever interpreted as a filesystem path at the destination.
                var name = (i + 1).ToString("D6") + "-" +
                           item.Sha256.ToUpperInvariant() + ".pdf";
                var target = Path.Combine(folder, name);
                var entry = archive.GetEntry(item.RelativePath) ??
                    throw new InvalidDataException("Verified document is absent.");
                using var input = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[64 * 1024];
                long total = 0;
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    total = checked(total + count);
                    if (total > item.Size)
                        throw new InvalidDataException("Archived document grew during staging.");
                    output.Write(buffer, 0, count);
                    hash.AppendData(buffer, 0, count);
                }
                output.Flush(flushToDisk: true);
                var digest = Convert.ToHexString(hash.GetHashAndReset());
                if (total != item.Size ||
                    !string.Equals(digest, item.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Synthetic staged document failed SHA-256.");
                results.Add(new SyntheticStagedDocument(category, item.RelativePath,
                    item.Sha256.ToUpperInvariant(), total, target));
                if (simulateFailureAfterFirstFile)
                    throw new InvalidOperationException("SYNTHETIC_INJECTED_DOCUMENT_INTERRUPTION");
            }
            cancellationToken.ThrowIfCancellationRequested();
            finished = true;
            return new SyntheticDocumentStageReceipt(stage, results,
                "STAGED_SYNTHETIC_DOCUMENT_EVIDENCE_ONLY", false,
                "Evidence files only; no SQLite rows, FK relationships, live paths, " +
                "document catalogs or recovery activation were changed.");
        }
        finally
        {
            if (!finished && Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
        }
    }

    /// <summary>
    /// Independent verification of the disposable staging folder; never
    /// trusts just the original archive check or unverified caller paths.
    /// </summary>
    public bool Verify(SyntheticDocumentStageReceipt receipt,
        string syntheticTargetDatabase)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var root = IsolatedRecoveryAdditiveTestService
            .RequireSyntheticFixtureRoot(syntheticTargetDatabase);
        if (receipt.RealRestoreAuthorized || receipt.Status !=
                "STAGED_SYNTHETIC_DOCUMENT_EVIDENCE_ONLY" ||
            receipt.Documents.Count > MaximumDocuments ||
            !IsDirectGeneratedStage(root, receipt.StageDirectory) ||
            !Directory.Exists(receipt.StageDirectory) ||
            IsLinked(receipt.StageDirectory))
            return false;
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var item in receipt.Documents)
        {
            if (item.Category is not ("Bills" or "Tariffs") ||
                item.Size < 0 || item.Size > MaximumDocumentsBytes ||
                item.Sha256.Length != 64 ||
                !item.Sha256.All(Uri.IsHexDigit) ||
                !item.SourceRelativePath.StartsWith(
                    "documents/" + item.Category + "/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetDirectoryName(item.StageFilePath),
                    Path.Combine(receipt.StageDirectory, item.Category),
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(item.StageFilePath).EndsWith(
                    "-" + item.Sha256 + ".pdf", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(item.StageFilePath) || IsLinked(item.StageFilePath) ||
                !expected.Add(Path.GetFullPath(item.StageFilePath)) ||
                new FileInfo(item.StageFilePath).Length != item.Size)
                return false;
            total = checked(total + item.Size);
            if (total > MaximumDocumentsBytes) return false;
            using var stream = File.OpenRead(item.StageFilePath);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(stream)),
                    item.Sha256, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        // Any unlisted file, including an unexpected extra document or
        // a junction nested under the stage, fails verification.
        if (Directory.EnumerateDirectories(receipt.StageDirectory, "*",
                SearchOption.TopDirectoryOnly).Any(dir =>
                (Path.GetFileName(dir) is not ("Bills" or "Tariffs")) || IsLinked(dir)))
            return false;
        if (Directory.EnumerateFiles(receipt.StageDirectory, "*",
                SearchOption.TopDirectoryOnly).Any())
            return false;
        var actual = Directory.EnumerateFiles(receipt.StageDirectory, "*",
                SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return actual.SetEquals(expected);
    }

    private static string AuthorizeInputs(string zip, string database)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zip);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(database);
        var file = Path.GetFullPath(zip);
        var relative = Path.GetRelativePath(root, file);
        if (relative == "." || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) || !File.Exists(file) || IsLinked(file))
            throw new InvalidOperationException(
                "Synthetic staging requires an archive inside its marked fixture.");
        var cursor = Path.GetDirectoryName(file);
        while (cursor is not null &&
               !string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(cursor) || IsLinked(cursor))
                throw new InvalidOperationException("Linked/missing synthetic source parent.");
            cursor = Path.GetDirectoryName(cursor);
        }
        if (cursor is null) throw new InvalidOperationException("Archive outside fixture.");
        return root;
    }

    private static bool IsDirectGeneratedStage(string root, string stage) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(stage)),
            root, StringComparison.OrdinalIgnoreCase) &&
        Path.GetFileName(stage).StartsWith(StagePrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(Path.GetFileName(stage)[StagePrefix.Length..], "N", out _);

    private static bool IsLinked(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool IsDocument(string path) =>
        (path.StartsWith("documents/Bills/", StringComparison.Ordinal) ||
         path.StartsWith("documents/Tariffs/", StringComparison.Ordinal)) &&
        path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
}

public sealed record SyntheticStagedDocument(string Category,
    string SourceRelativePath, string Sha256, long Size, string StageFilePath);
public sealed record SyntheticDocumentStageReceipt(string StageDirectory,
    IReadOnlyList<SyntheticStagedDocument> Documents, string Status,
    bool RealRestoreAuthorized, string SafetyExplanation);
