namespace ErateWorkbench.Domain;

/// <summary>
/// Pre-aggregated commitment totals per applicant entity per funding year.
/// Built by <c>ApplicantYearCommitmentSummaryBuilder</c> from the raw FundingCommitments table.
/// One row per (FundingYear, ApplicantEntityNumber) pair.
/// Not used by dashboards yet — exists for reconciliation and future analytics.
/// </summary>
public class ApplicantYearCommitmentSummary
{
    public int Id { get; set; }

    public int FundingYear { get; set; }

    /// <summary>BEN — may be null for raw rows that had no entity number.</summary>
    public string? ApplicantEntityNumber { get; set; }

    /// <summary>Deterministic display name derived from raw rows (MIN of ApplicantName).</summary>
    public string? ApplicantEntityName { get; set; }

    /// <summary>SUM of TotalEligibleAmount across all raw rows for this (year, BEN).</summary>
    public decimal TotalEligibleAmount { get; set; }

    /// <summary>SUM of CommittedAmount across all raw rows for this (year, BEN).</summary>
    public decimal TotalCommittedAmount { get; set; }

    /// <summary>COUNT(*) of raw FundingCommitment rows for this (year, BEN).</summary>
    public int CommitmentRowCount { get; set; }

    /// <summary>COUNT(DISTINCT FundingRequestNumber) for this (year, BEN).</summary>
    public int DistinctFrnCount { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
