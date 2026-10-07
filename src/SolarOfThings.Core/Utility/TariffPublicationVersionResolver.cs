namespace SolarOfThings.Core.Utility;

/// <summary>
/// Resolves publication-version relationships for one effective period.
/// Precedence is evidence-driven:
/// 1. explicit official correction/supersession graph;
/// 2. unique later official publication date when every competing document
///    has authoritative date metadata;
/// 3. unique publication explicitly marked retroactive;
/// 4. otherwise remain ambiguous.
///
/// It does not resolve commune, RED, ETR or customer applicability.
/// Local database row order, capture time and parser CandidateIndex are never
/// authority for version precedence.
/// </summary>
public sealed class TariffPublicationVersionResolver
{
    public IReadOnlyList<TariffPublicationVersionResolution> Resolve(
        IReadOnlyList<TariffPublication> publications) =>
        Resolve(
            publications,
            []);

    public IReadOnlyList<TariffPublicationVersionResolution> Resolve(
        IReadOnlyList<TariffPublication> publications,
        IReadOnlyList<TariffPublicationRelation> relations)
    {
        var result =
            new List<TariffPublicationVersionResolution>();

        foreach (var item in publications.Where(
                     item => !item.EffectiveFrom.HasValue))
        {
            result.Add(
                new TariffPublicationVersionResolution(
                    item.PublicationId,
                    null,
                    "NO_EFFECTIVE_DATE",
                    null,
                    "Publication has no parsed effective period."));
        }

        var groups = publications
            .Where(item => item.EffectiveFrom.HasValue)
            .GroupBy(item => new
            {
                item.Provider,
                item.Category,
                Effective = item.EffectiveFrom!.Value
            });

        foreach (var group in groups)
        {
            var items = group
                .OrderBy(item => item.PublicationId)
                .ToArray();

            if (TryResolveFromExplicitGraph(
                    items,
                    relations,
                    group.Key.Effective,
                    result))
            {
                continue;
            }

            if (TryResolveEquivalentDuplicateFiles(
                    items,
                    group.Key.Effective,
                    result))
            {
                continue;
            }

            if (TryResolveFromOfficialPublicationDate(
                    items,
                    group.Key.Effective,
                    result))
            {
                continue;
            }

            var retroactive = items
                .Where(item => item.IsRetroactive)
                .ToArray();

            if (retroactive.Length == 1)
            {
                var preferred = retroactive[0];
                foreach (var item in items)
                {
                    result.Add(
                        new TariffPublicationVersionResolution(
                            item.PublicationId,
                            group.Key.Effective,
                            item.PublicationId ==
                            preferred.PublicationId
                                ? "VERSION_PREFERRED_RETROACTIVE"
                                : "VERSION_SUPERSEDED_BY_RETROACTIVE",
                            preferred.PublicationId,
                            item.PublicationId ==
                            preferred.PublicationId
                                ? "The publication is explicitly marked retroactive and is the only retroactive publication for this effective period."
                                : $"A corrective publication explicitly marked retroactive exists for the same effective period (publication {preferred.PublicationId})."));
                }

                continue;
            }

            if (retroactive.Length > 1)
            {
                foreach (var item in items)
                {
                    result.Add(
                        new TariffPublicationVersionResolution(
                            item.PublicationId,
                            group.Key.Effective,
                            "VERSION_AMBIGUOUS_MULTIPLE_RETROACTIVE",
                            null,
                            "More than one distinct publication is explicitly marked retroactive for this effective period and no stronger official precedence evidence resolves them."));
                }

                continue;
            }

            if (items.Length == 1)
            {
                result.Add(
                    new TariffPublicationVersionResolution(
                        items[0].PublicationId,
                        group.Key.Effective,
                        "VERSION_SINGLE",
                        items[0].PublicationId,
                        "Only one publication was discovered for this effective period."));
                continue;
            }

            foreach (var item in items)
            {
                result.Add(
                    new TariffPublicationVersionResolution(
                        item.PublicationId,
                        group.Key.Effective,
                        "VERSION_AMBIGUOUS_MULTIPLE_VARIANTS",
                        null,
                        "Multiple non-retroactive publications affect the same parsed effective month; no official correction relation or unique authoritative date establishes precedence."));
            }
        }

        return result
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.PublicationId)
            .ToArray();
    }

    public static bool IsAuthoritativeStatus(
        string status) =>
        status == "VERSION_SINGLE" ||
        status.StartsWith(
            "VERSION_PREFERRED_",
            StringComparison.Ordinal);

    private static bool TryResolveFromExplicitGraph(
        IReadOnlyList<TariffPublication> items,
        IReadOnlyList<TariffPublicationRelation> relations,
        DateOnly effective,
        ICollection<TariffPublicationVersionResolution> output)
    {
        if (items.Count < 2 ||
            relations.Count == 0)
        {
            return false;
        }

        var itemIds =
            items
                .Select(item => item.PublicationId)
                .ToHashSet();

        var itemByOfficialNumber =
            items
                .Where(item =>
                    !string.IsNullOrWhiteSpace(
                        item.OfficialDocumentNumber))
                .GroupBy(
                    item => item.OfficialDocumentNumber!,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.OrdinalIgnoreCase);

        var relevant = relations
            .Where(relation =>
                relation.RelationType is
                    "CORRECTS" or "SUPERSEDES" &&
                itemIds.Contains(
                    relation.SourcePublicationId))
            .ToArray();

        if (relevant.Length == 0)
            return false;

        var supersededIds = new HashSet<long>();
        var sourceIds = new HashSet<long>();

        foreach (var relation in relevant)
        {
            sourceIds.Add(
                relation.SourcePublicationId);

            if (relation.TargetPublicationId.HasValue &&
                itemIds.Contains(
                    relation.TargetPublicationId.Value))
            {
                supersededIds.Add(
                    relation.TargetPublicationId.Value);
                continue;
            }

            if (itemByOfficialNumber.TryGetValue(
                    relation.TargetOfficialDocumentNumber,
                    out var targets))
            {
                foreach (var target in targets)
                {
                    supersededIds.Add(
                        target.PublicationId);
                }
            }
        }

        var terminalSources =
            sourceIds
                .Where(id =>
                    !supersededIds.Contains(id))
                .ToArray();

        if (terminalSources.Length != 1)
            return false;

        var preferredId =
            terminalSources[0];

        foreach (var item in items)
        {
            if (item.PublicationId == preferredId)
            {
                output.Add(
                    new TariffPublicationVersionResolution(
                        item.PublicationId,
                        effective,
                        "VERSION_PREFERRED_OFFICIAL_CORRECTION",
                        preferredId,
                        "Explicit official correction/supersession relations identify this publication as the terminal current document for the effective period."));
            }
            else if (supersededIds.Contains(
                         item.PublicationId))
            {
                output.Add(
                    new TariffPublicationVersionResolution(
                        item.PublicationId,
                        effective,
                        "VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION",
                        preferredId,
                        $"An explicit official correction/supersession relation leads to publication {preferredId}."));
            }
            else
            {
                output.Add(
                    new TariffPublicationVersionResolution(
                        item.PublicationId,
                        effective,
                        "VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION",
                        preferredId,
                        $"Official correction graph establishes publication {preferredId} as current for this effective period."));
            }
        }

        return true;
    }

    private static bool TryResolveEquivalentDuplicateFiles(
        IReadOnlyList<TariffPublication> items,
        DateOnly effective,
        ICollection<TariffPublicationVersionResolution> output)
    {
        if (items.Count < 2 ||
            items.Any(item =>
                string.IsNullOrWhiteSpace(
                    item.ContentSha256)))
        {
            return false;
        }

        var hashes = items
            .Select(item => item.ContentSha256!)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (hashes.Length != 1)
            return false;

        var preferred = items
            .OrderByDescending(item =>
                item.OfficialPublicationDate)
            .ThenBy(item =>
                item.SourceUrl,
                StringComparer.Ordinal)
            .First();

        foreach (var item in items)
        {
            output.Add(
                new TariffPublicationVersionResolution(
                    item.PublicationId,
                    effective,
                    item.PublicationId ==
                    preferred.PublicationId
                        ? "VERSION_PREFERRED_EQUIVALENT_FILE"
                        : "VERSION_EQUIVALENT_DUPLICATE_FILE",
                    preferred.PublicationId,
                    "All competing publications have the same preserved PDF SHA-256; they are byte-identical evidence, not conflicting tariff versions."));
        }

        return true;
    }

    private static bool TryResolveFromOfficialPublicationDate(
        IReadOnlyList<TariffPublication> items,
        DateOnly effective,
        ICollection<TariffPublicationVersionResolution> output)
    {
        if (items.Count < 2 ||
            items.Any(item =>
                !item.OfficialPublicationDate.HasValue))
        {
            return false;
        }

        var latestDate =
            items.Max(item =>
                item.OfficialPublicationDate!.Value);
        var latest =
            items
                .Where(item =>
                    item.OfficialPublicationDate ==
                    latestDate)
                .ToArray();

        if (latest.Length != 1)
            return false;

        // Date alone is authoritative only when at least one competing item
        // explicitly presents itself as corrective/retroactive or declares a
        // corrected official document. This prevents choosing unrelated
        // variants merely because one happened to be issued later.
        if (!latest[0].IsRetroactive &&
            string.IsNullOrWhiteSpace(
                latest[0].CorrectsOfficialDocumentNumber))
        {
            return false;
        }

        var preferred =
            latest[0];

        foreach (var item in items)
        {
            output.Add(
                new TariffPublicationVersionResolution(
                    item.PublicationId,
                    effective,
                    item.PublicationId ==
                    preferred.PublicationId
                        ? "VERSION_PREFERRED_OFFICIAL_DATE"
                        : "VERSION_SUPERSEDED_BY_LATER_OFFICIAL_DATE",
                    preferred.PublicationId,
                    item.PublicationId ==
                    preferred.PublicationId
                        ? $"This corrective publication has the unique latest official issue date {latestDate:yyyy-MM-dd}."
                        : $"A corrective publication with later official issue date {latestDate:yyyy-MM-dd} exists for the same effective period."));
        }

        return true;
    }
}
