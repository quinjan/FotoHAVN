using FotoHavn.Core.GuestCycles;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace FotoHavn.Core.Tests.GuestCycles;

/// <summary>
/// The Guest Cycle model's safety invariants (plan section 18, Q23), checked against random action sequences that include stale
/// operation ids, stale revisions, out-of-range windows, and duplicate Captures. A step is one random action, interpreted against the
/// live model so the sequences reach complete strips, Retakes, and Moves instead of being ignored from the first action.
/// </summary>
public sealed class GuestCycleInvariantTests
{
    private const int Runs = 400;

    private static readonly (string Id, int Windows)[] Templates = [("signature-strip", 4), ("panorama-four", 4), ("butter-gingham", 3)];
    private static readonly string[] Looks = ["naturale-v1", "sepia-v1", "monochrome-v1"];

    /// <summary>One random step: a kind (which action) and three numbers its interpretation reads.</summary>
    public sealed record RandomStep(int Kind, int A, int B, int C);

    /// <summary>A random action sequence.</summary>
    public sealed record Script(IReadOnlyList<RandomStep> Steps)
    {
        public override string ToString() => string.Join(" ", Steps.Select(step => $"{step.Kind}:{step.A}.{step.B}.{step.C}"));
    }

    /// <summary>What a check sees after each step.</summary>
    public sealed record Observed(
        int Index,
        GuestCycleModel Before,
        GuestCycleAction Action,
        GuestCycleOutcome Outcome,
        IReadOnlySet<string> Known,
        IReadOnlySet<string> Discarded)
    {
        public GuestCycleModel After => Outcome.Model;

        public override string ToString() => $"step {Index}: {Action} (applied {Outcome.Applied})";
    }

    public static class Generators
    {
        internal static readonly (int Weight, int Kind)[] Kinds =
        [
            (2, 0), (5, 1), (3, 2), (14, 3), (3, 6), (2, 7), (4, 8), (2, 9), (1, 10), (2, 11), (4, 12), (6, 14), (5, 15), (1, 16), (2, 17), (7, 18), (2, 19),
        ];

        public static Arbitrary<Script> Scripts()
        {
            var step = Gen.Frequency(Kinds.Select(entry => (entry.Weight, Gen.Constant(entry.Kind))))
                .SelectMany(kind => Gen.Choose(0, 9).SelectMany(a => Gen.Choose(0, 9).SelectMany(b => Gen.Choose(0, 9).Select(c => new RandomStep(kind, a, b, c)))));
            return Gen.ArrayOf(step, 60).Select(steps => new Script(steps)).ToArbitrary();
        }
    }

    /// <summary>Runs <paramref name="script"/> from a fresh cycle, calling <paramref name="check"/> after every action.</summary>
    private static void Run(Script script, Action<Observed> check)
    {
        var model = GuestCycleModel.Create("signature-strip", 4, "naturale-v1");
        var known = new HashSet<string>(StringComparer.Ordinal);
        var discarded = new HashSet<string>(StringComparer.Ordinal);
        var nextOperation = 1L;
        var nextCapture = 1;

        for (var index = 0; index < script.Steps.Count; index++)
        {
            var action = Interpret(script.Steps[index], model, ref nextOperation, ref nextCapture, known);
            var outcome = GuestCycleRules.Apply(model, action);
            if (outcome.Applied && action is CaptureCommittedAction committed)
            {
                known.Add(committed.Capture.Id);
                // The generator re-commits a discarded id; the model keeps no record of discards, so the id is kept again, not discarded.
                discarded.Remove(committed.Capture.Id);
            }

            foreach (var id in outcome.DiscardedCaptureIds)
            {
                discarded.Add(id);
            }

            check(new Observed(index, model, action, outcome, known, discarded));
            model = outcome.Model;
        }
    }

    private static GuestCycleAction Interpret(RandomStep step, GuestCycleModel model, ref long nextOperation, ref int nextCapture, HashSet<string> known)
    {
        var window = (step.A % 6) - 1;
        var currentOperation = model.Operation?.Id ?? 0;
        var staleOperation = step.C % 4 == 0;
        var operationId = staleOperation ? currentOperation + 100 : currentOperation;

        switch (step.Kind)
        {
            case 0:
                var template = Templates[step.A % Templates.Length];
                return new SelectTemplateAction(template.Id, template.Windows);
            case 1:
                return new BeginSequenceAction(nextOperation++);
            case 2:
                return new BeginRetakeAction(nextOperation++, window);
            case 3:
                var id = step.B % 5 == 0 && known.Count > 0 ? known.Order().ElementAt(step.A % known.Count) : $"c{nextCapture++}";
                var target = model.Operation is { Target: { } retake } && step.B % 3 != 0 ? retake : model.NextEmptyWindow is { } empty && step.B % 3 != 0 ? empty : window;
                return new CaptureCommittedAction(operationId, target, new KeptCapture(id, nextCapture, $"https://media.fotohavn/c/{id}.jpg", 1640, 1230));
            case 6:
                return new FinishSequenceAction(operationId);
            case 7:
                return new CancelAction();
            case 8:
                return new AcceptRetakeAction();
            case 9:
                return new TryAgainAction();
            case 10:
                return new KeepOriginalAction();
            case 11:
                return new SelectWindowAction(window);
            case 12:
                return new MoveStartAction(window);
            case 14:
                return new MoveToAction(window);
            case 15:
                var source = step.A % 5;
                return new DragPlaceAction(
                    source,
                    source < model.WindowCount && model.Windows[source] is { } dragged && step.B % 5 != 0 ? dragged : "missing",
                    step.C % 5 == 0 ? model.Revision + 1 : model.Revision,
                    (step.B % 6) - 1);
            case 16:
                return new MoveCancelAction();
            case 17:
                return new SelectLookAction(Looks[step.A % Looks.Length]);
            case 18:
                return new GoToStepAction(step.B % 2 == 0 ? (GuestCycleStep)Math.Min((int)model.Step + 1, 3) : (GuestCycleStep)(step.A % 4));
            default:
                return new ShowTabAction((PhotographsTab)(step.A % 2));
        }
    }

    private static bool Filled(GuestCycleModel model, int index) => model.Windows[index] is { } id && model.KeptCaptures.ContainsKey(id);

    private static bool SameArrangement(GuestCycleModel a, GuestCycleModel b) =>
        a.TemplateId == b.TemplateId && a.Windows.SequenceEqual(b.Windows);

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool Every_window_Capture_is_kept_and_appears_once_across_the_windows(Script script)
    {
        Run(script, seen =>
        {
            var model = seen.After;
            var placed = model.Windows.OfType<string>().ToList();
            Assert.True(placed.All(model.KeptCaptures.ContainsKey), seen.ToString());
            Assert.Equal(placed.Count, placed.Distinct().Count());
            Assert.True(model.Operation?.Candidate is null || model.KeptCaptures.ContainsKey(model.Operation.Candidate), seen.ToString());
            Assert.True(model.Operation?.Candidate is null || !placed.Contains(model.Operation.Candidate), seen.ToString());
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool The_window_list_always_has_one_entry_per_Template_window(Script script)
    {
        Run(script, seen => Assert.True(seen.After.Windows.Count == seen.After.WindowCount, seen.ToString()));
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool A_stale_or_cancelled_operation_never_changes_the_windows(Script script)
    {
        Run(script, seen =>
        {
            if (seen.Action is CaptureCommittedAction commit && seen.Before.Operation?.Id != commit.OperationId)
            {
                Assert.False(seen.Outcome.Applied, seen.ToString());
                Assert.Same(seen.Before, seen.After);
            }

            if (seen.Action is FinishSequenceAction finish && seen.Before.Operation?.Id != finish.OperationId)
            {
                Assert.Same(seen.Before, seen.After);
            }

            if (seen.Before.Operation is null && seen.Action is CaptureCommittedAction)
            {
                Assert.Equal(seen.Before.Windows, seen.After.Windows);
            }
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool During_a_Retake_the_original_stays_in_its_window_until_the_Retake_is_accepted(Script script)
    {
        Run(script, seen =>
        {
            if (seen.Before.Operation is not { Mode: OperationMode.Retake, Target: { } target } operation)
            {
                return;
            }

            if (seen.Action is AcceptRetakeAction && seen.Outcome.Applied)
            {
                Assert.Equal(operation.Candidate, seen.After.Windows[target]);
                Assert.Equal([seen.Before.Windows[target]!], seen.Outcome.DiscardedCaptureIds);
            }
            else
            {
                Assert.Equal(seen.Before.Windows[target], seen.After.Windows[target]);
            }
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool No_kept_Capture_is_ever_lost_kept_is_arranged_plus_candidate_plus_explicitly_discarded(Script script)
    {
        Run(script, seen =>
        {
            var model = seen.After;
            var kept = model.Windows.OfType<string>().Concat(model.Operation?.Candidate is { } candidate ? [candidate] : []).ToHashSet();

            Assert.Equal(seen.Known.Order(), kept.Union(seen.Discarded).Order());
            Assert.Empty(kept.Intersect(seen.Discarded));
            Assert.Equal(kept.Order(), model.KeptCaptures.Keys.Order());
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool Revision_increases_by_exactly_one_per_arrangement_change_and_never_otherwise(Script script)
    {
        Run(script, seen =>
        {
            var expected = seen.Before.Revision + (SameArrangement(seen.Before, seen.After) ? 0 : 1);
            Assert.True(seen.After.Revision == expected, $"{seen}: revision {seen.Before.Revision} to {seen.After.Revision}");
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool Continuing_is_possible_only_with_every_window_filled_and_nothing_pending(Script script)
    {
        Run(script, seen =>
        {
            var model = seen.After;
            if (model.CanContinue)
            {
                Assert.True(Enumerable.Range(0, model.WindowCount).All(index => Filled(model, index)), seen.ToString());
                Assert.Null(model.Operation);
                Assert.Null(model.Move);
            }

            if (model.Step is GuestCycleStep.Filters or GuestCycleStep.Keepsake)
            {
                Assert.True(Enumerable.Range(0, model.WindowCount).All(index => Filled(model, index)), seen.ToString());
            }
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool The_cycle_never_goes_back_to_an_earlier_step_and_the_Template_never_changes_after_it(Script script)
    {
        Run(script, seen =>
        {
            Assert.True(seen.After.Step >= seen.Before.Step, seen.ToString());
            if (seen.Before.Step != GuestCycleStep.Template)
            {
                Assert.Equal(seen.Before.TemplateId, seen.After.TemplateId);
            }
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool An_operation_and_a_Move_never_coexist_and_a_completed_cycle_never_changes(Script script)
    {
        Run(script, seen =>
        {
            Assert.False(seen.After.Operation is not null && seen.After.Move is not null, seen.ToString());
            if (seen.Before.Step == GuestCycleStep.Keepsake)
            {
                Assert.Same(seen.Before, seen.After);
            }
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = Runs)]
    public bool The_Photo_Strip_arrangement_of_a_complete_cycle_is_one_Capture_per_window_with_no_repeats(Script script)
    {
        Run(script, seen =>
        {
            if (seen.After.IsComplete)
            {
                Assert.Equal(seen.After.WindowCount, seen.After.ArrangedCaptureIds.Count);
                Assert.Equal(seen.After.ArrangedCaptureIds.Count, seen.After.ArrangedCaptureIds.Distinct().Count());
            }
        });
        return true;
    }

    [Fact]
    public void The_generator_reaches_complete_strips_Retakes_and_Moves()
    {
        var reached = new HashSet<string>();
        var random = new Random(42);
        for (var run = 0; run < 200; run++)
        {
            var script = new Script([.. Enumerable.Range(0, 60).Select(_ => new RandomStep(WeightedKind(random), random.Next(10), random.Next(10), random.Next(10)))]);
            Run(script, seen =>
            {
                reached.Add(seen.After.CanContinue ? "continue" : "no");
                reached.Add(seen.After.Operation is { Mode: OperationMode.Retake, Candidate: not null } ? "candidate" : "no");
                reached.Add(seen.After.Move is not null ? "move" : "no");
                reached.Add(seen.After.Step == GuestCycleStep.Keepsake ? "keepsake" : "no");
                reached.Add(seen.Action is AcceptRetakeAction && seen.Outcome.Applied ? "accepted" : "no");
            });
        }

        Assert.Superset(new HashSet<string> { "continue", "candidate", "move", "keepsake", "accepted" }, reached);
    }

    private static int WeightedKind(Random random)
    {
        var pick = random.Next(Generators.Kinds.Sum(entry => entry.Weight));
        foreach (var (weight, kind) in Generators.Kinds)
        {
            pick -= weight;
            if (pick < 0)
            {
                return kind;
            }
        }

        return 0;
    }
}
