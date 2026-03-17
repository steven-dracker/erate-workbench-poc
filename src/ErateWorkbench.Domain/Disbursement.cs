namespace ErateWorkbench.Domain;

/// <summary>
/// A single invoice line item from the USAC E-Rate Invoices and Authorized Disbursements
/// dataset (datahub.usac.org, Socrata resource jpiu-tj8h — FCC Forms 472 and 474).
///
/// Each row represents one line item on a BEAR (Form 472, applicant reimbursement) or
/// SPI (Form 474, service provider invoice) invoice. Multiple rows share a FundingRequestNumber;
/// the natural key is the combination of InvoiceId and InvoiceLineNumber, captured in
/// <see cref="RawSourceKey"/> for idempotent upsert.
///
/// Analytics enabled:
///   - Commitment vs disbursement (join to FundingCommitments on FundingRequestNumber)
///   - Entity-level utilization (join to Entities / EpcEntities on ApplicantEntityNumber)
///   - Payment lag (InvoiceReceivedDate → LineCompletionDate)
///   - SPIN-level disbursement volume (group by ServiceProviderSpin)
/// </summary>
public class Disbursement
{
    public int Id { get; set; }

    /// <summary>
    /// Idempotent upsert key: "{invoice_id}-{inv_line_num}" when invoice_id is present,
    /// or "{funding_request_number}-{inv_line_num}" as a fallback.
    /// </summary>
    public required string RawSourceKey { get; set; }

    /// <summary>USAC Funding Request Number (FRN). Links to FundingCommitments.FundingRequestNumber.</summary>
    public required string FundingRequestNumber { get; set; }

    /// <summary>USAC invoice identifier (invoice_id). Groups all lines on a single invoice.</summary>
    public string? InvoiceId { get; set; }

    /// <summary>Line number within the invoice (inv_line_num).</summary>
    public string? InvoiceLineNumber { get; set; }

    /// <summary>Invoice form type: "BEAR" (Form 472, applicant reimbursement) or "SPI" (Form 474, provider invoice).</summary>
    public string? InvoiceType { get; set; }

    /// <summary>USAC processing status for this line (inv_line_item_status).</summary>
    public string? InvoiceLineStatus { get; set; }

    /// <summary>FCC Form 471 application number (form_471_app_num). Links to Form471Applications.</summary>
    public string? ApplicationNumber { get; set; }

    /// <summary>Billed Entity Number (billed_entity_number). Links to EpcEntities / Entities on EntityNumber.</summary>
    public string? ApplicantEntityNumber { get; set; }

    public string? ApplicantEntityName { get; set; }

    /// <summary>Service Provider SPIN (inv_service_provider_id_number_spin). Links to ServiceProviders.</summary>
    public string? ServiceProviderSpin { get; set; }

    public string? ServiceProviderName { get; set; }

    public int FundingYear { get; set; }

    /// <summary>Category 1 or Category 2 (chosen_category_of_service).</summary>
    public string? CategoryOfService { get; set; }

    /// <summary>Amount requested on this invoice line (requested_inv_line_amt).</summary>
    public decimal? RequestedAmount { get; set; }

    /// <summary>Amount approved and authorized for disbursement (approved_inv_line_amt).</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>Date the invoice was received by USAC (inv_received_date).</summary>
    public DateTime? InvoiceReceivedDate { get; set; }

    /// <summary>Date this line item was completed / payment authorized (inv_line_completion_date).</summary>
    public DateTime? LineCompletionDate { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
