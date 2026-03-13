using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class IndexModel(EpcEntityRepository repo, ImportJobService importJobService) : PageModel
{
    public int TotalEntities { get; private set; }
    public int ActiveEntities { get; private set; }
    public int StateCount { get; private set; }
    public DateTime? LastImport { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var (_, total) = await repo.SearchAsync(null, null, null, null, 0, 1, ct);
        var (_, active) = await repo.SearchAsync(null, null, null, "Active", 0, 1, ct);
        var states = await repo.GetCountByStateAsync(ct);

        TotalEntities = total;
        ActiveEntities = active;
        StateCount = states.Count(r => !string.IsNullOrEmpty(r.Item1));

        var (_, _, _, last) = await importJobService.GetSummaryAsync(ct);
        LastImport = last?.CompletedAt;
    }
}
