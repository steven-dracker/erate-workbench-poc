namespace ErateWorkbench.Domain;

/// <summary>
/// Pre-aggregated disbursement totals per applicant entity per funding year.
/// Built by <c>ApplicantYearDisbursementSummaryBuilder</c> from the raw Disbursements table.
/// One row per (FundingYear, ApplicantEntityNumber) pair.
///
/// Inclusion rule: only disbursement rows where ApprovedAmount > 0 are counted.
/// All InvoiceLineStatus values are included (no status filter).
/// </summary>
public class ApplicantYearDisbursementSummary
{
    public int Id { get; set; }

    public int FundingYear { get; set; }

    /// <summary>BEN — may be null for raw rows that had no entity number.</summary>
    public string? ApplicantEntityNumber { get; set; }

    /// <summary>Deterministic display name derived from raw rows (MIN of ApplicantEntityName).</summary>
    public string? ApplicantEntityName { get; set; }

    /// <summary>SUM of RequestedAmount for included rows (ApprovedAmount > 0).</summary>
    public decimal TotalRequestedAmount { get; set; }

    /// <summary>SUM of ApprovedAmount for included rows (ApprovedAmount > 0).</summary>
    public decimal TotalApprovedAmount { get; set; }

    /// <summary>COUNT of disbursement rows where ApprovedAmount > 0 for this (year, BEN).</summary>
    public int DisbursementRowCount { get; set; }

    /// <summary>COUNT(DISTINCT FundingRequestNumber) for included rows.</summary>
    public int DistinctFrnCount { get; set; }

    /// <summary>COUNT(DISTINCT InvoiceId) for included rows.</summary>
    public int DistinctInvoiceCount { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
