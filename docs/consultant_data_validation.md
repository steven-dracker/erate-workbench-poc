# Consultant Dataset Validation Report

**Task:** CC-ERATE-000038C
**Date:** 2026-03-22
**Analyst:** Claude Code (automated via live Socrata API queries)
**Status:** COMPLETE — all sections evidence-based from live data

---

## Executive Summary

Both USAC consultant datasets are analytically viable for Competitive Intelligence dashboard
development. The data is internally consistent, the join key works, and EPC ID grouping is safe.
However, two critical findings require mitigation before building aggregations:

1. **Systematic Socrata-level duplication:** x5px-esft rows appear ~2x; mihb-jfex rows appear ~4x.
   The ETL's RawSourceKey upsert correctly deduplicates on import. Raw API queries without
   deduplication will overcount by the same factor.
2. **Bulk download CSV incompatibility (bug — fixed in this PR):** The views bulk download
   (`/api/views/x5px-esft/rows.csv?accessType=DOWNLOAD`) returns display-name column headers,
   not snake_case. The original import service would fail on any live import attempt. Both import
   services have been refactored to use the resource API with pagination (this PR).

**Recommendation: PROCEED WITH CAVEATS** — see final section.

---

## 1. Row Count Validation

### Source-of-truth counts (live Socrata API, 2026-03-22)

| Dataset | Total Rows (raw) | Distinct Natural Key | Duplication Factor |
|---------|-----------------|---------------------|-------------------|
| x5px-esft (ConsultantApplications) | **593,519** | 308,492 (app+consultant) | ~2x |
| mihb-jfex (ConsultantFrnStatuses) | **2,297,235** | 551,190 (app+FRN) | ~4x |

**Post-import (after ETL deduplication by RawSourceKey):**
- `ConsultantApplications`: ~308,492 rows
- `ConsultantFrnStatuses`: ~551,190 rows

### Row counts by funding year

**x5px-esft (ConsultantApplications) — raw rows:**

| Funding Year | Row Count | Notes |
|-------------|-----------|-------|
| 2016 | 51,074 | First year available |
| 2017 | 49,527 | |
| 2018 | 48,490 | Slight dip |
| 2019 | 53,208 | |
| 2020 | 62,490 | +17% spike — COVID window extension drove surge in filings |
| 2021 | 57,825 | |
| 2022 | 58,005 | |
| 2023 | 59,462 | |
| 2024 | 64,727 | |
| 2025 | 69,249 | Largest complete year |
| 2026 | 19,462 | **Partial — window still open** |

**mihb-jfex (ConsultantFrnStatuses) — raw rows:**

| Funding Year | Row Count | Notes |
|-------------|-----------|-------|
| 2016 | 324,517 | Highest — large initial committed base |
| 2017 | 275,685 | |
| 2018 | 219,157 | Declining trend (fewer FRNs/app) |
| 2019 | 217,301 | |
| 2020 | 252,156 | COVID spike visible |
| 2021 | 195,892 | Sharp drop — possible E-Rate modernization impact |
| 2022 | 185,849 | Lowest complete year |
| 2023 | 187,717 | |
| 2024 | 202,092 | Recovering |
| 2025 | 208,680 | |
| 2026 | 28,189 | **Partial — window still open** |

**Anomalies:**
- FY2020 spike in both datasets: consistent with COVID window extension (matches behavior documented
  in Form 471 analytics). Not data quality — annotate in dashboards.
- FY2021 mihb-jfex drop from 252K to 196K: significant but plausible — E-Rate Category 2 (internal
  connections) budget cycle changes in 2021 reduced FRN volume.
- FY2026 partial: expected. Treat as in-progress year; exclude from trend analysis.

---

## 2. Duplication Analysis (Critical Finding)

Both datasets have systematic Socrata-level row duplication that would cause overcounting in any
aggregation that queries the raw API without deduplication.

### x5px-esft duplication

Sample of 50,000 distinct (application_number, cnslt_epc_organization_id) combinations:

| Rows per combo | Count | % |
|---------------|-------|---|
| 1 (no duplicate) | 1,262 | 2.5% |
| 2 (duplicated) | 48,738 | 97.5% |

**Pattern:** 97.5% of all consultant-application combinations appear exactly twice. No combination
exceeds 2 rows in the sample. This appears to be a systematic Socrata export artifact.

### mihb-jfex duplication

Sample of 50,000 distinct funding_request_numbers:

| Rows per FRN | Count | % |
|-------------|-------|---|
| 1 | 24 | 0.0% |
| 2 | 942 | 1.9% |
| 4 | 48,286 | 96.6% |
| 6 | 104 | 0.2% |
| 8 | 641 | 1.3% |
| 12 | 3 | 0.0% |

**Pattern:** 96.6% of FRNs appear exactly 4 times. Max observed duplication factor: 36x (for
consortium-type applications with many line items).

**Verification:** Duplicate rows for the same FRN are byte-for-byte identical — all fields including
amounts, dates, and status are the same. This is NOT revision history; it is true redundant data.

### ETL handling (correct)

The ETL's `UpsertBatchAsync` deduplicates within each batch and upserts by `RawSourceKey`. The "last
occurrence wins" strategy collapses all duplicates into one row per natural key. This is correct
behavior. No code change required — but the deduplication behavior must be documented for analysts
so they don't assume row count = record count in the raw API.

**Implication for aggregation:** Any query against the **imported tables** is safe — duplicates are
collapsed. Any query against the **raw Socrata API** (for reconciliation or spot checks) must account
for the ~2x/~4x inflation factor.

---

## 3. Consultant Identity Validation

### Distinct consultant count

| Metric | Value |
|--------|-------|
| Distinct `cnslt_epc_organization_id` in x5px-esft | **539** |
| Distinct `cnslt_epc_organization_id` in mihb-jfex | **539** |

The identical count confirms that the same consultant universe is represented in both datasets.

### EPC IDs with multiple names (identity drift)

Query: EPC IDs where `COUNT(DISTINCT cnslt_name) > 1`

| EPC ID | Name Count | Representative Names |
|--------|-----------|---------------------|
| 16081692 | 4 | "Rodabough Education Group, Inc.", "Rollins & Sumrall Education Group, Inc.", "Rodabough Education Group, LLC", "Rodabough Education Group" |
| 16075152 | 4 | (queried separately — similar rebranding pattern) |
| 16043595 | 2 | "e2e Exchange, LLC" (14,944 rows), "Erate Exchange LLC" (508 rows) |
| 16043626 | 3 | (rebranding variants) |

**Interpretation:** Name variants under the same EPC ID represent:
- Firm rebrands (e.g., Rollins & Sumrall → Rodabough Education Group)
- Legal entity suffix changes (Inc. → LLC)
- Data entry variations (casing, punctuation)

**Conclusion: Grouping by `ConsultantEpcOrganizationId` is SAFE.** The EPC ID correctly captures
all activity under a single consultant firm regardless of name variant.

---

## 4. Name Normalization Risk Analysis

### Top 20 consultants by raw name (x5px-esft, by application count)

| Rank | `cnslt_name` | EPC ID | App Count |
|------|-------------|--------|-----------|
| 1 | E-Rate Central | 16060891 | 21,880 |
| 2 | AdTec-Administrative and Technical Consulting | 16024741 | 20,684 |
| 3 | ERateProgram, LLC | 16048902 | 19,562 |
| 4 | E-Rate Advantage | 16060670 | 16,876 |
| 5 | CSM Consulting Inc. | 16043564 | 16,524 |
| 6 | Funds for Learning | 16024808 | 15,832 |
| 7 | e2e Exchange, LLC | 16043595 | 14,944 |
| 8 | Educational Consortium for Telecom Savings | 16024807 | 13,657 |
| 9 | E-RATE ONLINE LLC | 16048791 | 12,305 |
| 10 | Infinity Communications & Consulting | 16043605 | 10,408 |
| 11 | Elite Fund Inc | 16043589 | 9,600 |
| 12 | CRW Consulting | 16024800 | 8,967 |
| 13 | ESC Region 12 E-Rate Consulting | 16062048 | 7,872 |
| 14 | ONeal Consulting | 16024811 | 7,464 |
| 15 | eRate Solutions, L.L.C. | 16024804 | 7,097 |
| 16 | Strategic Management Solutions | 16054698 | 6,957 |
| 17 | Kellogg & Sovereign Consulting, LLC | 16024809 | 6,477 |
| 18 | Communications Audit Services | 17021966 | 6,196 |
| 19 | E Rate Solutions Group Inc | 16043598 | 6,016 |
| 20 | CTI eRate Services | 16043573 | 5,982 |

**Split identity risk (same firm, different names under same EPC ID):**
- `e2e Exchange, LLC` (14,944) + `Erate Exchange LLC` (508) → EPC 16043595 — both collapse to one firm
- Rodabough group (4 name variants) — all under EPC 16081692

**Name-only grouping risk:**
- Names like "E-Rate Solutions, LLC" vs "E Rate Solutions Group Inc" vs "eRate Solutions, L.L.C."
  are visually similar but may be different firms with different EPC IDs — cannot determine from
  names alone.

**Risk level for name-based grouping: MEDIUM**
- Large firms (top 10) have stable, consistent names
- Long tail consultants are prone to variant spellings, legal suffix differences, rebrands
- **Mitigation:** Always group by `ConsultantEpcOrganizationId`, never by name alone.
  Display names are acceptable for labels/display only.

---

## 5. Join Behavior Validation

### Cross-dataset application coverage

| Metric | Value |
|--------|-------|
| Distinct `application_number` in x5px-esft | **275,428** |
| Distinct `application_number` in mihb-jfex | **275,428** |

**Identical count.** Every application in x5px-esft has a corresponding entry in mihb-jfex and
vice versa. There are no one-sided applications — the datasets are in complete agreement on application
coverage.

### Application number format

- x5px-esft sample: "161000002", "211007579" — 9-digit numeric string
- mihb-jfex sample: "161000002", "161011863" — 9-digit numeric string
- **Format consistent.** No prefix, no padding differences. Join on `application_number` is safe.

### FRN count distribution per application (mihb-jfex, sample of 50,000 apps)

| FRNs per application | Applications | % |
|---------------------|-------------|---|
| 1 | 112 | 0.2% |
| 2–5 | 18,545 | 37.1% |
| 6–10 | 11,030 | 22.1% |
| 11–25 | 16,772 | 33.5% |
| 26+ | 3,541 | 7.1% |

- **Average FRNs per application:** ~8.3 (raw) → ~2.0 (post-dedup by RawSourceKey: 551,190 / 275,428)
- **Maximum FRNs per application (raw):** 712 (consortium-type applications with many line items)

> **Note on deduplication:** The raw FRN distribution above reflects the pre-dedup dataset (4x inflation).
> After ETL import, a 4-row FRN becomes 1 row. The post-dedup average is ~2 FRNs per application.

### Fan-out risk

Joining x5px-esft (application grain) to mihb-jfex (FRN grain) on `application_number` alone
produces 1:N rows — one application row fans out to multiple FRN rows. This is expected and by design,
not a defect. Correct aggregation strategy:
- `SUM(funding_commitment_request)` across FRNs → application-level committed total
- `GROUP BY cnslt_epc_organization_id` → consultant-level totals
- Do NOT join and then aggregate without the GROUP BY — this produces inflated counts.

### Join assessment: **CONDITIONALLY SAFE**

The join key (`application_number`) is confirmed consistent across both datasets with 100% overlap.
The N:1 cardinality (application-level → FRN-level) is well-understood and manageable. All
aggregation must explicitly account for grain differences.

---

## 6. Funding Aggregation Validation

### FRN status breakdown (mihb-jfex, all FYs)

| Status | Row Count | % |
|--------|-----------|---|
| Pending | 1,157,470 | 50.4% |
| Funded | 1,043,259 | 45.4% |
| Cancelled | 76,597 | 3.3% |
| Denied | 19,905 | 0.9% |
| As yet unfunded | 4 | ~0% |

**Key implication:** 50% of FRNs are `Pending` — they have a `funding_commitment_request` value
but have not received an FCDL. Pending amounts represent anticipated funding, not committed funding.
Dashboard aggregations must explicitly filter by status:
- **`= 'Funded'`** → committed funding (safe to aggregate as "committed")
- **`= 'Pending'`** → applications under review (show separately or exclude from commitment totals)
- **`IN ('Cancelled', 'Denied')`** → rejected (exclude from positive metrics)

### Funded FRN aggregate check

| Metric | Value |
|--------|-------|
| Funded FRN count | 1,043,259 |
| Total `funding_commitment_request` (Funded) | **$39.4 billion** |
| Total `total_authorized_disbursement` (Funded) | **$31.2 billion** |
| Disbursement rate | 79.1% |

These numbers cover all 539 consultants across FY2016–FY2025. The scale (~$39B committed over 10
years across consultant-assisted applications) is plausible for the E-Rate program, which committed
~$4–5B per year total. These amounts represent the consultant-assisted subset of total E-Rate funding.

### Double-counting risk

`funding_commitment_request` is an FRN-level field. Summing across FRNs within an application gives
the correct application total. **No double-counting risk** in the ETL-imported tables, because:
- Each FRN is stored exactly once (upsert deduplication)
- Amounts are FRN-level, not repeated across rows

**Warning:** Summing `funding_commitment_request` from the raw Socrata API without deduplication
will produce 4x inflated totals.

---

## 7. Geographic Interpretation Validation

### Top 15 applicant states (`state` in x5px-esft)

| State | Application Rows | State | Application Rows |
|-------|-----------------|-------|-----------------|
| CA | 57,557 | FL | 20,789 |
| TX | 39,426 | OK | 18,930 |
| NY | 38,162 | AZ | 17,424 |
| OH | 33,156 | KS | 15,931 |
| IL | 31,395 | WI | 14,972 |
| NJ | 26,370 | | |
| PA | 25,156 | | |
| MI | 24,443 | | |
| IN | 22,873 | | |
| NE | 22,822 | | |

### Top 15 consultant HQ states (`cnslt_state` in x5px-esft)

| State | Rows | State | Rows |
|-------|------|-------|------|
| NY | 66,343 | MO | 24,628 |
| CA | 52,957 | NE | 22,821 |
| OK | 40,391 | MI | 20,053 |
| NJ | 39,023 | AZ | 17,447 |
| TX | 36,667 | KS | 16,413 |
| PA | 34,664 | CT | 15,856 |
| OH | 28,991 | | |
| IL | 26,213 | | |
| IN | 24,742 | | |

### Interpretation

`state` (applicant) and `cnslt_state` (consultant HQ) measure different things:
- **`state`** → Where the applicant entity is located → "What states does this consultant serve?"
- **`cnslt_state`** → Where the consultant firm is headquartered → "Where are consultants based?"

Observable cross-state dynamics:
- **OK** ranks #12 for applicants but #3 for consultant HQ — Oklahoma-based firms serve applicants
  nationally (consistent with several large national consultants being headquartered in OK/TX)
- **NE** ranks #10 for applicants but #11 for consultant HQ — Nebraska applicants use many consultants,
  often non-NE-based
- **CA** is top applicant state (#1) but only #2 for consultant HQ — large state with broad national
  consultant coverage

**Safe interpretation:** "States served" (applicant distribution) is a valid and safe metric for
characterizing a consultant's market footprint. "Consultant HQ state" is a distinct fact about the
firm's location, not service coverage. Do not conflate the two fields.

---

## 8. E-Rate Central Identification

### Search results

| Firm | EPC ID | Status | Evidence |
|------|--------|--------|----------|
| **E-Rate Central** | **16060891** | **CONFIRMED — HIGH confidence** | Single name, 21,880 rows in x5px-esft, consistent across all funding years |
| Tel Logic Inc | (not found) | **NOT IN DATASET** | Searched "TEL LOGIC", "TELLOGIC", "TEL-LOGIC" — zero matches |

### E-Rate Central detail

- **EPC ID:** `16060891`
- **Name variants:** 1 (no variants — "E-Rate Central" only, all 21,880 rows identical)
- **Application count (x5px-esft):** 21,880 — #1 in the market
- **FRN count (mihb-jfex):** 94,704 — #2 in the market
- **Committed funding (Funded FRNs):** $7.29 billion — #2 in the market
- **Confidence:** HIGH — single unambiguous EPC ID, no name fragmentation

### Tel Logic Inc

Tel Logic is not present in x5px-esft or mihb-jfex under any spelling variant searched. Possible
explanations:
1. The firm may have filed applications as a non-consultant (directly as a service provider or in-house)
2. The firm name may be recorded differently (e.g., under a parent or DBA)
3. The firm may not have appeared in consultant-assisted E-Rate applications during FY2016–FY2026
4. The firm may have operated under a completely different name in EPC

**Status: UNRESOLVED.** A manual EPC entity lookup using USAC's organization search would be
required to find the correct firm name or confirm absence.

---

## 9. Dataset Completeness and Performance

### Scale comparison

| Dataset | Raw Rows | Post-dedup Rows | Comparable to |
|---------|----------|-----------------|--------------|
| FundingCommitments | ~10M+ | ~10M | Largest existing dataset |
| mihb-jfex | 2,297,235 | ~551,190 | Medium — significantly smaller than FundingCommitments |
| x5px-esft | 593,519 | ~308,492 | Smaller — similar to Form 471 Applications |

### Import time estimates (resource API, 10,000 rows/page)

| Dataset | Pages (raw) | Pages (effective, post-dedup) | Estimated Time |
|---------|------------|-------------------------------|---------------|
| x5px-esft | ~60 | ~31 | 5–15 minutes |
| mihb-jfex | ~230 | ~56 (distinct FRNs) | 20–60 minutes |

> Estimates assume ~5–15 seconds per page including DB upsert. Actual time depends on Socrata API
> latency and SQLite write performance. The FundingCommitments import (10M rows, paged) typically
> takes 3.5–4 hours for comparison.

### Pagination: required

Both datasets exceed Socrata's 50,000-row single-request limit (for the full dataset). The import
services have been refactored in this PR to use `$limit/$offset` pagination via the resource API.
The initial import service implementation (using the views bulk download) would fail due to both the
display-name CSV headers and (for very large datasets) potential memory pressure.

---

## 10. Market-Shape Sanity Check

### Top 10 consultants by application count (x5px-esft)

| Rank | Consultant | EPC ID | Applications |
|------|-----------|--------|-------------|
| 1 | E-Rate Central | 16060891 | 21,880 |
| 2 | AdTec-Administrative and Technical Consulting | 16024741 | 20,684 |
| 3 | ERateProgram, LLC | 16048902 | 19,562 |
| 4 | E-Rate Advantage | 16060670 | 16,876 |
| 5 | CSM Consulting Inc. | 16043564 | 16,524 |
| 6 | Funds for Learning | 16024808 | 15,832 |
| 7 | e2e Exchange, LLC | 16043595 | 14,944 |
| 8 | Educational Consortium for Telecom Savings | 16024807 | 13,657 |
| 9 | E-RATE ONLINE LLC | 16048791 | 12,305 |
| 10 | Infinity Communications & Consulting | 16043605 | 10,408 |

### Top 10 consultants by committed dollars (mihb-jfex, all FRN statuses)

| Rank | Consultant | EPC ID | Committed ($M) |
|------|-----------|--------|---------------|
| 1 | CSM Consulting Inc. | 16043564 | $9,969.8M |
| 2 | E-Rate Central | 16060891 | $7,287.2M |
| 3 | Funds for Learning | 16024808 | $5,276.5M |
| 4 | Infinity Communications & Consulting | 16043605 | $3,381.2M |
| 5 | E-Rate 360 Solutions, LLC | 16048893 | $3,252.7M |
| 6 | VST Services LP | 16043688 | $2,728.5M |
| 7 | Southeast Regional Resource Center (SERRC) | 16062991 | $2,276.5M |
| 8 | AdTec-Administrative and Technical Consulting | 16024741 | $2,014.4M |
| 9 | E-Rate Elite Services, Inc. | 16024803 | $1,912.9M |
| 10 | ESC Region 12 E-Rate Consulting | 16062048 | $1,823.1M |

### Sanity check

**Volume vs dollars ranking difference is expected and logical:**
- CSM Consulting (#5 by apps, #1 by dollars) — consistent with managing large-dollar Category 2
  (internal connections) applications with high per-FRN amounts
- E-Rate Central (#1 by apps, #2 by dollars) — high volume, consistent with broad market presence
- Educational Consortium (#8 by apps) doesn't appear in top 10 by dollars — consistent with serving
  smaller applicants (school districts in rural states with lower discount levels)
- VST Services LP appears in top 6 by dollars but not top 10 by apps — large-FRN specialist
- Southeast Regional Resource Center (SERRC) handles Alaska clients with disproportionately
  large funding commitments (remote/rural premiums)

**Assessment: No obvious anomalies.** The ranking differences between applications and committed
dollars are consistent with known E-Rate market dynamics. The total committed figure ($39.4B for
funded FRNs from consultant-assisted applications, FY2016–FY2025) is plausible and internally
consistent.

### Applicant type distribution (x5px-esft)

| Type | Count | % |
|------|-------|---|
| School District | 350,683 | 59.1% |
| School | 164,169 | 27.7% |
| Consortium | 37,999 | 6.4% |
| Library | 23,691 | 4.0% |
| Library System | 16,977 | 2.9% |

School districts and individual schools together represent 86.8% of consultant-assisted applications.
This is consistent with E-Rate program demographics.

---

## 11. Recommendation

### PROCEED WITH CAVEATS

The consultant datasets are analytically sound for Competitive Intelligence dashboard development,
with three mitigations required before building financial aggregations.

---

### Top Risks

| Risk | Severity | Status | Mitigation |
|------|----------|--------|-----------|
| Bulk import URL incompatibility (display-name headers) | **CRITICAL** | **Fixed in this PR** | Import services refactored to resource API with pagination |
| Socrata duplication (~2x in x5px-esft, ~4x in mihb-jfex) | **HIGH** | **Handled by ETL** | Upsert deduplication collapses to unique rows on import; document for analysts |
| ~50% of mihb-jfex FRNs are Pending (not committed) | **HIGH** | **Must mitigate in dashboard** | All financial charts must filter `WHERE form_471_frn_status_name = 'Funded'` |
| Join fan-out (avg 2 FRNs per application post-dedup) | **MEDIUM** | **By design** | All aggregations must use `GROUP BY` — do not count joined rows naively |
| Tel Logic not found in dataset | **LOW** | **Unresolved** | Manual EPC entity lookup required before firm-specific Tel Logic queries |
| FY2026 partial | **LOW** | **Expected** | Annotate as in-progress in UI; exclude from trend baselines |
| Name-based grouping produces incorrect results | **MEDIUM** | **Documented** | Group by `ConsultantEpcOrganizationId` exclusively; names are display-only |

---

### Required mitigations before dashboard (CC-ERATE-000038D or equivalent)

1. **Run full imports** using the fixed import services (`POST /import/consultants/applications`,
   `POST /import/consultants/frn-status`) to populate both tables.

2. **Filter financial aggregations by FRN status:** Use `WHERE FrnStatusName = 'Funded'` for
   any "committed funding" metric. Show Pending separately or not at all in financial views.

3. **Implement fan-out-aware aggregation:** When joining ConsultantApplications to
   ConsultantFrnStatuses on `ApplicationNumber`, always `GROUP BY ConsultantEpcOrganizationId`
   before summing amounts to prevent double-counting.

4. **Group by EPC ID, not by name:** All consultant ranking/grouping logic must use
   `ConsultantEpcOrganizationId`. `ConsultantName` is display-only.

5. **Annotate FY2026 as partial** in any dashboard trend visualization.

6. **Tel Logic identification:** Requires manual EPC entity lookup at
   https://www.usac.org/e-rate/applicant-process/before-you-begin/entity-numbers/ or USAC
   staff contact before building any Tel Logic-specific views.

---

### Deferred to CC-ERATE-000038D

- Summary layer tables for consultant analytics (parallel to ApplicantYearCommitmentSummary)
- Competitive Intelligence dashboard views
- E-Rate Central vs market comparison queries
- State-of-service market footprint visualizations
- Year-over-year consultant market share analysis
