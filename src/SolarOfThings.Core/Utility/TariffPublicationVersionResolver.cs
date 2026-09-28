namespace SolarOfThings.Core.Utility;

/// <summary>
/// Resolves only publication-version relationships for one effective period.
/// It does not resolve commune, RED, ETR or customer applicability.
/// </summary>
public sealed class TariffPublicationVersionResolver
{
    public IReadOnlyList<TariffPublicationVersionResolution> Resolve(
        IReadOnlyList<TariffPublication> publications)
    {
        var result = new List<TariffPublicationVersionResolution>();

        foreach (var item in publications.Where(item => !item.EffectiveFrom.HasValue))
        {
            result.Add(new TariffPublicationVersionResolution(
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

            var retroactive = items
                .Where(item => item.IsRetroactive)
                .ToArray();

            if (retroactive.Length == 1)
            {
                var preferred = retroactive[0];
                foreach (var item in items)
                {
                    result.Add(new TariffPublicationVersionResolution(
                        item.PublicationId,
                        group.Key.Effective,
                        item.PublicationId == preferred.PublicationId
                            ? "VERSION_PREFERRED_RETROACTIVE"
                            : "VERSION_SUPERSEDED_BY_RETROACTIVE",
                        preferred.PublicationId,
                        item.PublicationId == preferred.PublicationId
                            ? "The publication is explicitly marked retroactive and is the only retroactive publication for this effective period."
                            : $"A later/corrective publication explicitly marked retroactive exists for the same effective period (publication {preferred.PublicationId})."));
                }

                continue;
            }

            if (retroactive.Length > 1)
            {
                foreach (var item in items)
                {
                    result.Add(new TariffPublicationVersionResolution(
                        item.PublicationId,
                        group.Key.Effective,
                        "VERSION_AMBIGUOUS_MULTIPLE_RETROACTIVE",
                        null,
                        "More than one publication is explicitly marked retroactive for this effective period; version precedence is unresolved."));
                }

                continue;
            }

            if (items.Length == 1)
            {
                result.Add(new TariffPublicationVersionResolution(
                    items[0].PublicationId,
                    group.Key.Effective,
                    "VERSION_SINGLE",
                    items[0].PublicationId,
                    "Only one publication was discovered for this effective period."));
                continue;
            }

            foreach (var item in items)
            {
                result.Add(new TariffPublicationVersionResolution(
                    item.PublicationId,
                    group.Key.Effective,
                    "VERSION_AMBIGUOUS_MULTIPLE_VARIANTS",
                    null,
                    "Multiple non-retroactive publications affect the same parsed effective month; do not choose one automatically."));
            }
        }

        return result
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.PublicationId)
            .ToArray();
    }
}
