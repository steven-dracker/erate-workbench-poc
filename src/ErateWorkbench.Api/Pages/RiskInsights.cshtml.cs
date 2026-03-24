using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class RiskInsightsModel(RiskInsightsRepository repo) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Severity { get; set; }

    public List<int> AvailableYears { get; private set; } = [];
    public bool HasFilters => Year.HasValue || !string.IsNullOrEmpty(Severity);

    public bool ShowPartialYearBanner { get; private set; }
    public string PartialYearMessage { get; private set; } = string.Empty;

    public RiskSnapshot Snapshot { get; private set; } = new(0, 0, 0, 0, 0);
    public List<ApplicantRiskRow> TopRiskApplicants { get; private set; } = [];
    public List<CommitmentGapRow> TopGaps { get; private set; } = [];
    public List<ReductionRateRow> TopReductions { get; private set; } = [];
    public List<AdvisorySignalDto> AdvisorySignals { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        AvailableYears     = await repo.GetAvailableYearsAsync(ct);
        Snapshot           = await repo.GetSnapshotAsync(Year, ct);
        TopRiskApplicants  = await repo.GetTopRiskApplicantsAsync(20, Year, Severity, ct);
        TopGaps            = await repo.GetTopCommitmentDisbursementGapsAsync(15, Year, ct);
        TopReductions      = await repo.GetTopReductionRatesAsync(15, Year, ct);
        AdvisorySignals    = await repo.GetAdvisorySignalsAsync(Year, Severity, 25, ct);

        ShowPartialYearBanner = PartialYearDetector.IsPartialYear(AvailableYears, Year);
        if (ShowPartialYearBanner)
            PartialYearMessage = $"FY{Year} is a partial year — advisory signals are preliminary and may overstate anomalies.";
    }
}
