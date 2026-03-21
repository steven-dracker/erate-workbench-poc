using ErateWorkbench.Domain;
using Microsoft.EntityFrameworkCore;

namespace ErateWorkbench.Infrastructure;

/// <summary>
/// Analytics queries backing the Filing Window dashboard.
/// All queries draw from Form471Applications (demand) and FundingCommitments (supply).
/// </summary>
public class FilingWindowRepository(AppDbContext db)
{
    /// <summary>
    /// Daily submission counts by day-of-year and funding year, used to draw
    /// cumulative certification curves on the dashboard.
    ///
    /// Day numbering: day 1 = January 1 of the funding year.
    /// Data quality filters applied:
    ///   - CertificationDate must be non-null.
    ///   - CertificationDate must be before July 1 of FY+1 to exclude rare
    ///     late-certification outliers (audit identified 5 records beyond this threshold).
    ///   - FundingYear >= 2020 (earliest available in 9s6i-myen).
    ///
    /// NOTE: FY2020 contains a known COVID window extension spike in Sept–Oct 2020;
    /// the caller should surface this as an annotation in the UI.
    /// </summary>
    public async Task<List<FilingWindowDayRow>> GetSubmissionTimingAsync(
        CancellationToken cancellationToken = default)
    {
        // Pull raw (FundingYear, CertificationDate) pairs. SQLite cannot compute
        // day-of-year in a EF-translatable expression, so we do it in memory.
        var raw = await db.Form471Applications
            .Where(a => a.CertificationDate != null && a.FundingYear >= 2020)
            .Select(a => new { a.FundingYear, CertDate = a.CertificationDate!.Value })
            .ToListAsync(cancellationToken);

        return raw
            .Where(r => r.CertDate < new DateTime(r.FundingYear + 1, 7, 1))  // exclude late-cert outliers
            .GroupBy(r => new
            {
                r.FundingYear,
                Day = (r.CertDate.Date - new DateTime(r.FundingYear, 1, 1)).Days + 1,
            })
            .Select(g => new FilingWindowDayRow(g.Key.FundingYear, g.Key.Day, g.Count()))
            .OrderBy(r => r.FundingYear)
            .ThenBy(r => r.DayOfYear)
            .ToList();
    }

    /// <summary>
    /// Total requested amount (Form 471) vs total committed amount (Funding Commitments)
    /// grouped by funding year, newest year first.
    /// SQLite does not support Sum on decimal; cast to double in SQL, convert in memory.
    /// </summary>
    public async Task<List<RequestedVsCommittedRow>> GetRequestedVsCommittedByYearAsync(
        CancellationToken cancellationToken = default)
    {
        var requested = await db.Form471Applications
            .GroupBy(a => a.FundingYear)
            .Select(g => new
            {
                Year = g.Key,
                AppCount = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var committed = await db.FundingCommitments
            .GroupBy(c => c.FundingYear)
            .Select(g => new
            {
                Year = g.Key,
                TotalDouble = g.Sum(c => (double?)c.CommittedAmount ?? 0.0),
            })
            .ToListAsync(cancellationToken);

        var committedLookup = committed.ToDictionary(r => r.Year, r => r.TotalDouble);

        return requested
            .OrderByDescending(r => r.Year)
            .Select(r => new RequestedVsCommittedRow(
                r.Year,
                r.AppCount,
                (decimal)r.TotalDouble,
                (decimal)committedLookup.GetValueOrDefault(r.Year, 0.0)))
            .ToList();
    }

    /// <summary>
    /// Application status breakdown by funding year (newest first), descending count within year.
    /// Rows where ApplicationStatus is null are excluded.
    /// </summary>
    public async Task<List<AppStatusRow>> GetApplicationStatusByYearAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Form471Applications
            .Where(a => a.ApplicationStatus != null)
            .GroupBy(a => new { a.FundingYear, a.ApplicationStatus })
            .Select(g => new { g.Key.FundingYear, Status = g.Key.ApplicationStatus!, Count = g.Count() })
            .OrderByDescending(r => r.FundingYear)
            .ThenByDescending(r => r.Count)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AppStatusRow(r.FundingYear, r.Status, r.Count)).ToList();
    }

    /// <summary>
    /// Summary row for the current-year progress card comparing FY2026 vs FY2025.
    /// </summary>
    public async Task<Fy2026ProgressRow> GetFy2026ProgressAsync(
        CancellationToken cancellationToken = default)
    {
        var current = await db.Form471Applications
            .Where(a => a.FundingYear == 2026)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var prev = await db.Form471Applications
            .Where(a => a.FundingYear == 2025)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalDouble = g.Sum(a => (double?)a.RequestedAmount ?? 0.0),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new Fy2026ProgressRow(
            current?.Count ?? 0,
            (decimal)(current?.TotalDouble ?? 0.0),
            prev?.Count ?? 0,
            (decimal)(prev?.TotalDouble ?? 0.0));
    }
}

public record FilingWindowDayRow(int FundingYear, int DayOfYear, int Count);

public record RequestedVsCommittedRow(
    int FundingYear,
    int ApplicationCount,
    decimal TotalRequested,
    decimal TotalCommitted);

public record AppStatusRow(int FundingYear, string Status, int Count);

public record Fy2026ProgressRow(
    int Fy2026Count,
    decimal Fy2026Requested,
    int Fy2025Count,
    decimal Fy2025Requested);
