using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class SearchModel(EpcEntityRepository repo) : PageModel
{
    private const int PageSize = 25;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? State { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNum { get; set; } = 1;

    public List<EpcEntity> Items { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public int CurrentPage { get; private set; }
    public List<string> States { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        States = (await repo.GetCountByStateAsync(ct))
            .Select(r => r.Item1)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        EpcEntityType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(EntityType) &&
            Enum.TryParse<EpcEntityType>(EntityType, ignoreCase: true, out var t))
            parsedType = t;

        CurrentPage = Math.Max(1, PageNum);
        var skip = (CurrentPage - 1) * PageSize;

        (Items, TotalCount) = await repo.SearchAsync(Search, State, parsedType, Status, skip, PageSize, ct);
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
