using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ErateWorkbench.Api.Pages;

public class AdvisorPlaybookModel : PageModel
{
    public IReadOnlyList<PlaybookPhase> Phases { get; } = AdvisorPlaybookData.Phases;

    public void OnGet() { }
}

public enum PlaybookState { Complete, Current, Upcoming }

public record PlaybookPhase(
    int Number,
    string Title,
    string ShortLabel,
    string Timing,
    string AccentColor,
    PlaybookState State,
    string Objective,
    string[] KeyTasks,
    string[] Owners,
    string RiskNote,
    string ExitCriteria);

public static class AdvisorPlaybookData
{
    public static readonly PlaybookPhase[] Phases =
    [
        new(
            Number: 0,
            Title: "Client Engagement &amp; Intake",
            ShortLabel: "Intake",
            Timing: "Jul – Sep",
            AccentColor: "#0891b2",
            State: PlaybookState.Complete,
            Objective: "Establish the advisory relationship, surface the client's goals and constraints, and ensure all prerequisite data is in place before competitive bidding begins.",
            KeyTasks:
            [
                "Conduct initial consultation to surface connectivity goals, budget constraints, and timeline",
                "Verify entity registration, BEN status, and prior E-Rate participation history",
                "Collect student counts, NSLP/CEP data, service inventory, and existing contracts",
                "Document advisor/client responsibility boundaries and agree on the planning strategy",
            ],
            Owners: ["E-Rate Central", "Client", "Shared"],
            RiskNote: "Starting competitive bidding before eligibility is confirmed can result in application denial or audit exposure.",
            ExitCriteria: "Client eligibility confirmed, all planning data collected, funding strategy documented and agreed."
        ),

        new(
            Number: 1,
            Title: "Program Planning &amp; Eligibility Confirmation",
            ShortLabel: "Planning",
            Timing: "Jul – Oct",
            AccentColor: "#2563eb",
            State: PlaybookState.Complete,
            Objective: "Confirm all eligibility requirements are met and define the service scope and funding priorities needed for a strong Form 470.",
            KeyTasks:
            [
                "Confirm FCC Entity Registration, NSLP participation, and enrollment counts are current",
                "Document service gaps, bandwidth utilization, and infrastructure needs",
                "Review all vendor contracts expiring before or during the upcoming program year",
                "Finalize Category 1/2 priorities and confirm client's budget authority for the applicant share",
            ],
            Owners: ["E-Rate Central", "Client"],
            RiskNote: "Technology needs must be documented before Form 470 opens in October. Late documentation limits bidding scope.",
            ExitCriteria: "Eligibility confirmed, service scope documented, Form 470 draft ready to post."
        ),

        new(
            Number: 2,
            Title: "Competitive Bidding (Form 470)",
            ShortLabel: "Form 470",
            Timing: "Oct – Jan",
            AccentColor: "#7c3aed",
            State: PlaybookState.Complete,
            Objective: "Conduct a legally compliant competitive bidding process by posting Form 470 and evaluating all vendor responses before selecting a provider.",
            KeyTasks:
            [
                "Draft and post Form 470 in EPC; open the mandatory 28-day bidding window",
                "Hold all preferred-vendor contact during the open bidding period",
                "Evaluate all responsive bids using pre-defined, documented criteria — price is the primary factor",
                "Award contract and archive all bids, scoring sheets, and selection rationale",
            ],
            Owners: ["E-Rate Central", "Client"],
            RiskNote: "Form 470 must remain posted for at least 28 calendar days. Preferred-vendor contact during the window voids eligibility.",
            ExitCriteria: "Contract awarded after 28-day window, all documentation archived, ready to file Form 471."
        ),

        new(
            Number: 3,
            Title: "Funding Application (Form 471)",
            ShortLabel: "Form 471",
            Timing: "Jan – Mar",
            AccentColor: "#16a34a",
            State: PlaybookState.Current,
            Objective: "Submit a complete and accurate Form 471 within the filing window, capturing all funded services, vendor agreements, and applicant certifications.",
            KeyTasks:
            [
                "Execute vendor contracts only after the 28-day bidding window has closed",
                "Enter all FRNs, service descriptions, vendor SPINs, and cost detail in EPC",
                "Have the authorized district or library official certify and submit the application",
                "Confirm USAC receipt and log the application number for tracking",
            ],
            Owners: ["E-Rate Central", "Client"],
            RiskNote: "The Form 471 filing window typically closes late March — missing it forfeits E-Rate funding for the entire program year.",
            ExitCriteria: "Application submitted within the window, USAC receipt confirmed, application number logged."
        ),

        new(
            Number: 4,
            Title: "USAC PIA Review",
            ShortLabel: "PIA Review",
            Timing: "Mar – Jul",
            AccentColor: "#dc2626",
            State: PlaybookState.Upcoming,
            Objective: "Respond promptly and thoroughly to all USAC Program Integrity Assurance inquiries to protect the application from funding reduction or denial.",
            KeyTasks:
            [
                "Monitor EPC daily for new PIA questions; assign each question to a named owner",
                "Respond to every inquiry within 15 calendar days — no exceptions",
                "Supply contracts, bids, scoring evidence, and certifications exactly as they existed at filing",
                "Track all open items in a shared log; escalate any overdue items immediately",
            ],
            Owners: ["Shared", "Audit Risk"],
            RiskNote: "Missing the 15-day PIA response window is treated as non-responsive — funding is denied with very limited appeal recourse.",
            ExitCriteria: "All PIA questions answered and closed; FCDL issued by USAC."
        ),

        new(
            Number: 5,
            Title: "Funding Commitment (FCDL)",
            ShortLabel: "FCDL",
            Timing: "Jul – Oct",
            AccentColor: "#2563eb",
            State: PlaybookState.Upcoming,
            Objective: "Review and reconcile the Funding Commitment Decision Letter; act within the appeal window for any denied or reduced FRNs.",
            KeyTasks:
            [
                "Line-by-line review of FCDL committed amounts against each Form 471 FRN",
                "Flag shortfalls between committed and requested amounts; decide whether to appeal",
                "Submit any FCDL appeal within 60 days with full supporting evidence and legal basis",
                "Notify service providers and leadership of final commitment status",
            ],
            Owners: ["E-Rate Central", "Client"],
            RiskNote: "FCDL appeal window is exactly 60 days from the FCDL date — hard cutoff with no extension mechanism.",
            ExitCriteria: "Committed amounts confirmed, appeals filed if warranted, service delivery authorized."
        ),

        new(
            Number: 6,
            Title: "Service Delivery &amp; Invoicing (BEAR / SPI)",
            ShortLabel: "Invoicing",
            Timing: "Jul – Oct (following year)",
            AccentColor: "#d97706",
            State: PlaybookState.Upcoming,
            Objective: "Confirm all funded services were delivered as contracted, then submit timely invoices to recover the E-Rate discount.",
            KeyTasks:
            [
                "Obtain written confirmation that all contracted services were delivered and accepted",
                "Select BEAR (applicant reimbursement) or SPI (provider direct invoice) per FRN",
                "Submit completed invoice forms within 120 days of service delivery or FCDL date",
                "Monitor EPC for payment processing and reconcile disbursements against expected amounts",
            ],
            Owners: ["E-Rate Central", "Client"],
            RiskNote: "BEAR invoices must be filed within 120 days of service delivery or FCDL — missed deadlines permanently forfeit the funding.",
            ExitCriteria: "All invoices submitted, USAC disbursement received and reconciled to financial records."
        ),

        new(
            Number: 7,
            Title: "Disbursement, Compliance &amp; Document Retention",
            ShortLabel: "Compliance",
            Timing: "Ongoing",
            AccentColor: "#16a34a",
            State: PlaybookState.Upcoming,
            Objective: "Close the program year properly — reconcile all payments, archive required records, and prepare the client for the next cycle and potential audits.",
            KeyTasks:
            [
                "Confirm all USAC disbursements are posted correctly in the client's financial system",
                "Organize and archive records by program year and FRN for audit retrieval",
                "Ensure files are audit-ready for USAC, FCC OIG, or state reviewers",
                "Debrief on outcomes, document lessons learned, and begin next-cycle eligibility planning",
            ],
            Owners: ["Shared", "Client"],
            RiskNote: "E-Rate records must be retained for 10 years from the last date of service. Audit requests can arrive years after the program year closes.",
            ExitCriteria: "Records archived, payments reconciled, next-cycle review initiated."
        ),
    ];
}
