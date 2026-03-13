using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ErateWorkbench.Api.Controllers;

/// <summary>
/// Analytics endpoints over the imported E-Rate datasets.
/// All queries are pushed to SQLite via EF Core — no in-memory aggregation.
/// </summary>
[ApiController]
[Route("metrics")]
[Produces("application/json")]
public class MetricsController(
    EpcEntityRepository repo,
    ImportJobService importJobService,
    ServiceProviderRepository spRepo,
    Form471Repository form471Repo,
    FundingCommitmentRepository fundingRepo) : ControllerBase
{
    /// <summary>
    /// Count of EPC entities grouped by state, ordered by count descending.
    /// </summary>
    [HttpGet("entities-by-state")]
    [ProducesResponseType<List<EntityCountByStateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EntitiesByState(CancellationToken ct)
    {
        var rows = await repo.GetCountByStateAsync(ct);
        return Ok(rows.Select(r => new EntityCountByStateDto(r.Item1, r.Item2)).ToList());
    }

    /// <summary>
    /// Count of EPC entities grouped by entity type (School, Library, etc.), ordered by count descending.
    /// </summary>
    [HttpGet("entities-by-type")]
    [ProducesResponseType<List<EntityCountByTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EntitiesByType(CancellationToken ct)
    {
        var rows = await repo.GetCountByTypeAsync(ct);
        return Ok(rows.Select(r => new EntityCountByTypeDto(r.Item1.ToString(), r.Item2)).ToList());
    }

    /// <summary>
    /// Average Category 1 and 2 E-Rate discount rates by state, for entities with non-null rates.
    /// Ordered by average Category 1 rate descending. Use <paramref name="topN"/> to limit results (default 20).
    /// </summary>
    [HttpGet("top-discount-states")]
    [ProducesResponseType<List<DiscountRateByStateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TopDiscountStates([FromQuery] int topN = 20, CancellationToken ct = default)
    {
        var rows = await repo.GetDiscountRatesByStateAsync(topN, ct);
        return Ok(rows.Select(r => new DiscountRateByStateDto(
            r.Item1, r.Item2,
            Math.Round(r.Item3, 2),
            Math.Round(r.Item4, 2)
        )).ToList());
    }

    /// <summary>
    /// Summary of import job history: total, succeeded, and failed job counts,
    /// plus details of the most recent import run.
    /// </summary>
    [HttpGet("import-summary")]
    [ProducesResponseType<ImportSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportSummary(CancellationToken ct)
    {
        var (total, succeeded, failed, last) = await importJobService.GetSummaryAsync(ct);
        return Ok(new ImportSummaryDto(
            total, succeeded, failed,
            last?.StartedAt,
            last?.CompletedAt,
            last?.RecordsProcessed,
            last?.DatasetName
        ));
    }

    /// <summary>
    /// Top service providers by total committed amount, derived from FundingCommitments joined to ServiceProviders on SPIN.
    /// Only providers with at least one linked funding commitment are returned.
    /// Use <paramref name="topN"/> to control the result size (default 20).
    /// </summary>
    [HttpGet("top-service-providers")]
    [ProducesResponseType<List<TopServiceProviderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TopServiceProviders([FromQuery] int topN = 20, CancellationToken ct = default)
    {
        var rows = await spRepo.GetTopByCommittedAmountAsync(topN, ct);
        return Ok(rows.Select(r => new TopServiceProviderDto(r.Spin, r.ProviderName, r.CommitmentCount, r.TotalCommitted)).ToList());
    }

    /// <summary>
    /// Count of distinct service providers and funding commitments per applicant state.
    /// Join chain: FundingCommitments → EpcEntities (on ApplicantEntityNumber = EntityNumber) → PhysicalState.
    /// Only rows with non-null SPIN, ApplicantEntityNumber, and State are included.
    /// </summary>
    [HttpGet("providers-by-state")]
    [ProducesResponseType<List<ProvidersByStateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProvidersByState(CancellationToken ct)
    {
        var rows = await spRepo.GetProvidersByApplicantStateAsync(ct);
        return Ok(rows.Select(r => new ProvidersByStateDto(r.State, r.ProviderCount, r.CommitmentCount)).ToList());
    }

    /// <summary>
    /// Total requested amount and Form 471 application count grouped by funding year, newest year first.
    /// Captures demand-side trends — what applicants requested before USAC review.
    /// </summary>
    [HttpGet("funding-demand-by-year")]
    [ProducesResponseType<List<DemandByYearDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FundingDemandByYear(CancellationToken ct)
    {
        var rows = await form471Repo.GetDemandByYearAsync(ct);
        return Ok(rows.Select(r => new DemandByYearDto(r.FundingYear, r.ApplicationCount, r.TotalRequested)).ToList());
    }

    /// <summary>
    /// Total requested amount and count split by category of service (Category 1 / Category 2).
    /// Category 1 covers connectivity; Category 2 covers internal connections and managed Wi-Fi.
    /// Rows with a null category are excluded.
    /// </summary>
    [HttpGet("category1-vs-category2-demand")]
    [ProducesResponseType<List<CategoryDemandDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CategoryDemand(CancellationToken ct)
    {
        var rows = await form471Repo.GetDemandByCategoryAsync(ct);
        return Ok(rows.Select(r => new CategoryDemandDto(r.Category, r.ApplicationCount, r.TotalRequested)).ToList());
    }

    /// <summary>
    /// Top service types by total requested amount from Form 471 applications.
    /// Use <paramref name="topN"/> to control the result size (default 20).
    /// Rows with a null service type are excluded.
    /// </summary>
    [HttpGet("top-requested-service-types")]
    [ProducesResponseType<List<TopServiceTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TopRequestedServiceTypes([FromQuery] int topN = 20, CancellationToken ct = default)
    {
        var rows = await form471Repo.GetTopServiceTypesAsync(topN, ct);
        return Ok(rows.Select(r => new TopServiceTypeDto(r.ServiceType, r.ApplicationCount, r.TotalRequested)).ToList());
    }

    // --- Cross-dataset analytics (FundingCommitments + EpcEntities) ---

    /// <summary>
    /// Total committed amount and commitment count from FundingCommitments grouped by funding year,
    /// newest year first. This reflects actual USAC-approved commitments, not applicant requests.
    /// No join required — all fields come from FundingCommitments directly.
    /// </summary>
    [HttpGet("funding-by-year")]
    [ProducesResponseType<List<FundingByYearDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FundingByYear(CancellationToken ct)
    {
        var rows = await fundingRepo.GetFundingByYearAsync(ct);
        return Ok(rows.Select(r => new FundingByYearDto(r.FundingYear, r.CommitmentCount, r.TotalCommitted)).ToList());
    }

    /// <summary>
    /// Total committed amount and count split by category of service (Category 1 / Category 2)
    /// from FundingCommitments. Rows with a null category are excluded.
    /// No join required — all fields come from FundingCommitments directly.
    /// </summary>
    [HttpGet("cat1-vs-cat2-funding")]
    [ProducesResponseType<List<FundingByCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cat1VsCat2Funding(CancellationToken ct)
    {
        var rows = await fundingRepo.GetFundingByCategoryAsync(ct);
        return Ok(rows.Select(r => new FundingByCategoryDto(r.Category, r.CommitmentCount, r.TotalCommitted)).ToList());
    }

    /// <summary>
    /// Total committed amount and commitment count grouped by applicant state.
    /// Join: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber → PhysicalState.
    /// Commitments where ApplicantEntityNumber is null or has no matching EpcEntity are excluded.
    /// Ordered by total committed amount descending.
    /// </summary>
    [HttpGet("funding-by-state")]
    [ProducesResponseType<List<FundingByStateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FundingByState(CancellationToken ct)
    {
        var rows = await fundingRepo.GetFundingByStateAsync(ct);
        return Ok(rows.Select(r => new FundingByStateDto(r.State, r.CommitmentCount, r.TotalCommitted)).ToList());
    }

    /// <summary>
    /// Top entities by total committed amount from FundingCommitments.
    /// Join: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber (inner join semantics).
    /// Entities with commitments but no matching EpcEntity record are excluded.
    /// Use <paramref name="topN"/> to control the result size (default 20).
    /// </summary>
    [HttpGet("top-funded-entities")]
    [ProducesResponseType<List<TopFundedEntityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TopFundedEntities([FromQuery] int topN = 20, CancellationToken ct = default)
    {
        var rows = await fundingRepo.GetTopFundedEntitiesAsync(topN, ct);
        return Ok(rows.Select(r => new TopFundedEntityDto(
            r.EntityNumber, r.EntityName, r.EntityType, r.State, r.CommitmentCount, r.TotalCommitted
        )).ToList());
    }

    /// <summary>
    /// Entities with a high E-Rate Category 1 discount rate but below-average committed funding
    /// relative to peers in the same state — a signal of potential under-utilization.
    /// Join: FundingCommitments.ApplicantEntityNumber → EpcEntities.EntityNumber.
    /// Entities in EpcEntities with no commitments are included with CommittedAmount = 0 and will
    /// typically qualify. Entities absent from EpcEntities (no discount rate) are excluded.
    /// Use <paramref name="minDiscountRate"/> to set the discount threshold (default 70).
    /// </summary>
    [HttpGet("high-discount-low-utilization")]
    [ProducesResponseType<List<HighDiscountLowUtilizationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> HighDiscountLowUtilization(
        [FromQuery] decimal minDiscountRate = 70m, CancellationToken ct = default)
    {
        var rows = await fundingRepo.GetHighDiscountLowUtilizationAsync(minDiscountRate, ct);
        return Ok(rows.Select(r => new HighDiscountLowUtilizationDto(
            r.EntityNumber, r.EntityName, r.State,
            r.DiscountRate, r.TotalCommitted, r.StateAvgCommitted
        )).ToList());
    }
}
