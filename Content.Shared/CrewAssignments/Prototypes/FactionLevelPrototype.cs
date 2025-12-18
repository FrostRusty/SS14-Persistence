using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CrewAssignments.Prototypes;

/// <summary>
/// This is a prototype for a cargo bounty, a set of items
/// that must be sold together in a labeled container in order
/// to receive a monetary reward.
/// </summary>
[Prototype]
public sealed partial class FactionLevelPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// A description for flava purposes.
    /// </summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    /// Cost to purchase this faction level.
    /// </summary>
    [DataField]
    public int Cost = 0;

    /// <summary>
    /// How many tiles this faction is allowed to claim.
    /// </summary>
    [DataField]
    public int TileLimit = 0;

    /// <summary>
    /// Whether the faction can claim a tradestation
    /// </summary>
    [DataField]
    public bool TradestationClaim = false;

    /// <summary>
    /// What StationLevel is the next available to purchase
    /// </summary>
    [DataField]
    public ProtoId<FactionLevelPrototype> Next = string.Empty;

}
