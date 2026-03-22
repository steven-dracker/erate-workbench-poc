using ErateWorkbench.Api;
using ErateWorkbench.Domain;
using ErateWorkbench.Infrastructure;
using ErateWorkbench.Infrastructure.Csv;
using ErateWorkbench.Infrastructure.Reconciliation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ─────────────────────────────────────────────────────────────────
// Replace the default console provider with SimpleConsole so every line
// includes a timestamp and level. SingleLine keeps output compact.
// Log levels are controlled via appsettings.json — no code change needed.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "HH:mm:ss ";
    options.SingleLine = true;
});

builder.Services.AddMemoryCache();
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
builder.Services.AddScoped<EntityCsvParser>();
builder.Services.AddScoped<EntityRepository>();
builder.Services.AddScoped<EntityImportService>();
builder.Services.AddScoped<DisbursementCsvParser>();
builder.Services.AddScoped<DisbursementRepository>();
builder.Services.AddScoped<DisbursementImportService>();
builder.Services.AddScoped<ConsultantApplicationCsvParser>();
builder.Services.AddScoped<ConsultantApplicationRepository>();
builder.Services.AddScoped<ConsultantApplicationImportService>();
builder.Services.AddScoped<ConsultantFrnStatusCsvParser>();
builder.Services.AddScoped<ConsultantFrnStatusRepository>();
builder.Services.AddScoped<ConsultantFrnStatusImportService>();
builder.Services.AddScoped<AnalyticsRepository>();
builder.Services.AddScoped<RiskInsightsRepository>();
builder.Services.AddScoped<FilingWindowRepository>();

// Reconciliation
builder.Services.AddHttpClient<SocrataReconciliationService>();
builder.Services.AddScoped<FundingCommitmentLocalDataProvider>();
builder.Services.AddScoped<DisbursementLocalDataProvider>();
builder.Services.AddScoped<FundingCommitmentSummaryLocalProvider>();
builder.Services.AddScoped<ReconciliationReportWriter>();

// Summary builders
builder.Services.AddScoped<ApplicantYearCommitmentSummaryBuilder>();
builder.Services.AddScoped<ApplicantYearDisbursementSummaryBuilder>();
builder.Services.AddScoped<DisbursementSummaryLocalProvider>();
builder.Services.AddScoped<ApplicantYearRiskSummaryBuilder>();

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

// --- Health ---

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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
        baseUrl: request?.DatasetUrl ?? "https://datahub.usac.org/resource/avi8-svp9.csv",
        cancellationToken: ct);

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

app.MapPost("/import/entities", async (
    EntityImportRequest? request,
    EntityImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        baseUrl: request?.DatasetUrl ?? "https://datahub.usac.org/resource/avi8-svp9.csv",
        cancellationToken: ct);

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
.WithName("TriggerEntityImport")
.WithSummary("Trigger ingestion of USAC E-Rate ROS entity data from the funding commitments dataset")
.WithOpenApi();

app.MapPost("/import/form471", async (
    Form471ImportRequest? request,
    Form471ImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        datasetUrl: request?.DatasetUrl,
        fundingYear: request?.FundingYear,
        cancellationToken: ct);

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

app.MapPost("/import/disbursements", async (
    DisbursementImportRequest? request,
    DisbursementImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        baseUrl: request?.DatasetUrl ?? "https://datahub.usac.org/resource/jpiu-tj8h.csv",
        cancellationToken: ct);

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
.WithName("TriggerDisbursementImport")
.WithSummary("Trigger ingestion of the USAC E-Rate Invoices and Authorized Disbursements dataset")
.WithOpenApi();

app.MapPost("/import/consultants/applications", async (
    ConsultantApplicationImportRequest? request,
    ConsultantApplicationImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        baseUrl: request?.DatasetUrl,
        cancellationToken: ct);

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
.WithName("TriggerConsultantApplicationImport")
.WithSummary("Trigger ingestion of the USAC Form 471 Consultants dataset (x5px-esft)")
.WithOpenApi();

app.MapPost("/import/consultants/frn-status", async (
    ConsultantFrnStatusImportRequest? request,
    ConsultantFrnStatusImportService importService,
    CancellationToken ct) =>
{
    var result = await importService.RunAsync(
        baseUrl: request?.DatasetUrl,
        cancellationToken: ct);

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
.WithName("TriggerConsultantFrnStatusImport")
.WithSummary("Trigger ingestion of the USAC Consultant FRN Status dataset (mihb-jfex)")
.WithOpenApi();

// --- Summary builder endpoints (dev) ---

app.MapPost("/dev/summary/risk", async (
    int? year,
    ApplicantYearRiskSummaryBuilder builder,
    CancellationToken ct) =>
{
    var result = await builder.RebuildAsync(fundingYear: year, cancellationToken: ct);
    return Results.Ok(result);
})
.WithName("RebuildRiskSummary")
.WithSummary("(Dev) Rebuild ApplicantYearRiskSummary by merging commitment and disbursement summaries. " +
             "Pass ?year=2021 to rebuild a single year.")
.WithOpenApi();

app.MapPost("/dev/summary/disbursements", async (
    int? year,
    ApplicantYearDisbursementSummaryBuilder builder,
    CancellationToken ct) =>
{
    var result = await builder.RebuildAsync(fundingYear: year, cancellationToken: ct);
    return Results.Ok(result);
})
.WithName("RebuildDisbursementSummary")
.WithSummary("(Dev) Rebuild ApplicantYearDisbursementSummary from raw Disbursements (ApprovedAmount > 0). " +
             "Pass ?year=2021 to rebuild a single year.")
.WithOpenApi();

app.MapPost("/dev/summary/funding-commitments", async (
    int? year,
    ApplicantYearCommitmentSummaryBuilder builder,
    CancellationToken ct) =>
{
    var result = await builder.RebuildAsync(fundingYear: year, cancellationToken: ct);
    return Results.Ok(result);
})
.WithName("RebuildFundingCommitmentSummary")
.WithSummary("(Dev) Rebuild ApplicantYearCommitmentSummary from raw FundingCommitments. " +
             "Pass ?year=2021 to rebuild a single year.")
.WithOpenApi();

// --- Reconciliation endpoints (dev/validation) ---

app.MapPost("/dev/reconcile/funding-commitments", async (
    SocrataReconciliationService reconciler,
    FundingCommitmentLocalDataProvider localProvider,
    FundingCommitmentSummaryLocalProvider summaryProvider,
    ReconciliationReportWriter writer,
    CancellationToken ct) =>
{
    var result = await reconciler.ReconcileAsync(
        DatasetManifests.FundingCommitments, localProvider, summaryProvider, ct);
    var dir    = Path.Combine(Directory.GetCurrentDirectory(), "reports");
    var stamp  = result.RunAtUtc.ToString("yyyyMMdd-HHmmss");
    await writer.WriteMarkdownAsync(result, Path.Combine(dir, $"reconcile-FundingCommitments-{stamp}.md"), ct);
    await writer.WriteJsonAsync(result,     Path.Combine(dir, $"reconcile-FundingCommitments-{stamp}.json"), ct);
    return Results.Ok(result);
})
.WithName("ReconcileFundingCommitments")
.WithSummary("(Dev) Compare local FundingCommitments table against USAC Socrata source — writes report to /reports/")
.WithOpenApi();

app.MapPost("/dev/reconcile/disbursements", async (
    SocrataReconciliationService reconciler,
    DisbursementLocalDataProvider localProvider,
    DisbursementSummaryLocalProvider summaryProvider,
    ReconciliationReportWriter writer,
    CancellationToken ct) =>
{
    var result = await reconciler.ReconcileAsync(DatasetManifests.Disbursements, localProvider, summaryProvider, ct);
    var dir    = Path.Combine(Directory.GetCurrentDirectory(), "reports");
    var stamp  = result.RunAtUtc.ToString("yyyyMMdd-HHmmss");
    await writer.WriteMarkdownAsync(result, Path.Combine(dir, $"reconcile-Disbursements-{stamp}.md"), ct);
    await writer.WriteJsonAsync(result,     Path.Combine(dir, $"reconcile-Disbursements-{stamp}.json"), ct);
    return Results.Ok(result);
})
.WithName("ReconcileDisbursements")
.WithSummary("(Dev) Compare local Disbursements table against USAC Socrata source — writes report to /reports/")
.WithOpenApi();

// --- Analytics endpoints ---

app.MapGet("/analytics/commitment-vs-disbursement", async (
    AnalyticsRepository analytics,
    CancellationToken ct) =>
{
    var rows = await analytics.GetCommitmentVsDisbursementByYearAsync(ct);
    return Results.Ok(rows);
})
.WithName("GetCommitmentVsDisbursementByYear")
.WithSummary("Committed vs disbursed (approved) amounts grouped by funding year")
.WithOpenApi();

app.MapGet("/analytics/top-applicants", async (
    int? top,
    AnalyticsRepository analytics,
    CancellationToken ct) =>
{
    var rows = await analytics.GetTopApplicantsAsync(Math.Clamp(top ?? 20, 1, 100), ct);
    return Results.Ok(rows);
})
.WithName("GetTopApplicants")
.WithSummary("Top applicant entities by total committed amount, with disbursements")
.WithOpenApi();

app.MapGet("/analytics/top-providers", async (
    int? top,
    AnalyticsRepository analytics,
    CancellationToken ct) =>
{
    var rows = await analytics.GetTopServiceProvidersAsync(Math.Clamp(top ?? 20, 1, 100), ct);
    return Results.Ok(rows);
})
.WithName("GetTopProviders")
.WithSummary("Top service providers by total committed amount, with disbursements")
.WithOpenApi();

app.MapGet("/analytics/rural-urban-summary", async (
    AnalyticsRepository analytics,
    CancellationToken ct) =>
{
    var rows = await analytics.GetRuralUrbanSummaryAsync(ct);
    return Results.Ok(rows);
})
.WithName("GetRuralUrbanSummary")
.WithSummary("Funding commitments and disbursements grouped by rural/urban status of the applicant entity")
.WithOpenApi();

app.MapGet("/analytics/funding-per-student", async (
    int? top,
    AnalyticsRepository analytics,
    CancellationToken ct) =>
{
    var rows = await analytics.GetFundingPerStudentAsync(Math.Clamp(top ?? 50, 1, 200), ct);
    return Results.Ok(rows);
})
.WithName("GetFundingPerStudent")
.WithSummary("Committed funding per student for entities with student counts in the Entities table")
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
record Form471ImportRequest(string? DatasetUrl, int? FundingYear);
record EntityImportRequest(string? DatasetUrl);
record DisbursementImportRequest(string? DatasetUrl);
record ConsultantApplicationImportRequest(string? DatasetUrl);
record ConsultantFrnStatusImportRequest(string? DatasetUrl);
