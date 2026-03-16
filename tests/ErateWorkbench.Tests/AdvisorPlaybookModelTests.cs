using ErateWorkbench.Api.Pages;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for the dedicated Advisor Playbook page model and phase data.
/// </summary>
public class AdvisorPlaybookModelTests
{
    private static IReadOnlyList<PlaybookPhase> Phases => AdvisorPlaybookData.Phases;

    // -----------------------------------------------------------------------
    // Phase count and ordering
    // -----------------------------------------------------------------------

    [Fact]
    public void Phases_HasEightEntries_ZeroThroughSeven()
    {
        Assert.Equal(8, Phases.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], Phases.Select(p => p.Number));
    }

    // -----------------------------------------------------------------------
    // State progression — Complete → Current → Upcoming
    // -----------------------------------------------------------------------

    [Fact]
    public void ExactlyOnePhase_IsMarkedCurrent()
    {
        Assert.Single(Phases, p => p.State == PlaybookState.Current);
    }

    [Fact]
    public void Phase3_IsCurrentPhase()
    {
        Assert.Equal(PlaybookState.Current, Phases.Single(p => p.Number == 3).State);
    }

    [Fact]
    public void PhasesBeforeCurrent_AreComplete()
    {
        var current = Phases.Single(p => p.State == PlaybookState.Current);
        var before = Phases.Where(p => p.Number < current.Number);
        Assert.All(before, p => Assert.Equal(PlaybookState.Complete, p.State));
    }

    [Fact]
    public void PhasesAfterCurrent_AreUpcoming()
    {
        var current = Phases.Single(p => p.State == PlaybookState.Current);
        var after = Phases.Where(p => p.Number > current.Number);
        Assert.All(after, p => Assert.Equal(PlaybookState.Upcoming, p.State));
    }

    // -----------------------------------------------------------------------
    // Every phase has full operational content
    // -----------------------------------------------------------------------

    [Fact]
    public void AllPhases_HaveObjective()
    {
        Assert.All(Phases, p => Assert.False(string.IsNullOrWhiteSpace(p.Objective)));
    }

    [Fact]
    public void AllPhases_HaveFourKeyTasks()
    {
        Assert.All(Phases, p => Assert.Equal(4, p.KeyTasks.Length));
    }

    [Fact]
    public void AllPhases_HaveAtLeastOneOwner()
    {
        Assert.All(Phases, p => Assert.NotEmpty(p.Owners));
    }

    [Fact]
    public void AllPhases_HaveRiskNote()
    {
        Assert.All(Phases, p => Assert.False(string.IsNullOrWhiteSpace(p.RiskNote)));
    }

    [Fact]
    public void AllPhases_HaveExitCriteria()
    {
        Assert.All(Phases, p => Assert.False(string.IsNullOrWhiteSpace(p.ExitCriteria)));
    }

    // -----------------------------------------------------------------------
    // ShortLabels match the expected journey strip order
    // -----------------------------------------------------------------------

    [Fact]
    public void ShortLabels_MatchExpectedJourneyOrder()
    {
        var expected = new[] { "Intake", "Planning", "Form 470", "Form 471", "PIA Review", "FCDL", "Invoicing", "Compliance" };
        Assert.Equal(expected, Phases.Select(p => p.ShortLabel));
    }

    // -----------------------------------------------------------------------
    // Owner values are all recognizable by the OwnerBadge color map
    // -----------------------------------------------------------------------

    private static readonly string[] KnownOwners = ["E-Rate Central", "Client", "USAC", "Shared", "Audit Risk"];

    [Fact]
    public void AllOwners_AreRecognizedByOwnerBadge()
    {
        var unknownOwners = Phases
            .SelectMany(p => p.Owners)
            .Where(o => !KnownOwners.Contains(o))
            .Distinct()
            .ToList();

        Assert.True(unknownOwners.Count == 0,
            $"Unrecognized owners: {string.Join(", ", unknownOwners)}");
    }
}
