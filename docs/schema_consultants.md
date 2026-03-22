# Consultant Dataset Schema Discovery

**Task:** CC-ERATE-000038A (Completion Pass)
**Original attempt:** 2026-03-21 — blocked by Socrata scheduled maintenance
**Completed:** 2026-03-22
**Status:** COMPLETE — live API data observed for both datasets

---

## Discovery Status

Both datasets were fetched live from `datahub.usac.org` after the maintenance window closed.
All field inventories, example values, and analytical sections below reflect actual observed API
responses, not catalog metadata or inference.

| Dataset | Field inventory | Example values | API field names |
|---------|----------------|---------------|----------------|
| x5px-esft (Form 471 Consultants) | Complete — 16 fields observed | Yes | Yes — confirmed snake_case |
| mihb-jfex (Consultant FRN Status) | Complete — 59 fields observed | Yes | Yes — confirmed snake_case |

---

## Dataset 1 — Form 471 Consultants (x5px-esft)

**Full title:** E-Rate Request for Discount on Services: Consultants (FCC Form 471 and Related Information)
**URL:** https://datahub.usac.org/E-Rate/E-Rate-Request-for-Discount-on-Services-Consultant/x5px-esft
**Granularity:** One row per consultant per Form 471 application
**Coverage:** Historical — all funding years where a consultant was listed on a Form 471

**Live sample record (application_number 211007579, FY2021):**
```json
{
  "application_number": "211007579",
  "funding_year": "2021",
  "state": "OH",
  "form_version": "Original",
  "is_certified_in_window": "In Window",
  "epc_organization_id": "130138",
  "organization_name": "UPPER SCIOTO VALLEY LSD",
  "applicant_type": "School District",
  "cnct_email": "ruthie@effsd.com",
  "cnslt_name": "ERATE FUNDING FOR SCHOOL DISTRICTS",
  "cnslt_epc_organization_id": "17011235",
  "cnslt_city": "TROY",
  "cnslt_state": "OH",
  "cnslt_zipcode": "45373",
  "cnslt_phone": "937-440-0444",
  "cnslt_email": "marv@effsd.com"
}
```

**Field inventory (16 observed API fields):**

| API Field Name | Example Value | Inferred Type | Notes |
|----------------|---------------|---------------|-------|
| `application_number` | "211007579" | string | Form 471 application ID — primary join key |
| `funding_year` | "2021" | string (numeric) | E-Rate funding year |
| `state` | "OH" | string | Applicant's state — 2-char code (see State Field section) |
| `form_version` | "Original" | string | Values observed: "Original", "Current" |
| `is_certified_in_window` | "In Window" | string | Certification timing status |
| `epc_organization_id` | "130138" | string | Applicant's EPC system organization ID |
| `organization_name` | "UPPER SCIOTO VALLEY LSD" | string | Applicant organization name — free text, mixed casing |
| `applicant_type` | "School District" | string | Entity type; values observed: "School District" |
| `cnct_email` | "ruthie@effsd.com" | string | Applicant contact email |
| `cnslt_name` | "ERATE FUNDING FOR SCHOOL DISTRICTS" | string | Consultant firm/individual name — no casing standard (see Data Quality) |
| `cnslt_epc_organization_id` | "17011235" | string | Consultant's EPC organization ID — preferred grouping key over name |
| `cnslt_city` | "TROY" | string | Consultant's city of record |
| `cnslt_state` | "OH" | string | Consultant's state — 2-char code; distinct from applicant `state` |
| `cnslt_zipcode` | "45373" | string | Consultant's zip code |
| `cnslt_phone` | "937-440-0444" | string | Consultant's phone number |
| `cnslt_email` | "marv@effsd.com" | string | Consultant's email address |

**Notes on field count discrepancy:** The USAC catalog page listed 17 display names, including
"Consultant's Phone Ext." That field does not appear in the live API response for any of the 5
sample records fetched. Socrata omits null/empty fields from JSON responses, so `cnslt_phone_ext`
likely exists but is sparsely populated. ETL should handle its absence gracefully.

**No `ben` (Billed Entity Number) field present in x5px-esft.** The catalog listed "Billed Entity
Number" but it does not appear in any of the 5 live sample records. This dataset links via
`epc_organization_id`, not BEN.

---

## Dataset 2 — Consultant FRN Status (mihb-jfex)

**Full title:** Consultant Update to E-rate Request for Discount on Services: FRN Status (FCC Form 471 and Related Information)
**URL:** https://datahub.usac.org/E-Rate/Consultant-Update-to-E-rate-Request-for-Discount-o/mihb-jfex
**Granularity:** One row per FRN (Funding Request Number) per consultant-assisted application
**Coverage:** Observed from FY2016; upper bound unknown (likely current funding year)

**Live sample record (application_number 161011863, FY2016, FRN 1699028290):**
```json
{
  "application_number": "161011863",
  "funding_year": "2016",
  "state": "TX",
  "form_version": "Current",
  "is_certified_in_window": "In Window",
  "ben": "140028",
  "organization_name": "Texhoma Independent Sch Dist",
  "cnct_email": "cmortonassociates@gmail.com",
  "cnslt_epc_organization_id": "16062048",
  "cnslt_name": "ESC Region 12 E-Rate Consulting",
  "funding_request_number": "1699028290",
  "form_471_frn_status_name": "Funded",
  "nickname": "Internet Access",
  "form_471_service_type_name": "Data Transmission and/or Internet Access",
  "contract_type_name": "Contract",
  "bid_count": "1",
  "is_based_on_state_master_contract": "No",
  "is_multiple_award": "No",
  "establishing_form_470": "160003189",
  "was_fcc_form_470_posted": "Yes",
  "award_date": "2016-02-08T00:00:00.000",
  "service_delivery_deadline": "2017-06-30T00:00:00.000",
  "spin_name": "Region 16 Education Service Center",
  "spac_filed": "Yes",
  "epc_organization_id": "143016965",
  "has_voluntary_extension": "No",
  "pricing_confidentiality": "No",
  "service_start_date": "2016-07-01T00:00:00.000",
  "contract_expiration_date": "2019-06-30T00:00:00.000",
  "narrative": "internet access - first year of three year contract",
  "total_monthly_recurring_cost": "4791.66",
  "total_monthly_recurring_ineligible_costs": "0",
  "total_monthly_recurring_eligible_costs": "4791.66",
  "months_of_service": "12",
  "total_pre_discount_eligible_recurring_costs": "57499.92",
  "total_one_time_costs": "0",
  "total_ineligible_one_time_costs": "0",
  "total_pre_discount_eligible_one_time_costs": "0",
  "total_pre_discount_costs": "57499.92",
  "dis_pct": "0.8",
  "funding_commitment_request": "45999.94",
  "pending_reason": "FCDL Issued",
  "organization_entity_type_name": "School District",
  "actual_start_date": "2016-07-01T00:00:00.000",
  "form_486_no": "19296",
  "f486_case_status": "Approved",
  "invoicing_ready": "Yes",
  "last_date_to_invoice": "2017-10-30T00:00:00.000",
  "wave_sequence_number": "17",
  "fcdl_letter_date": "2016-10-17T00:00:00.000",
  "user_generated_fcdl_date": "2016-10-17T00:00:00.000",
  "fcdl_comment_app": "MR1:The discount percentage of this FCC Form 471 application was increased from 60% to 80%...",
  "fcdl_comment_frn": "MR1:Approved as submitted.",
  "appeal_wave_number": "244",
  "revised_fcdl_date": "2024-06-26T00:00:00.000",
  "invoicing_mode": "SPI",
  "total_authorized_disbursement": "45615.94",
  "post_commitment_rationale": "244-An entity update has been created...",
  "revised_fcdl_comment": "244-MR1:BEN 84350 TEXHOMA ELEMENTARY SCHOOL has been removed..."
}
```

**Field inventory (59 observed API fields):**

| API Field Name | Example Value | Inferred Type | Notes |
|----------------|---------------|---------------|-------|
| `application_number` | "161011863" | string | Form 471 application ID — join key to x5px-esft |
| `funding_year` | "2016" | string (numeric) | E-Rate funding year |
| `state` | "TX" | string | Applicant's state — 2-char code |
| `form_version` | "Current" | string | Form version |
| `is_certified_in_window` | "In Window" | string | Certification timing |
| `ben` | "140028" | string | Building Entity Number — applicant entity ID (legacy USAC system) |
| `organization_name` | "Texhoma Independent Sch Dist" | string | Applicant organization name |
| `cnct_email` | "cmortonassociates@gmail.com" | string | Applicant contact email |
| `cnslt_epc_organization_id` | "16062048" | string | Consultant's EPC organization ID |
| `cnslt_name` | "ESC Region 12 E-Rate Consulting" | string | Consultant name |
| `funding_request_number` | "1699028290" | string | FRN — unique ID per funding request line; differentiates granularity from x5px-esft |
| `form_471_frn_status_name` | "Funded" | string | FRN commitment status; values include "Funded", likely others |
| `nickname` | "Internet Access" | string | Applicant-provided FRN description |
| `form_471_service_type_name` | "Data Transmission and/or Internet Access" | string | Service type — **present in this dataset** (unlike 9s6i-myen) |
| `contract_type_name` | "Contract" | string | Procurement type |
| `bid_count` | "1" | string (integer) | Number of bids received |
| `is_based_on_state_master_contract` | "No" | string (boolean) | |
| `is_multiple_award` | "No" | string (boolean) | |
| `establishing_form_470` | "160003189" | string | Related Form 470 (competitive bidding form) number |
| `was_fcc_form_470_posted` | "Yes" | string (boolean) | Whether Form 470 was posted |
| `award_date` | "2016-02-08T00:00:00.000" | string (ISO datetime) | Contract award date |
| `service_delivery_deadline` | "2017-06-30T00:00:00.000" | string (ISO datetime) | |
| `spin_name` | "Region 16 Education Service Center" | string | Service provider name |
| `spac_filed` | "Yes" | string (boolean) | Service Provider Annual Certification filed |
| `epc_organization_id` | "143016965" | string | EPC org ID — distinct from BEN; see notes below |
| `has_voluntary_extension` | "No" | string (boolean) | Contract extension |
| `pricing_confidentiality` | "No" | string (boolean) | |
| `service_start_date` | "2016-07-01T00:00:00.000" | string (ISO datetime) | |
| `contract_expiration_date` | "2019-06-30T00:00:00.000" | string (ISO datetime) | |
| `narrative` | "internet access - first year of three year contract" | string | Free-text FRN description |
| `total_monthly_recurring_cost` | "4791.66" | string (decimal) | Total monthly cost including ineligible |
| `total_monthly_recurring_ineligible_costs` | "0" | string (decimal) | |
| `total_monthly_recurring_eligible_costs` | "4791.66" | string (decimal) | |
| `months_of_service` | "12" | string (integer) | Service months in funding year |
| `total_pre_discount_eligible_recurring_costs` | "57499.92" | string (decimal) | Annual eligible recurring costs pre-discount |
| `total_one_time_costs` | "0" | string (decimal) | |
| `total_ineligible_one_time_costs` | "0" | string (decimal) | |
| `total_pre_discount_eligible_one_time_costs` | "0" | string (decimal) | |
| `total_pre_discount_costs` | "57499.92" | string (decimal) | Total pre-discount eligible costs |
| `dis_pct` | "0.8" | string (decimal, 0.0–1.0) | E-Rate discount percentage applied |
| `funding_commitment_request` | "45999.94" | string (decimal) | E-Rate requested amount (pre-discount × dis_pct) |
| `pending_reason` | "FCDL Issued" | string | FCDL status reason |
| `organization_entity_type_name` | "School District" | string | Entity type — equivalent to x5px-esft `applicant_type` |
| `actual_start_date` | "2016-07-01T00:00:00.000" | string (ISO datetime) | |
| `form_486_no` | "19296" | string | Form 486 (service delivery confirmation) number |
| `f486_case_status` | "Approved" | string | Form 486 review status |
| `invoicing_ready` | "Yes" | string (boolean) | Whether invoicing can proceed |
| `last_date_to_invoice` | "2017-10-30T00:00:00.000" | string (ISO datetime) | |
| `wave_sequence_number` | "17" | string (integer) | FCDL wave number |
| `fcdl_letter_date` | "2016-10-17T00:00:00.000" | string (ISO datetime) | Original FCDL issue date |
| `user_generated_fcdl_date` | "2016-10-17T00:00:00.000" | string (ISO datetime) | |
| `fcdl_comment_app` | "MR1:..." | string | Application-level FCDL comment; long free text |
| `fcdl_comment_frn` | "MR1:Approved as submitted." | string | FRN-level FCDL comment |
| `appeal_wave_number` | "244" | string (integer) | Appeal wave; this record was revised in wave 244 (2024) |
| `revised_fcdl_date` | "2024-06-26T00:00:00.000" | string (ISO datetime) | Date of most recent FCDL revision |
| `invoicing_mode` | "SPI" | string | Invoicing mode ("SPI" = Service Provider Invoice) |
| `total_authorized_disbursement` | "45615.94" | string (decimal) | Actual E-Rate disbursement — differs from commitment request due to post-commitment changes |
| `post_commitment_rationale` | "244-An entity update..." | string | Long free text; reason for post-commitment change |
| `revised_fcdl_comment` | "244-MR1:BEN..." | string | Long free text; details of FCDL revision |

**Note on `epc_organization_id` in mihb-jfex:** In the sample record, `epc_organization_id` is
"143016965" while `ben` is "140028". These reference different USAC systems (EPC vs. legacy BEN
system). This field's exact semantic (applicant EPC ID vs. FRN-level entity) requires clarification
before using as a join key — use `application_number` for joins, not `epc_organization_id`.

---

## Candidate Analytical Fields

Both datasets confirmed to contain the following fields:

| Analytical Purpose | x5px-esft Field | mihb-jfex Field | Confirmed |
|--------------------|-----------------|-----------------|-----------|
| Consultant identity | `cnslt_name` | `cnslt_name` | Yes — both present |
| Consultant grouping key | `cnslt_epc_organization_id` | `cnslt_epc_organization_id` | Yes — both present |
| Application linkage / join | `application_number` | `application_number` | Yes — confirmed join key |
| Funding year filter | `funding_year` | `funding_year` | Yes — both present |
| Applicant state (geography) | `state` | `state` | Yes — both refer to applicant state |
| Consultant state (geography) | `cnslt_state` | Not present | Partial — x5px-esft only |
| Entity type segmentation | `applicant_type` | `organization_entity_type_name` | Yes — different field names, same concept |
| Service type | Not present | `form_471_service_type_name` | mihb-jfex only — **ServiceType IS present here** (unlike 9s6i-myen) |
| FRN commitment request | Not present | `funding_commitment_request` | mihb-jfex only |
| Actual disbursement | Not present | `total_authorized_disbursement` | mihb-jfex only |
| FRN-level granularity | Not present | `funding_request_number` | mihb-jfex only |
| Discount percentage | Not present | `dis_pct` | mihb-jfex only |
| FRN status | Not present | `form_471_frn_status_name` | mihb-jfex only |

**Key finding:** Funding amounts exist exclusively in mihb-jfex. x5px-esft is purely consultant
identity / application relationship data. Any financial analytics for consultant-assisted applications
must source amounts from mihb-jfex or a join to the standard FRN Status dataset (`qdmp-ygft`).

**ServiceType resolution:** `form_471_service_type_name` is present in mihb-jfex (unlike dataset
`9s6i-myen` which is null for all records). This resolves TD-note about ServiceType being unavailable —
it is available through the consultant FRN path.

---

## Join Strategy Assessment

**Join key confirmed:** `application_number` exists in both datasets with consistent format.

**Format consistency check (from live data):**
- x5px-esft: "211007579" — 9-digit numeric string, no prefix
- mihb-jfex: "161011863" — 9-digit numeric string, no prefix
- Format matches ✓

**Cardinality:**
- x5px-esft: one row per consultant per application (application-level)
- mihb-jfex: one row per FRN per consultant-assisted application (FRN-level)
- Joining on `application_number` alone will fan out: 1 x5px-esft row → N mihb-jfex rows
  (one per FRN on that application). Applications with multiple FRNs produce multiple join results.
  Aggregation must SUM or COUNT at the application level after joining.

**Null presence:** `application_number` was populated in both sample records. Null rate unknown across
full dataset — safe to assume it is the primary key in x5px-esft and a foreign key in mihb-jfex.

**Duplication risk:** If a single application has multiple consultants (possible per x5px-esft
granularity definition), joining to mihb-jfex will produce cartesian fan-out.
Recommend joining via `cnslt_epc_organization_id` + `application_number` where possible to scope
the join to a specific consultant's FRNs.

**Assessment: CONDITIONALLY SAFE**

The join key exists and has consistent format in both datasets. The cardinality difference
(application-level in x5px-esft, FRN-level in mihb-jfex) is manageable but requires intentional
aggregation. A join without grouping will produce inflated row counts. ETL design must explicitly
decide the grain of any combined view before building.

---

## Data Quality Observations

**Consultant name normalization — observed inconsistency across 5 x5px-esft records:**

| Record | `cnslt_name` value |
|--------|--------------------|
| 1 | "ERATE FUNDING FOR SCHOOL DISTRICTS" — all uppercase |
| 2 | "Quinn e-Solutions, LLC" — mixed case, legal suffix with comma |
| 3 | "Erate Exchange LLC" — mixed case, legal suffix without comma |
| 4 | "ATG" — all-caps abbreviation |
| 5 | "CTC Technology & Energy" — mixed case, ampersand |
| mihb-jfex sample | "ESC Region 12 E-Rate Consulting" — mixed case |

**Conclusion:** No consistent casing standard. Name-based grouping requires normalization
(lowercasing + trimming at minimum). `cnslt_epc_organization_id` is the reliable grouping key
and should be preferred over `cnslt_name` for any aggregation.

**Null/sparse fields:**
- `cnslt_phone_ext` — not present in any of the 5 x5px-esft records fetched (Socrata omits null
  fields from JSON). ETL must default to null; do not treat absence as error.
- `narrative`, `fcdl_comment_app`, `fcdl_comment_frn`, `post_commitment_rationale`,
  `revised_fcdl_comment` in mihb-jfex — long free-text fields, likely sparse for earlier FYs
  or simple FRNs. Treat as optional.
- `revised_fcdl_date`, `appeal_wave_number` — populated only when an appeal or revision occurred.
  Sparse by design.

**Funding value semantics in mihb-jfex:**
- `funding_commitment_request` = the E-Rate requested amount (total pre-discount eligible costs × discount %)
- `total_authorized_disbursement` = the actual disbursed amount after post-commitment adjustments
- These differ (45999.94 vs 45615.94 in the sample) due to appeals, entity changes, or FRN line modifications
- Both are FRN-level figures, not application-level totals. Aggregation to application or consultant
  total requires SUMming across all FRNs for that application/consultant.

---

## E-Rate Central Representation

E-Rate Central was not found in the 5-record sample from x5px-esft. Firm names observed:
"ERATE FUNDING FOR SCHOOL DISTRICTS", "Quinn e-Solutions, LLC", "Erate Exchange LLC", "ATG",
"CTC Technology & Energy". In mihb-jfex: "ESC Region 12 E-Rate Consulting".

A targeted query is required to confirm E-Rate Central's representation:
```
GET https://datahub.usac.org/resource/x5px-esft.json?$where=cnslt_name LIKE '%E-Rate Central%'&$limit=5
```

Alternative approach: look up E-Rate Central's EPC Organization ID through the EPC portal or
USAC entity lookup, then query `?$where=cnslt_epc_organization_id='XXXX'` to capture all name
variants under a single firm.

**Status:** Representation in dataset is plausible but unconfirmed from this sample. The
`cnslt_epc_organization_id` approach is recommended for production querying over name matching.

---

## State Field Interpretation

**x5px-esft:**
- `state` = applicant's state (confirmed: "OH" for Upper Scioto Valley LSD, an Ohio school district)
- `cnslt_state` = consultant's state of record (confirmed: "OH" for ERATE FUNDING FOR SCHOOL DISTRICTS
  headquartered in Troy, OH — different from the Virginia-based E-Rate Central example but correct here)
- These are distinct fields with distinct purposes. Do not conflate.

**mihb-jfex:**
- `state` = applicant's state (confirmed: "TX" for Texhoma Independent Sch Dist, a Texas school district)
- No `cnslt_state` field present in mihb-jfex

**Analytical implication:**
- "Consultant market by state" → use `state` (applicant's state) — where the consultant is winning business
- "Consultant location/HQ" → use `cnslt_state` from x5px-esft — where the consultant is based
- A consultant headquartered in MD (`cnslt_state = MD`) may have the majority of their business
  in CA (`state = CA`). Both dimensions are valuable; neither should stand in for the other.

---

## Open Questions / Risks

The following remain unresolved after live API observation:

1. **E-Rate Central EPC Organization ID** — The specific `cnslt_epc_organization_id` for E-Rate
   Central is not yet confirmed. A targeted query by firm name or manual EPC lookup is required
   before building any firm-specific analytics. This is a prerequisite for Competitive Intelligence
   Dashboard work.

2. **Multi-consultant applications in x5px-esft** — Can one Form 471 application have rows for
   multiple consultants? If yes, a join to mihb-jfex on `application_number` alone will produce
   cross-consultant fan-out. The safe join key is `(application_number, cnslt_epc_organization_id)`
   if mihb-jfex contains both. Current sample only confirms `application_number` as a join field.

3. **`cnslt_phone_ext` field existence** — Not observed in 5 records. May be entirely absent or
   wholly null. Confirm by querying `$where=cnslt_phone_ext IS NOT NULL&$limit=1`.

4. **`epc_organization_id` semantics in mihb-jfex** — Value "143016965" in the sample record does
   not match the applicant `ben` ("140028") and is not explained by the field name alone. May be
   the FRN originating entity's EPC ID. Requires deeper investigation before using as a join key.

5. **FY coverage lower bound for mihb-jfex** — Earliest observed record is FY2016. Whether earlier
   funding years (FY2015 and prior) are included is unknown.

6. **`applicant_type` (x5px-esft) vs `organization_entity_type_name` (mihb-jfex)** — Both represent
   entity type but use different field names. Value equivalence (e.g., "School District" in both) is
   confirmed for one record but may diverge for libraries, consortia, or non-traditional entities.
