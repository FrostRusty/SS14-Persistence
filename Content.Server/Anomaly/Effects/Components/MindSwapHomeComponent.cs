namespace Content.Server.Anomaly.Effects.Components;

/// <summary>
/// Lives on a Mind entity while it's away from its original body due to the Eye anomaly's pulse.
/// <see cref="HomeBody"/> is set ONCE, the first time this mind is ever displaced, and never
/// overwritten by later re-swaps - no matter how many times this mind gets bounced around by
/// subsequent pulses, it always remembers where it truly started. <see cref="ReturnAt"/> and
/// <see cref="SourceAnomaly"/> DO get refreshed on every subsequent swap, since those describe
/// "what's currently supposed to happen to me", not "where I'm truly from".
/// </summary>
[RegisterComponent]
public sealed partial class MindSwapHomeComponent : Component
{
    /// <summary>
    /// The body this mind will eventually return to. Set once and never changed thereafter.
    /// </summary>
    [DataField(required: true)]
    public EntityUid HomeBody;

    /// <summary>
    /// When this mind is next due to be sent home automatically. Refreshed to a freshly-rolled
    /// duration every time this mind is involved in a new swap. Having a stale/redundant timer
    /// left over from an earlier swap fire later is harmless - EyeAnomalySystem's return logic
    /// is idempotent and simply no-ops if the mind is already home by the time it checks.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan ReturnAt;

    /// <summary>
    /// Whichever Eye anomaly instance most recently displaced this mind - used to look up that
    /// instance's RevertOnCrit/RevertOnDeath toggles at the moment of a crit/death revert check.
    /// </summary>
    [DataField(required: true)]
    public EntityUid SourceAnomaly;
}
