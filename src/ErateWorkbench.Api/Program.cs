using ErateWorkbench.Api;
using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Csv;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(
        Path.Combine(AppContext.BaseDirectory, "ErateWorkbench.Api.xml"),
        includeControllerXmlComments: true);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=erate-workbench.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHttpClient<UsacCsvClient>();
builder.Services.AddScoped<ApplicantCsvParser>();
builder.Services.AddScoped<ApplicantRepository>();
builder.Services.AddScoped<ImportJobService>();
builder.Services.AddScoped<EpcEntityCsvParser>();
builder.Services.AddScoped<EpcEntityRepository>();
builder.Services.AddScoped<EpcEntityImportService>();
builder.Services.AddScoped<FundingCommitmentCsvParser>();
builder.Services.AddScoped<FundingCommitmentRepository>();
builder.Services.AddScoped<FundingCommitmentImportService>();
builder.Services.AddScoped<ServiceProviderCsvParser>();
builder.Services.AddScoped<ServiceProviderRepository>();
builder.Services.AddScoped<ServiceProviderImportService>();
builder.Services.AddScoped<Form471CsvParser>();
builder.Services.AddScoped<Form471Repository>();
builder.Services.AddScoped<Form471ImportService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.MapControllers();
app.MapRazorPages();

app.UseHttpsRedirection();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// --- Import endpoints ---

app.MapPost("/imports", async (ImportRequest request, ImportJobService importJobService, CancellationToken ct) =>
{
    var job = await importJobService.RunApplicantImportAsync(request.DatasetUrl, request.FundingYear, ct);
    return Results.Ok(job);
})
.WithName("TriggerImport")
.WithOpenApi();

app.MapGet("/imports/{id:int}", async (int id, ImportJobService importJobService, CancellationToken ct) =>
{
    var job = await importJobService.GetJobAsync(id, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
})
.WithName("GetImportJob")
.WithOpenApi();

app.MapGet("/imports", (ImportJobService importJobService) =>
{
    return Results.Ok(importJobService.QueryJobs().Take(50).ToList());
})
.WithName("ListImportJobs")
.WithOpenApi();

// --- EPC Entity endpoints ---

app.MapPost("/import/usac", async (EpcEntityImportRequest? request, EpcEntityImportService importService, CancellationToken ct) =>
{
    var job = await importService.RunAsync(
        request?.DatasetUrl ?? "https://datahub.usac.org/api/views/7i5i-83qf/rows.csv?accessType=DOWNLOAD",
        ct);
    return Results.Ok(job);
})
.WithName("TriggerUsacEntityImport")
.WithSummary("Trigger ingestion of the USAC E-Rate Supplemental Entity Information dataset")
.WithOpenApi();

app.MapPost("/import/funding-commitments", async (
    FundingCommitmentsImportRequest? request,
    FundingCommitmentImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        request?.DatasetUrl ?? "https://datahub.usac.org/api/views/i5j4-3rvr/rows.csv?accessType=DOWNLOAD",
        ct);

    var dto = new FundingImportResultDto(
        result.RecordsProcessed,
        result.RecordsInserted,
        result.RecordsUpdated,
        result.RecordsFailed,
        result.Duration.ToString(@"hh\:mm\:ss"),
        result.DatasetName,
        result.Status.ToString(),
        result.ErrorMessage);

    return Results.Ok(dto);
})
.WithName("TriggerFundingCommitmentsImport")
.WithSummary("Trigger ingestion of the USAC E-Rate Funding Request Commitments dataset")
.WithOpenApi();

app.MapPost("/import/service-providers", async (
    ServiceProviderImportRequest? request,
    ServiceProviderImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        request?.DatasetUrl ?? "https://datahub.usac.org/api/views/s8d5-n6qr/rows.csv?accessType=DOWNLOAD",
        ct);

    return Results.Ok(new FundingImportResultDto(
        result.RecordsProcessed,
        result.RecordsInserted,
        result.RecordsUpdated,
        result.RecordsFailed,
        result.Duration.ToString(@"hh\:mm\:ss"),
        result.DatasetName,
        result.Status.ToString(),
        result.ErrorMessage));
})
.WithName("TriggerServiceProviderImport")
.WithSummary("Trigger ingestion of the USAC E-Rate Service Provider (SPIN) dataset")
.WithOpenApi();

app.MapPost("/import/form471", async (
    Form471ImportRequest? request,
    Form471ImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        request?.DatasetUrl ?? "https://datahub.usac.org/api/views/9s85-xeem/rows.csv?accessType=DOWNLOAD",
        ct);

    return Results.Ok(new FundingImportResultDto(
        result.RecordsProcessed,
        result.RecordsInserted,
        result.RecordsUpdated,
        result.RecordsFailed,
        result.Duration.ToString(@"hh\:mm\:ss"),
        result.DatasetName,
        result.Status.ToString(),
        result.ErrorMessage));
})
.WithName("TriggerForm471Import")
.WithSummary("Trigger ingestion of the USAC FCC Form 471 application dataset")
.WithOpenApi();

app.MapGet("/entities", async (
    string? search,
    string? state,
    string? entityType,
    string? status,
    int? page,
    int? pageSize,
    EpcEntityRepository repo,
    CancellationToken ct) =>
{
    var resolvedPage = Math.Max(1, page ?? 1);
    var resolvedPageSize = Math.Clamp(pageSize ?? 25, 1, 100);

    EpcEntityType? parsedType = null;
    if (!string.IsNullOrWhiteSpace(entityType) &&
        Enum.TryParse<EpcEntityType>(entityType, ignoreCase: true, out var t))
        parsedType = t;

    var (items, totalCount) = await repo.SearchAsync(
        search, state, parsedType, status,
        skip: (resolvedPage - 1) * resolvedPageSize,
        take: resolvedPageSize,
        cancellationToken: ct);

    var result = new PagedResult<EpcEntityDto>(
        items.Select(EpcEntityDto.From).ToList(),
        totalCount,
        resolvedPage,
        resolvedPageSize);

    return Results.Ok(result);
})
.WithName("SearchEntities")
.WithOpenApi();

app.MapGet("/entities/{entityNumber}", async (string entityNumber, EpcEntityRepository repo, CancellationToken ct) =>
{
    var entity = await repo.FindByEntityNumberAsync(entityNumber, ct);
    return entity is null ? Results.NotFound() : Results.Ok(EpcEntityDto.From(entity));
})
.WithName("GetEntityByNumber")
.WithOpenApi();

// --- Applicant endpoints ---

app.MapGet("/applicants", (
    string? name,
    string? state,
    string? entityType,
    int? fundingYear,
    ApplicantRepository repo) =>
{
    var query = repo.Query();

    if (!string.IsNullOrWhiteSpace(name))
        query = query.Where(a => a.Name.Contains(name));

    if (!string.IsNullOrWhiteSpace(state))
        query = query.Where(a => a.State == state.ToUpperInvariant());

    if (fundingYear.HasValue)
        query = query.Where(a => a.FundingYear == fundingYear.Value);

    if (!string.IsNullOrWhiteSpace(entityType) &&
        Enum.TryParse<ErateWorkbench.Domain.ApplicantEntityType>(entityType, ignoreCase: true, out var parsed))
        query = query.Where(a => a.EntityType == parsed);

    return Results.Ok(query.Take(200).ToList());
})
.WithName("SearchApplicants")
.WithOpenApi();

app.MapGet("/applicants/{ben}", (string ben, int? fundingYear, ApplicantRepository repo) =>
{
    var query = repo.Query().Where(a => a.Ben == ben);
    if (fundingYear.HasValue)
        query = query.Where(a => a.FundingYear == fundingYear.Value);
    var results = query.ToList();
    return results.Count == 0 ? Results.NotFound() : Results.Ok(results);
})
.WithName("GetApplicantByBen")
.WithOpenApi();

app.Run();

record ImportRequest(string DatasetUrl, int FundingYear);
record EpcEntityImportRequest(string? DatasetUrl);
record FundingCommitmentsImportRequest(string? DatasetUrl);
record ServiceProviderImportRequest(string? DatasetUrl);
record Form471ImportRequest(string? DatasetUrl);
