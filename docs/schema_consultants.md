# Consultant Dataset Schema Discovery

**Task:** CC-ERATE-000038A
**Date:** 2026-03-21
**Status:** PARTIAL — see Discovery Status below

---

## Discovery Status

Both USAC Socrata endpoints (`datahub.usac.org`, `opendata.usac.org`) were offline during this discovery
session due to a confirmed scheduled platform maintenance window:

> **Socrata US Data Center — Scheduled Maintenance**
> March 21, 2026, 5:00pm – 9:00pm Eastern
> Affected components: SODA API, Catalog, Data Table, Ingress/Publishing, and others
> Source: https://status.socrata.com/

**Impact by dataset:**

| Dataset | Field inventory | Example values | API field names |
|---------|----------------|---------------|----------------|
| x5px-esft (Form 471 Consultants) | Partial — from USAC catalog, not live API | Not available | Not available — display names only |
| mihb-jfex (Consultant FRN Status) | Not available | Not available | Not available |

**Required follow-up:** Re-run live API fetch against both endpoints after maintenance ends.
`GET https://datahub.usac.org/resource/x5px-esft.json?$limit=1`
`GET https://datahub.usac.org/resource/mihb-jfex.json?$limit=1`

---

## Dataset 1 — Form 471 Consultants (x5px-esft)

**Full title:** E-Rate Request for Discount on Services: Consultants (FCC Form 471 and Related Information)
**URL:** https://datahub.usac.org/E-Rate/E-Rate-Request-for-Discount-on-Services-Consultant/x5px-esft
**Granularity:** One row per consultant per Form 471 application
**Coverage:** Historical — all funding years where a consultant was listed on a Form 471

**Source of field inventory:** USAC dataset catalog page (not a live API response).
JSON API field names (snake_case) are not confirmed — display names listed below may differ
from actual API keys. Example values and inferred types are not available.

| Field (Display Name) | Example | Type | Notes |
|----------------------|---------|------|-------|
| Application Number | — | text/numeric | Likely join key to other Form 471 datasets |
| Funding Year | — | numeric | E-Rate funding year (e.g., 2024) |
| Billed Entity Number | — | numeric | USAC-assigned entity identifier |
| Applicant's Organization Name | — | text | Name of the school/library applicant |
| Applicant Type | — | text | e.g., School, Library, Consortium |
| Billed Entity State | — | text | 2-letter state code — see State Field Interpretation section |
| Form Version | — | text/numeric | Version of the Form 471 submission |
| Window Status | — | text | e.g., Certified, Committed — unclear exact values |
| Contact Email | — | text | Applicant contact email |
| Consultant's Name | — | text | Free-text name of consultant firm or individual |
| Consultant's EPC Organization ID | — | numeric | EPC system ID for the consultant organization |
| Consultant's City | — | text | Consultant's city of record |
| Consultant's State | — | text | Consultant's state — 2-letter code |
| Consultant's Zip Code | — | text | Consultant's zip code |
| Consultant's Phone | — | text | Consultant phone number |
| Consultant's Phone Ext | — | text | Phone extension — likely sparse |
| Consultant's Email | — | text | Consultant contact email |

**Total confirmed fields from catalog:** 17
**API field names:** Not confirmed — must be retrieved from live API response

---

## Dataset 2 — Consultant FRN Status (mihb-jfex)

**Full title:** Consultant Update to E-rate Request for Discount on Services: FRN Status (FCC Form 471 and Related Information)
**URL:** https://datahub.usac.org/E-Rate/Consultant-Update-to-E-rate-Request-for-Discount-o/mihb-jfex
**Granularity:** Unknown — likely one row per FRN (Funding Request Number) per consultant
**Coverage:** Unknown — likely FY2016+ based on naming convention of similar FRN Status datasets

**Field inventory:** NOT AVAILABLE — platform offline during discovery window.

No field names, example values, types, or API keys can be documented without a live API response.
All downstream analysis sections for this dataset are necessarily incomplete.

**What is known from dataset title and USAC naming conventions:**
- This is a consultant-scoped view of FRN-level data (FRN = Funding Request Number, the line-item within a Form 471)
- It is described as a "consultant update to" the standard FRN Status dataset (`qdmp-ygft`), suggesting it adds or filters consultant attribution to FRN funding outcomes
- Standard FRN Status datasets in E-Rate typically include: application number, FRN, funding year, service type, requested amount, committed amount, FRN status
- Whether this dataset confirms, mirrors, or supplements those fields is not confirmed

**This section must be completed by a follow-up live API fetch.**

---

## Candidate Analytical Fields

Fields likely useful for Competitive Intelligence Dashboard analytics, based on x5px-esft catalog only.
Equivalent fields in mihb-jfex are unknown.

| Analytical Purpose | x5px-esft Field | mihb-jfex Field | Confidence |
|--------------------|-----------------|-----------------|------------|
| Consultant identity | Consultant's Name | Unknown | Partial |
| Application linkage | Application Number | Unknown | Partial |
| Funding year filter | Funding Year | Unknown | Partial |
| Geography (applicant) | Billed Entity State | Unknown | Partial |
| Geography (consultant) | Consultant's State | Unknown | Partial |
| Entity type segmentation | Applicant Type | Unknown | Partial |
| Requested funding | Not present in x5px-esft | Unknown | None |
| Committed funding | Not present in x5px-esft | Unknown | None |

**Key observation:** x5px-esft appears to be an applicant/consultant identity dataset with no funding
amounts. Funding amounts (requested, committed) likely exist only in mihb-jfex or would require a join
to the standard FRN Status dataset (`qdmp-ygft`). This has significant implications for dashboard design.

---

## Join Strategy Assessment

**Primary candidate join key:** Application Number

- x5px-esft contains Application Number
- mihb-jfex field inventory is unknown — join key cannot be confirmed
- If Application Number exists in both datasets, it would enable linking consultant identity (x5px-esft)
  to funding outcomes (mihb-jfex)
- The standard FRN Status dataset (`qdmp-ygft`) also contains Application Number and FRN — a three-way
  join may be the correct architecture depending on what mihb-jfex actually contains

**Join risks (cannot fully assess until mihb-jfex schema is confirmed):**
- x5px-esft has one row per consultant per application — if an application has multiple consultants,
  it will produce multiple rows. A join to FRN data on Application Number will fan out.
- FRN-level data may have one row per FRN, while application-level data has one row per application.
  The granularity mismatch must be understood before designing any aggregation.
- Consultant Name is free-text in x5px-esft. If mihb-jfex contains a different representation of
  consultant identity (e.g., EPC Organization ID only), cross-dataset consultant grouping must use
  the EPC ID, not the name.

**Assessment:** Join feasibility is uncertain. Cannot be validated until mihb-jfex schema is observed.

---

## Data Quality Observations

Based on x5px-esft catalog fields only:

**Consultant name normalization:**
- `Consultant's Name` is a free-text field. E-rate consultant firms commonly appear under multiple
  name variants (e.g., "E-Rate Central", "E-rate Central", "E-Rate Central LLC"). No normalization
  can be confirmed or ruled out from catalog data alone.
- `Consultant's EPC Organization ID` may provide a more reliable grouping key than the name field,
  but only if it is consistently populated.

**Potentially sparse fields:**
- `Consultant's Phone Ext` — likely sparse; phone extensions are often not recorded
- `Contact Email` — applicant contact; may or may not correspond to the form submitter
- `Consultant's Email` — unknown fill rate; not all consultant listings may include email

**Null risks:**
- No null rates can be assessed without live data
- Free-text fields (Consultant's Name, Applicant's Organization Name) may have formatting
  inconsistencies that are not visible in catalog documentation

**Funding amounts:**
- No funding amount fields appear in x5px-esft. This dataset is identity/relationship data only.
  Committed and requested amounts must come from mihb-jfex or a join to `qdmp-ygft`.

**mihb-jfex quality:** Cannot be assessed — no data observed.

---

## E-Rate Central Representation

Cannot be confirmed from catalog data. Requires a live API fetch to observe actual Consultant's Name
values and identify firm variants.

**What to look for when live data is available:**
- "E-Rate Central" (canonical)
- "E-rate Central" (lowercase 'r')
- "E-Rate Central LLC" (with legal suffix)
- "E-Rate Central, Inc." (alternate legal suffix)
- "ERate Central" (no hyphen)
- Any subsidiary or regional variant names

The `Consultant's EPC Organization ID` may resolve ambiguity if E-Rate Central registers under a
single EPC organization ID regardless of name variants.

---

## State Field Interpretation

**x5px-esft contains two state fields:**

| Field | What it represents |
|-------|--------------------|
| `Billed Entity State` | Applicant's state (the school/library entity that filed the Form 471) |
| `Consultant's State` | Consultant's state of record in EPC (mailing/business address) |

These serve different analytical purposes:
- **Applicant state** → used to measure geographic distribution of consultant-assisted applications
- **Consultant state** → used to identify where consultant firms are based

**Implication:** A consultant based in Virginia may serve applicants in multiple states. These two
state fields should not be conflated. Any "state" filter in the dashboard must be explicit about
which state it represents.

**mihb-jfex:** State field presence and semantics are unknown.

---

## Open Questions / Risks

The following cannot be determined from a single catalog record or from catalog metadata alone.
All require live API access and ideally multiple records.

1. **mihb-jfex full field inventory** — No fields observed. Cannot proceed with ETL design for this
   dataset until the platform maintenance ends and a live fetch is performed.

2. **API field names vs. display names** — USAC Socrata datasets use snake_case API keys that may
   not match the display names in the catalog. For example, "Application Number" might be
   `application_number`, `app_number`, or `form471_application_number`. Must verify against actual
   JSON response.

3. **Join key format consistency** — Application Number format (numeric vs. string, leading zeros,
   prefixes) must be confirmed to match across x5px-esft and mihb-jfex before designing a join.

4. **Consultant's EPC Organization ID fill rate** — If this field is sparsely populated, consultant
   grouping must fall back to name-based matching with all associated normalization complexity.

5. **Rows per application in x5px-esft** — Can an application have multiple consultants? If yes,
   joins to FRN data will produce duplicate rows and aggregations must account for this.

6. **mihb-jfex coverage / funding year range** — Whether this dataset covers all funding years or
   only FY2016+ (like many FRN Status datasets) is unknown.

7. **Whether mihb-jfex includes requested amounts or only committed amounts** — Standard FRN Status
   datasets include both, but this consultant-specific variant may differ.

8. **ServiceType presence in either dataset** — Known to be null in dataset `9s6i-myen` (Form 471
   Applications). Whether it is populated in the consultant-specific datasets is unknown.

9. **E-Rate Central EPC Organization ID** — The specific ID(s) under which E-Rate Central registers
   in EPC must be confirmed from live data before any firm-specific analytics can be built.

10. **State field in mihb-jfex** — Whether the dataset contains applicant state, consultant state,
    both, or neither is unknown.
