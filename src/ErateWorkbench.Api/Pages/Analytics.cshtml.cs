using System.Text.Json;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class AnalyticsModel(
    EpcEntityRepository repo,
    ImportJobService importJobService,
    FundingCommitmentRepository fundingRepo) : PageModel
{
    public string EntitiesByStateJson { get; private set; } = "[]";
    public string EntitiesByTypeJson { get; private set; } = "[]";
    public string TopDiscountStatesJson { get; private set; } = "[]";
    public string FundingByYearJson { get; private set; } = "[]";
    public string FundingByCategoryJson { get; private set; } = "[]";
    public string TopFundedEntitiesJson { get; private set; } = "[]";
    public ImportSummaryDto Summary { get; private set; } = new(0, 0, 0, null, null, null, null);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var byState = await repo.GetCountByStateAsync(ct);
        var byType = await repo.GetCountByTypeAsync(ct);
        var discount = await repo.GetDiscountRatesByStateAsync(20, ct);
        var (total, succeeded, failed, last) = await importJobService.GetSummaryAsync(ct);
        var fundingByYear = await fundingRepo.GetFundingByYearAsync(ct);
        var fundingByCategory = await fundingRepo.GetFundingByCategoryAsync(ct);
        var topEntities = await fundingRepo.GetTopFundedEntitiesAsync(10, ct);

        EntitiesByStateJson = JsonSerializer.Serialize(
            byState.Take(15)
                   .Where(r => !string.IsNullOrEmpty(r.Item1))
                   .Select(r => new { state = r.Item1, count = r.Item2 }));

        EntitiesByTypeJson = JsonSerializer.Serialize(
            byType.Select(r => new { type = r.Item1.ToString(), count = r.Item2 }));

        TopDiscountStatesJson = JsonSerializer.Serialize(
            discount.Select(r => new
            {
                state = r.Item1,
                avgCat1 = Math.Round(r.Item3, 1),
                avgCat2 = Math.Round(r.Item4, 1),
            }));

        FundingByYearJson = JsonSerializer.Serialize(
            fundingByYear.Select(r => new { year = r.FundingYear, total = r.TotalCommitted, count = r.CommitmentCount }));

        FundingByCategoryJson = JsonSerializer.Serialize(
            fundingByCategory.Select(r => new { category = r.Category, total = r.TotalCommitted }));

        TopFundedEntitiesJson = JsonSerializer.Serialize(
            topEntities.Select(r => new { name = r.EntityName, state = r.State ?? "—", total = r.TotalCommitted }));

        Summary = new ImportSummaryDto(
            total, succeeded, failed,
            last?.StartedAt, last?.CompletedAt,
            last?.RecordsProcessed, last?.DatasetName);
    }
}
