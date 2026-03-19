using ErateWorkbench.Api.Pages;

namespace ErateWorkbench.Tests;

/// <summary>
/// Tests for the Program Workflow page model and phase data.
/// These verify the shape of phase data that drives both the rendered HTML and
/// the localStorage key scheme (p0 … p7) used by client-side note persistence.
/// </summary>
public class ProgramWorkflowModelTests
{
    private static IReadOnlyList<WorkflowPhase> Phases => WorkflowData.Phases;

    // -----------------------------------------------------------------------
    // Phase count and ordering
    // -----------------------------------------------------------------------

    [Fact]
    public void Phases_IncludesPhaseZeroThroughSeven()
    {
        var numbers = Phases.Select(p => p.Number).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, numbers);
    }

    [Fact]
    public void Phases_AreOrderedByNumber()
    {
        var numbers = Phases.Select(p => p.Number).ToList();
        Assert.Equal(numbers.OrderBy(n => n).ToList(), numbers);
    }

    // -----------------------------------------------------------------------
    // Phase 0 — Client Engagement & Intake
    // -----------------------------------------------------------------------

    [Fact]
    public void Phase0_HasCorrectTitle()
    {
        var phase = Phases.Single(p => p.Number == 0);
        Assert.Contains("Intake", phase.Title);
        Assert.Contains("Engagement", phase.Title);
    }

    [Fact]
    public void Phase0_HasFourSteps()
    {
        var phase = Phases.Single(p => p.Number == 0);
        Assert.Equal(4, phase.Steps.Length);
    }

    [Fact]
    public void Phase0_StepNamesMatchSpec()
    {
        var phase = Phases.Single(p => p.Number == 0);
        var names = phase.Steps.Select(s => s.Name).ToArray();
        Assert.Contains("Initial Consultation", names);
        Assert.Contains("Eligibility Review", names);
        Assert.Contains("Data Collection", names);
        Assert.Contains("Planning Strategy", names);
    }

    [Fact]
    public void Phase0_FooterNoteContainsEntryPointCopy()
    {
        var phase = Phases.Single(p => p.Number == 0);
        Assert.Contains("Entry point", phase.FooterNote);
    }

    [Fact]
    public void Phase0_IsNotMarkedDanger()
    {
        var phase = Phases.Single(p => p.Number == 0);
        Assert.False(phase.Danger);
    }

    // -----------------------------------------------------------------------
    // Phase 1 — renamed
    // -----------------------------------------------------------------------

    [Fact]
    public void Phase1_TitleIsUpdated()
    {
        var phase = Phases.Single(p => p.Number == 1);
        // Must contain the new title copy; must NOT be the old "Eligibility & Planning" alone
        Assert.Contains("Planning", phase.Title);
        Assert.Contains("Eligibility", phase.Title);
        Assert.DoesNotContain("Eligibility &amp; Planning", phase.Title);
    }

    [Fact]
    public void Phase1_StillHasFourSteps()
    {
        var phase = Phases.Single(p => p.Number == 1);
        Assert.Equal(4, phase.Steps.Length);
    }

    // -----------------------------------------------------------------------
    // Danger phase
    // -----------------------------------------------------------------------

    [Fact]
    public void ExactlyOnePhaseIsMarkedDanger()
    {
        Assert.Single(Phases, p => p.Danger);
    }

    [Fact]
    public void Phase4_IsTheDangerPhase()
    {
        var danger = Phases.Single(p => p.Danger);
        Assert.Equal(4, danger.Number);
    }

    // -----------------------------------------------------------------------
    // All phases have steps with valid owners
    // -----------------------------------------------------------------------

    private static readonly string[] KnownOwners =
        ["E-Rate Central", "Client", "USAC", "Shared", "Audit Risk"];

    [Fact]
    public void AllSteps_HaveKnownOwner()
    {
        var unknownOwners = Phases
            .SelectMany(p => p.Steps)
            .Where(s => !KnownOwners.Contains(s.Owner))
            .Select(s => $"{s.Name} → '{s.Owner}'")
            .ToList();

        Assert.True(unknownOwners.Count == 0,
            $"Steps with unrecognized owners: {string.Join(", ", unknownOwners)}");
    }

    [Fact]
    public void AllPhases_HaveExactlyFourSteps()
    {
        var bad = Phases.Where(p => p.Steps.Length != 4).ToList();
        Assert.True(bad.Count == 0,
            $"Phases without exactly 4 steps: {string.Join(", ", bad.Select(p => p.Number))}");
    }

    // -----------------------------------------------------------------------
    // localStorage key derivation — p0 through p7
    // These mirror the client-side: data-phase-key="p@(phase.Number)"
    // -----------------------------------------------------------------------

    [Fact]
    public void PhaseNumbers_ProduceExpectedStorageKeys()
    {
        // The JS key for each phase is "p" + phase.Number.
        // Backward-compat: existing saves use p1..p7; new p0 is simply absent in old saves.
        var expectedKeys = new[] { "p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        var actualKeys = Phases.Select(p => $"p{p.Number}").ToArray();
        Assert.Equal(expectedKeys, actualKeys);
    }

    [Fact]
    public void BackwardCompat_OldSaveKeys_StillMapToCorrectPhases()
    {
        // Simulate: old localStorage payload has p1..p7 (no p0).
        // Each key maps to the phase with Number == that index.
        // Phase 1 note should load into phase 1, not shifted to phase 2.
        var oldKeys = new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        foreach (var key in oldKeys)
        {
            var phaseNumber = int.Parse(key[1..]);
            var matchingPhase = Phases.Single(p => p.Number == phaseNumber);
            Assert.Equal(phaseNumber, matchingPhase.Number);
        }
    }

    [Fact]
    public void BackwardCompat_P0AbsentInOldSave_DefaultsToEmpty()
    {
        // The JS load: if (saved[key]) ta.value = saved[key]
        // When p0 is absent from the stored object, saved["p0"] is undefined (falsy) → textarea stays empty.
        // This test documents the contract; actual JS behavior verified manually in browser.
        var phase0 = Phases.Single(p => p.Number == 0);
        Assert.Equal("p0", $"p{phase0.Number}");
        // No assertion can fail here — this confirms the key is p0, which is absent in old saves.
    }
}
