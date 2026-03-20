using ErateWorkbench.Domain;

namespace ErateWorkbench.Tests;

public class PartialYearDetectorTests
{
    [Fact]
    public void IsPartialYear_LatestYear_ReturnsTrue()
        => Assert.True(PartialYearDetector.IsPartialYear([2024, 2025, 2026], 2026));

    [Fact]
    public void IsPartialYear_PriorYear_ReturnsFalse()
        => Assert.False(PartialYearDetector.IsPartialYear([2024, 2025, 2026], 2025));

    [Fact]
    public void IsPartialYear_OldYear_ReturnsFalse()
        => Assert.False(PartialYearDetector.IsPartialYear([2024, 2025, 2026], 2024));

    [Fact]
    public void IsPartialYear_NoYearSelected_ReturnsFalse()
        => Assert.False(PartialYearDetector.IsPartialYear([2024, 2025, 2026], null));

    [Fact]
    public void IsPartialYear_EmptyYears_ReturnsFalse()
        => Assert.False(PartialYearDetector.IsPartialYear([], 2026));

    [Fact]
    public void IsPartialYear_SingleYear_ReturnsTrueWhenSelected()
        => Assert.True(PartialYearDetector.IsPartialYear([2026], 2026));

    [Fact]
    public void IsPartialYear_YearNotInList_ReturnsFalse()
        => Assert.False(PartialYearDetector.IsPartialYear([2024, 2025], 2026));
}
