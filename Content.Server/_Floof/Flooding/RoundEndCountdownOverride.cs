namespace Content.Server._Floof.Flooding;

/// <summary>
///     Replaces what the round end countdown stands for on a map without an evac shuttle.
///     Without a CallText the call is left for the override's owner to announce.
/// </summary>
public sealed record RoundEndCountdownOverride(
    TimeSpan Duration,
    string? CallText,
    string RecallText,
    string Sender,
    Action Finished);
