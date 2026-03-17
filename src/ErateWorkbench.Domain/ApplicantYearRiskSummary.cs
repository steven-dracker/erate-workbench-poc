namespace ErateWorkbench.Domain;

/// <summary>
/// Pre-computed risk analytics row merging commitment and disbursement summary data
/// at the grain: (FundingYear, ApplicantEntityNumber).
///
/// Source tables (never raw tables):
///   - ApplicantYearCommitmentSummaries
///   - ApplicantYearDisbursementSummaries
///
/// Merge semantics: full outer join on (FundingYear, ApplicantEntityNumber).
/// Rows that appear on only one side are preserved with zero-filled absent fields
/// and the corresponding presence flag set to false.
///
/// Derived metric rules (consistent with RiskCalculator):
///   ReductionPct  = (TotalEligibleAmount − TotalCommittedAmount) / TotalEligibleAmount
///                   0 when TotalEligibleAmount ≤ 0 (no commitment data)
///
///   DisbursementPct = TotalApprovedDisbursementAmount / TotalCommittedAmount
///                     0 when TotalCommittedAmount ≤ 0 (no commitment baseline)
///
///   RiskScore = 0.5 × ReductionPct + 0.5 × (1 − DisbursementPct)
///   RiskLevel  = High (> 0.6) | Moderate (0.3–0.6) | Low (< 0.3)
///
/// Fallback behaviour for one-sided rows:
///   CommitmentOnly (HasDisbursementData = false):
///     DisbursementPct = 0  → execution risk = 100%
///     RiskScore ≥ 0.5 (disbursement gap fully penalised)
///
///   DisbursementOnly (HasCommitmentData = false):
///     ReductionPct = 0, DisbursementPct = 0
///     RiskScore = 0.5 → classified Moderate (insufficient baseline)
/// </summary>
public class ApplicantYearRiskSummary
{
    public int Id { get; set; }

    public int FundingYear { get; set; }
    public string? ApplicantEntityNumber { get; set; }
    public string? ApplicantEntityName { get; set; }

    // ── Commitment fields ────────────────────────────────────────────────────

    /// <summary>Sum of TotalEligibleAmount from ApplicantYearCommitmentSummaries.</summary>
    public decimal TotalEligibleAmount { get; set; }

    /// <summary>Sum of TotalCommittedAmount from ApplicantYearCommitmentSummaries.</summary>
    public decimal TotalCommittedAmount { get; set; }

    /// <summary>CommitmentRowCount from ApplicantYearCommitmentSummaries (raw row count, not FRN count).</summary>
    public int CommitmentRowCount { get; set; }

    /// <summary>DistinctFrnCount from ApplicantYearCommitmentSummaries.</summary>
    public int DistinctCommitmentFrnCount { get; set; }

    // ── Disbursement fields ──────────────────────────────────────────────────

    /// <summary>Sum of TotalRequestedAmount from ApplicantYearDisbursementSummaries.</summary>
    public decimal TotalRequestedDisbursementAmount { get; set; }

    /// <summary>Sum of TotalApprovedAmount from ApplicantYearDisbursementSummaries (ApprovedAmount > 0 rows only).</summary>
    public decimal TotalApprovedDisbursementAmount { get; set; }

    /// <summary>DisbursementRowCount from ApplicantYearDisbursementSummaries.</summary>
    public int DisbursementRowCount { get; set; }

    /// <summary>DistinctFrnCount from ApplicantYearDisbursementSummaries.</summary>
    public int DistinctDisbursementFrnCount { get; set; }

    /// <summary>DistinctInvoiceCount from ApplicantYearDisbursementSummaries.</summary>
    public int DistinctInvoiceCount { get; set; }

    // ── Presence flags ───────────────────────────────────────────────────────

    /// <summary>True when a matching ApplicantYearCommitmentSummary row exists.</summary>
    public bool HasCommitmentData { get; set; }

    /// <summary>True when a matching ApplicantYearDisbursementSummary row exists.</summary>
    public bool HasDisbursementData { get; set; }

    // ── Derived metrics ──────────────────────────────────────────────────────

    /// <summary>Fraction of eligible funding NOT committed. 0–1, clamped.</summary>
    public double ReductionPct { get; set; }

    /// <summary>Fraction of committed funding actually disbursed. 0–1, clamped.</summary>
    public double DisbursementPct { get; set; }

    /// <summary>Composite risk score: 0.5 × ReductionPct + 0.5 × (1 − DisbursementPct). 0–1.</summary>
    public double RiskScore { get; set; }

    /// <summary>High / Moderate / Low classification using thresholds > 0.6 / > 0.3.</summary>
    public string RiskLevel { get; set; } = "Low";

    // ── Metadata ─────────────────────────────────────────────────────────────

    public DateTime ImportedAtUtc { get; set; }
}
