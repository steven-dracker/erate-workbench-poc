using System.Text.Json;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class ConsultantDetailModel(
    ConsultantAnalyticsService analytics,
    ILogger<ConsultantDetailModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [BindProperty(SupportsGet = true)]
    public string? EpcId { get; set; }

    public ConsultantDetailDto? Detail { get; private set; }

    public string TrendsJson { get; private set; } = "[]";
    public string StateBreakdownJson { get; private set; } = "[]";
    public string ServiceTypesJson { get; private set; } = "[]";

    public bool IsERateCentral =>
        Detail?.ConsultantEpcOrganizationId == ConsultantConstants.ERateCentralEpcId;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(EpcId))
            return RedirectToPage("/ConsultantIntelligence");

        Detail = await analytics.GetConsultantDetailsAsync(EpcId, ct);
        if (Detail is null)
        {
            logger.LogWarning("ConsultantDetail requested for unknown EPC ID {EpcId}", EpcId);
            return NotFound();
        }

        TrendsJson = JsonSerializer.Serialize(Detail.Trends, JsonOpts);
        StateBreakdownJson = JsonSerializer.Serialize(Detail.StateBreakdown, JsonOpts);
        ServiceTypesJson = JsonSerializer.Serialize(Detail.ServiceTypes, JsonOpts);

        return Page();
    }
}
