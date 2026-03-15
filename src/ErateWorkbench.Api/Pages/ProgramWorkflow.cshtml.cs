using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class ProgramWorkflowModel : PageModel
{
    public IReadOnlyList<WorkflowPhase> Phases { get; } = WorkflowData.Phases;

    public void OnGet() { }
}

public record WorkflowStep(string Name, string Description, string Owner);

public record WorkflowPhase(
    int Number,
    string Title,
    string Timing,
    string Lead,
    string AccentColor,
    bool Danger,
    WorkflowStep[] Steps,
    string FooterNote);

/// <summary>Owner label → badge color mapping.</summary>
public static class OwnerBadge
{
    public static string Color(string owner) => owner switch
    {
        "E-Rate Central" => "#16a34a",   // green
        "Client"         => "#2563eb",   // blue
        "USAC"           => "#d97706",   // amber
        "Shared"         => "#7c3aed",   // purple
        "Audit Risk"     => "#dc2626",   // red
        _                => "#6c757d",
    };
}

internal static class WorkflowData
{
    public static readonly WorkflowPhase[] Phases =
    [
        new(
            Number: 1,
            Title: "Eligibility &amp; Planning",
            Timing: "Jul – Oct",
            Lead: "E-Rate Central leads",
            AccentColor: "#2563eb",
            Danger: false,
            Steps:
            [
                new("Confirm E-Rate Eligibility",
                    "Verify active NSLP participation, FCC Entity Registration, and enrollment counts are current before the new program year opens.",
                    "E-Rate Central"),
                new("Assess Technology Needs",
                    "Document current service levels, bandwidth utilization, and infrastructure gaps to inform the upcoming Form 470 scope.",
                    "Client"),
                new("Review Expiring Contracts",
                    "Identify all vendor contracts expiring during or before the upcoming program year; note whether re-bidding is required.",
                    "Shared"),
                new("Set Funding Priorities",
                    "Rank Category 1 (connectivity) and Category 2 (equipment) needs; confirm budget authority for applicant share of costs.",
                    "E-Rate Central"),
            ],
            FooterNote: "Key deadline: technology needs documented before Form 470 opening in October"),

        new(
            Number: 2,
            Title: "Competitive Bidding (Form 470)",
            Timing: "Oct – Jan",
            Lead: "28-day mandatory open bidding window",
            AccentColor: "#7c3aed",
            Danger: false,
            Steps:
            [
                new("Draft Form 470 in EPC",
                    "Prepare detailed service descriptions and technical requirements. All services to be funded must be described; vague scopes invite PIA scrutiny.",
                    "E-Rate Central"),
                new("Post Form 470 &amp; Open Bidding",
                    "Submit Form 470 to open the mandatory 28 calendar-day competitive bidding window. Do not contact preferred vendors during this period.",
                    "Client"),
                new("Evaluate Vendor Bids",
                    "Review all responsive bids using pre-defined, documented criteria. Price of eligible services must be the primary factor in the selection decision.",
                    "Shared"),
                new("Select Provider &amp; Archive Records",
                    "Award contract to winning bidder after 28-day window. Retain all bid submissions, scoring sheets, and selection rationale for 10 years.",
                    "Client"),
            ],
            FooterNote: "Form 470 must remain posted for ≥28 days before any contract is signed"),

        new(
            Number: 3,
            Title: "Funding Application (Form 471)",
            Timing: "Jan – Mar",
            Lead: "~70-day filing window",
            AccentColor: "#16a34a",
            Danger: false,
            Steps:
            [
                new("Execute Contracts",
                    "Sign service agreements with selected provider(s) only after the 28-day window has closed. Backdating or pre-signing voids eligibility.",
                    "Client"),
                new("Prepare Form 471 in EPC",
                    "Enter all Funding Request Numbers (FRNs), service descriptions, vendor SPINs, and cost details. Verify entity counts and discount rates.",
                    "E-Rate Central"),
                new("Certify &amp; Submit Application",
                    "Authorized district or library official reviews and certifies the application in EPC. Certification attests to accuracy and rule compliance.",
                    "Client"),
                new("Confirm USAC Receipt",
                    "Download USAC receipt notification and record the application number. Monitor EPC for any receipt errors or missing certifications.",
                    "E-Rate Central"),
            ],
            FooterNote: "Application window typically closes late March — check USAC website for exact annual dates"),

        new(
            Number: 4,
            Title: "USAC PIA Review",
            Timing: "Mar – Jul",
            Lead: "Highest audit risk — 15-day response deadlines",
            AccentColor: "#dc2626",
            Danger: true,
            Steps:
            [
                new("Monitor PIA Question Queue",
                    "Check EPC daily for new Program Integrity Assurance (PIA) questions. Assign each question to an owner; track open items in a shared log.",
                    "Shared"),
                new("Respond Within 15 Days",
                    "All PIA responses must be submitted within 15 calendar days of the inquiry date. Missed deadlines are treated as non-responsive and risk denial.",
                    "Audit Risk"),
                new("Supply Supporting Documentation",
                    "Provide contracts, invoices, vendor bids, board minutes, and bid evaluation worksheets exactly as they existed at time of filing.",
                    "Audit Risk"),
                new("Appeal Denials Promptly",
                    "If an FRN is denied, file an appeal within 60 days of the FCDL date with a detailed written rationale and all supporting evidence.",
                    "E-Rate Central"),
            ],
            FooterNote: "⚠ 15-day PIA response deadline — missed responses result in funding denial with limited recourse"),

        new(
            Number: 5,
            Title: "Funding Commitment (FCDL)",
            Timing: "Jul – Oct",
            Lead: "Weekly commitment waves — 60-day appeal window",
            AccentColor: "#2563eb",
            Danger: false,
            Steps:
            [
                new("Review Funding Commitment Decision Letter",
                    "Compare each FCDL line item to the original Form 471 FRNs. Confirm committed amounts, discount percentages, and service descriptions match expectations.",
                    "E-Rate Central"),
                new("Reconcile Committed Amounts",
                    "Flag any shortfall between requested and committed amounts. Document discrepancies and determine whether appeal or contract adjustment is needed.",
                    "Shared"),
                new("File Appeal if Warranted",
                    "Submit a written appeal to USAC within 60 days of the FCDL for any partially funded or denied FRNs. Include supporting evidence and legal basis.",
                    "E-Rate Central"),
                new("Notify Service Providers &amp; Leadership",
                    "Inform selected vendors of commitment status so service delivery can be scheduled. Brief district or library leadership on total funding received.",
                    "Client"),
            ],
            FooterNote: "FCDL appeal deadline: 60 days from the date printed on the FCDL"),

        new(
            Number: 6,
            Title: "Service Delivery &amp; Invoicing (BEAR / SPI)",
            Timing: "Jul – Oct (following year)",
            Lead: "BEAR = applicant reimbursement · SPI = provider direct invoice",
            AccentColor: "#d97706",
            Danger: false,
            Steps:
            [
                new("Confirm Service Delivery",
                    "Obtain written confirmation from the service provider that all contracted services have been delivered and accepted. File with program records.",
                    "Client"),
                new("Select Invoice Method",
                    "Choose BEAR (FCC Form 472, applicant seeks reimbursement) or SPI (FCC Form 474, provider invoices USAC directly) for each FRN.",
                    "E-Rate Central"),
                new("Submit Invoices to USAC",
                    "File completed BEAR or SPI invoice forms in EPC within applicable deadlines. BEAR deadline is typically 120 days after service delivery or FCDL, whichever is later.",
                    "E-Rate Central"),
                new("Track Disbursement Status",
                    "Monitor EPC for invoice processing status and payment confirmation. Reconcile USAC disbursements against expected amounts in the applicant's accounting records.",
                    "Shared"),
            ],
            FooterNote: "BEAR invoice deadline: 120 days from service delivery date or FCDL date, whichever is later"),

        new(
            Number: 7,
            Title: "Disbursement, Compliance &amp; Document Retention",
            Timing: "Ongoing",
            Lead: "10-year document retention obligation",
            AccentColor: "#16a34a",
            Danger: false,
            Steps:
            [
                new("Reconcile USAC Disbursements",
                    "Confirm all USAC payments are received and correctly posted in the applicant's financial system. Investigate and resolve any payment discrepancies promptly.",
                    "Shared"),
                new("Archive Program Records",
                    "Organize and securely retain all E-Rate documentation — contracts, bids, invoices, certifications, and correspondence — for a minimum of 10 years.",
                    "Client"),
                new("Maintain Audit-Ready Files",
                    "Ensure records are retrievable and organized by program year and FRN. USAC, FCC OIG, and state auditors may request documentation years after program close.",
                    "Audit Risk"),
                new("Debrief &amp; Plan Next Cycle",
                    "Conduct a post-year review of lessons learned, PIA issues, and funding outcomes. Begin eligibility and technology needs assessment for the next program year.",
                    "E-Rate Central"),
            ],
            FooterNote: "Retention requirement: 10 years from the last date of service for each program year"),
    ];
}
