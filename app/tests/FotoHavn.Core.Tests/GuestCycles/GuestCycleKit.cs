using FotoHavn.Core.GuestCycles;

namespace FotoHavn.Core.Tests.GuestCycles;

/// <summary>Builders shared by the Guest Cycle model tests: the website tests' <c>loaded()</c>, <c>move()</c>, and <c>photo()</c> helpers.</summary>
internal static class GuestCycleKit
{
    public const string Signature = "signature-strip";
    public const string Naturale = "naturale-v1";

    public static KeptCapture Capture(string id, int sequence = 1) =>
        new(id, sequence, $"https://media.fotohavn/events/e1/guest-cycles/c1/capture-{sequence}.jpg", 1640, 1230);

    /// <summary>A new cycle on the Template step with four empty windows.</summary>
    public static GuestCycleModel Fresh(int windows = 4) => GuestCycleModel.Create(Signature, windows, Naturale);

    /// <summary>A cycle on Photographs with no Captures.</summary>
    public static GuestCycleModel OnPhotographs(int windows = 4) => Fresh(windows).Do(new GoToStepAction(GuestCycleStep.Photographs));

    /// <summary>The website's <c>loaded()</c>: on Photographs, one finished sequence that committed <paramref name="ids"/> into windows 0, 1, 2, ...</summary>
    public static GuestCycleModel Loaded(params string[] ids)
    {
        var state = OnPhotographs().Do(new BeginSequenceAction(1));
        for (var index = 0; index < ids.Length; index++)
        {
            state = state.Do(new CaptureCommittedAction(1, index, Capture(ids[index], index + 1)));
        }

        return state.Do(new FinishSequenceAction(1));
    }

    public static GuestCycleModel Loaded() => Loaded("A", "B", "C", "D");

    /// <summary>The website's <c>move()</c>: a Move from one window to another.</summary>
    public static GuestCycleModel Move(this GuestCycleModel model, int from, int to) =>
        model.Do(new MoveStartAction(from)).Do(new MoveToAction(to));

    public static GuestCycleModel Do(this GuestCycleModel model, GuestCycleAction action) => GuestCycleRules.Apply(model, action).Model;

    public static GuestCycleOutcome Try(this GuestCycleModel model, GuestCycleAction action) => GuestCycleRules.Apply(model, action);

    public static string?[] Slots(this GuestCycleModel model) => [.. model.Windows];
}
