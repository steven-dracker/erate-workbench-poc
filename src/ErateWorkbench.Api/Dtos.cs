using ErateWorkbench.Domain;

namespace ErateWorkbench.Api;

public record EpcEntityDto(
    string EntityNumber,
    string EntityName,
    string EntityType,
    string? Status,
    string? ParentEntityNumber,
    string? ParentEntityName,
    string? PhysicalAddress,
    string? PhysicalCity,
    string? PhysicalCounty,
    string? PhysicalState,
    string? PhysicalZip,
    string? Phone,
    string? Email,
    string? Website,
    double? Latitude,
    double? Longitude,
    string? UrbanRuralStatus,
    decimal? CategoryOneDiscountRate,
    decimal? CategoryTwoDiscountRate,
    string? LocaleCode,
    int? StudentCount,
    string? FccRegistrationNumber
)
{
    public static EpcEntityDto From(EpcEntity e) => new(
        e.EntityNumber,
        e.EntityName,
        e.EntityType.ToString(),
        e.Status,
        e.ParentEntityNumber,
        e.ParentEntityName,
        e.PhysicalAddress,
        e.PhysicalCity,
        e.PhysicalCounty,
        e.PhysicalState,
        e.PhysicalZip,
        e.Phone,
        e.Email,
        e.Website,
        e.Latitude,
        e.Longitude,
        e.UrbanRuralStatus,
        e.CategoryOneDiscountRate,
        e.CategoryTwoDiscountRate,
        e.LocaleCode,
        e.StudentCount,
        e.FccRegistrationNumber
    );
}

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public record EntityCountByStateDto(string State, int Count);

public record EntityCountByTypeDto(string EntityType, int Count);

public record DiscountRateByStateDto(
    string State,
    int EntityCount,
    double AvgCategoryOneRate,
    double AvgCategoryTwoRate
);

public record ImportSummaryDto(
    int TotalJobs,
    int SucceededJobs,
    int FailedJobs,
    DateTime? LastImportStartedAt,
    DateTime? LastImportCompletedAt,
    int? LastImportRecordsProcessed,
    string? LastImportDataset
);

public record TopServiceProviderDto(
    string Spin,
    string ProviderName,
    int CommitmentCount,
    decimal TotalCommitted
);

public record ProvidersByStateDto(
    string State,
    int ProviderCount,
    int CommitmentCount
);

public record DemandByYearDto(int FundingYear, int ApplicationCount, decimal TotalRequested);

public record CategoryDemandDto(string Category, int ApplicationCount, decimal TotalRequested);

public record TopServiceTypeDto(string ServiceType, int ApplicationCount, decimal TotalRequested);

public record FundingByYearDto(int FundingYear, int CommitmentCount, decimal TotalCommitted);

public record FundingByCategoryDto(string Category, int CommitmentCount, decimal TotalCommitted);

public record FundingByStateDto(string State, int CommitmentCount, decimal TotalCommitted);

public record TopFundedEntityDto(
    string EntityNumber,
    string EntityName,
    string EntityType,
    string? State,
    int CommitmentCount,
    decimal TotalCommitted
);

public record HighDiscountLowUtilizationDto(
    string EntityNumber,
    string EntityName,
    string State,
    decimal DiscountRate,
    decimal TotalCommitted,
    decimal StateAvgCommitted
);

public record FundingImportResultDto(
    int RecordsProcessed,
    int RecordsInserted,
    int RecordsUpdated,
    int RecordsFailed,
    string Duration,
    string DatasetName,
    string Status,
    string? ErrorMessage
);
