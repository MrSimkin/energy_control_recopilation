namespace SolarOfThings.Core.Utility;

public sealed class EnelTariffNormalizationService
{
    private readonly TariffRateCandidateRepository _candidateRepository;
    private readonly EnelBt1TariffTextParser _parser;

    public EnelTariffNormalizationService(
        TariffRateCandidateRepository candidateRepository,
        EnelBt1TariffTextParser parser)
    {
        _candidateRepository = candidateRepository;
        _parser = parser;
    }

    public TariffNormalizationResult NormalizePublication(
        long publicationId,
        IReadOnlyList<string> pageTexts)
    {
        try
        {
            var candidates = _parser.ParsePages(pageTexts);
            _candidateRepository.ReplaceForPublication(
                publicationId,
                candidates,
                EnelBt1TariffTextParser.ParserVersion);

            var pagesWithCandidates = candidates
                .Select(item => item.PageNumber)
                .Distinct()
                .Count();

            return new TariffNormalizationResult(
                publicationId,
                candidates.Count,
                pagesWithCandidates,
                EnelBt1TariffTextParser.ParserVersion);
        }
        catch (Exception ex)
        {
            _candidateRepository.MarkFailed(
                publicationId,
                EnelBt1TariffTextParser.ParserVersion,
                ex.Message);
            throw;
        }
    }
}
