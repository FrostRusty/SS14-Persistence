using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Requisitions;

/// <summary>
/// A customer-facing ordering console. It links to nearby item-printing machines (lathes) the same way an
/// ore silo links to its clients, gathers their combined recipe list, and lets a customer build a cart and
/// pay for a batch print in one go. A second, access-gated tab lets an authorised operator set the price of
/// each raw material and define extra fees (e.g. a research fee).
/// </summary>
[RegisterComponent]
[Access(typeof(SharedRequisitionsConsoleSystem))]
public sealed partial class RequisitionsConsoleComponent : Component
{
    #region Linking

    /// <summary>
    /// The item-printing machines (things with <see cref="LatheComponent"/>) this console dispatches prints to.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> LinkedMachines = new();

    /// <summary>
    /// The maximum distance a machine can be from the console and still be linkable. Mirrors the ore silo.
    /// </summary>
    [DataField]
    public float Range = 20f;

    #endregion

    #region Pricing configuration (server-authoritative, pushed to clients via BUI state)

    /// <summary>
    /// Operator-set price, in <see cref="Currency"/>, charged per unit of a given raw material.
    /// Only materials that actually appear in the linked machines' recipes are ever shown/priced.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> MaterialPrices = new();

    /// <summary>
    /// Extra named charges (research fee, handling fee, the automatic flatpack fee, …).
    /// </summary>
    [DataField]
    public List<RequisitionFee> Fees = new();

    /// <summary>
    /// Default per-material prices seeded from YAML. When a material first becomes priceable (because a newly
    /// linked machine uses it) and has no operator price yet, it is seeded from here.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> DefaultMaterialPrices = new();

    /// <summary>
    /// Fallback default price for any priceable material not present in <see cref="DefaultMaterialPrices"/>.
    /// </summary>
    [DataField]
    public int FallbackMaterialPrice;

    #endregion

    #region Money

    /// <summary>
    /// The cash stack this console accepts and dispenses. Spesos are physical <c>SpaceCash</c>, a stack whose
    /// count is its value, so inserted cash is valued by its stack count and refunds spawn this stack.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype> CashStack = "Credit";

    /// <summary>
    /// Money the customer has inserted but not yet spent. Refunded (spat out) at checkout after the cost is
    /// taken, or returned if they walk away. Not access-gated — it's the customer's own money.
    /// </summary>
    [DataField]
    public int PendingBalance;

    /// <summary>
    /// Money paid for prints that are still in progress. Released to <see cref="StoredBalance"/> as each item
    /// actually finishes printing, or refunded to the customer if the print is lost to a power failure. This is
    /// what stops a customer from paying for items a brownout cancelled.
    /// </summary>
    [DataField]
    public int EscrowBalance;

    /// <summary>
    /// Money earned by prints that actually completed, locked in the machine until an authorised operator
    /// withdraws it.
    /// </summary>
    [DataField]
    public int StoredBalance;

    #endregion

    #region Flatpack

    /// <summary>
    /// Set true when at least one linked machine is a flatpack creator. Enables the flatpack column and fee.
    /// </summary>
    [DataField]
    public bool FlatpackerLinked;

    /// <summary>
    /// The id of the automatic flatpack fee entry in <see cref="Fees"/>.
    /// </summary>
    [DataField]
    public string FlatpackFeeId = "Flatpack";

    /// <summary>
    /// Multiplier applied to a recipe's material cost when it is flatpacked. Flatpacking is more expensive.
    /// </summary>
    [DataField]
    public float FlatpackMaterialMultiplier = 1.5f;

    #endregion
}

/// <summary>
/// An extra charge the operator can attach to some or all catalogue items.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class RequisitionFee
{
    /// <summary>Stable identifier for this fee (used by config messages and the flatpack fee).</summary>
    [DataField(required: true)]
    public string Id = default!;

    /// <summary>Player-facing name, e.g. "Research Fee".</summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>Flat charge in the console's currency.</summary>
    [DataField]
    public int Price;

    /// <summary>Which catalogue items this fee applies to.</summary>
    [DataField]
    public RequisitionFeeScope Scope = RequisitionFeeScope.Specific;

    /// <summary>When <see cref="Scope"/> is <see cref="RequisitionFeeScope.Specific"/>, the recipes it applies to.</summary>
    [DataField]
    public HashSet<ProtoId<LatheRecipePrototype>> Recipes = new();
}

[Serializable, NetSerializable]
public enum RequisitionFeeScope : byte
{
    /// <summary>Only the recipes listed in <see cref="RequisitionFee.Recipes"/>.</summary>
    Specific,

    /// <summary>Every catalogue item.</summary>
    All,

    /// <summary>Only items that are being flatpacked. Reserved for the automatic flatpack fee.</summary>
    Flatpack,
}
