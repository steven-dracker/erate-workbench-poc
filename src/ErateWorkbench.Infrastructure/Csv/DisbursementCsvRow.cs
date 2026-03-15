using CsvHelper.Configuration.Attributes;

namespace ErateWorkbench.Infrastructure.Csv;

/// <summary>
/// Maps snake_case column names from the USAC E-Rate Invoices and Authorized Disbursements
/// dataset (Socrata resource jpiu-tj8h) as returned by the resource API:
///   https://datahub.usac.org/resource/jpiu-tj8h.csv?$limit=N&amp;$offset=N
///
/// Dates arrive as ISO-8601 strings (e.g. "2024-03-15T00:00:00.000"); the parser converts
/// them to nullable DateTime using DateTime.TryParse.
/// MissingFieldFound = null in the parser config silently handles absent columns.
/// </summary>
public class DisbursementCsvRow
{
    [Name("funding_request_number")]
    public string FundingRequestNumber { get; set; } = "";

    [Name("invoice_id")]
    public string? InvoiceId { get; set; }

    [Name("inv_line_num")]
    public string? InvoiceLineNumber { get; set; }

    [Name("invoice_type")]
    public string? InvoiceType { get; set; }

    [Name("inv_line_item_status")]
    public string? InvoiceLineStatus { get; set; }

    [Name("form_471_app_num")]
    public string? ApplicationNumber { get; set; }

    [Name("billed_entity_number")]
    public string? ApplicantEntityNumber { get; set; }

    [Name("billed_entity_name")]
    public string? ApplicantEntityName { get; set; }

    [Name("inv_service_provider_id_number_spin")]
    public string? ServiceProviderSpin { get; set; }

    [Name("inv_service_provider_name")]
    public string? ServiceProviderName { get; set; }

    // Mapped as string so blank values don't throw a TypeConverterException;
    // the parser converts to int via int.TryParse and skips rows where absent.
    [Name("funding_year")]
    public string? FundingYear { get; set; }

    [Name("chosen_category_of_service")]
    public string? CategoryOfService { get; set; }

    [Name("requested_inv_line_amt")]
    public decimal? RequestedAmount { get; set; }

    [Name("approved_inv_line_amt")]
    public decimal? ApprovedAmount { get; set; }

    // Dates arrive as strings; the parser converts them to DateTime?.
    [Name("inv_received_date")]
    public string? InvoiceReceivedDate { get; set; }

    [Name("inv_line_completion_date")]
    public string? LineCompletionDate { get; set; }
}
