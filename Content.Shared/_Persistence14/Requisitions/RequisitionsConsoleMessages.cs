using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Requisitions;

[Serializable, NetSerializable]
public enum RequisitionsConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// Everything the console UI needs, computed server-side and pushed to the client. The cart itself lives
/// entirely on the client; only the final <see cref="RequisitionCheckoutMessage"/> is sent back.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequisitionsConsoleState : BoundUserInterfaceState
{
    /// <summary>The joint, de-duplicated recipe list from every linked machine.</summary>
    public List<RequisitionCatalogueEntry> Catalogue = new();

    /// <summary>Material id -> total amount available across the linked machines (the "department stock").</summary>
    public Dictionary<string, int> Stock = new();

    /// <summary>Material id -> amount the customer has inserted into this console to lower the bill (raw units).</summary>
    public Dictionary<string, int> Contributed = new();

    /// <summary>Material id -> localized display name, for every priceable material.</summary>
    public Dictionary<string, string> MaterialNames = new();

    /// <summary>Material id -> operator-set price. Only materials used by the linked catalogue appear here.</summary>
    public Dictionary<string, int> MaterialPrices = new();

    /// <summary>Operator-defined fees, including the automatic flatpack fee when a flatpacker is linked.</summary>
    public List<RequisitionFee> Fees = new();

    /// <summary>Machines the operator can link/unlink (config tab). Empty for customers without config access.</summary>
    public List<RequisitionLinkEntry> Linkable = new();

    /// <summary>Spesos the customer has inserted but not yet spent (refunded at checkout).</summary>
    public int PendingBalance;

    /// <summary>Spesos currently locked in the machine, withdrawable only by an operator.</summary>
    public int StoredBalance;

    public bool FlatpackerLinked;

    /// <summary>Material-cost multiplier applied to flatpacked items, for client-side cost preview.</summary>
    public float FlatpackMultiplier = 1.5f;

    /// <summary>Whether the viewing player passes the access check for the config tab.</summary>
    public bool HasConfigAccess;

    /// <summary>Currency id, for display.</summary>
    public string Currency = "Credit";
}

/// <summary>One catalogue line: a single recipe, merged across every machine that can print it.</summary>
[Serializable, NetSerializable]
public sealed class RequisitionCatalogueEntry
{
    public string RecipeId = string.Empty;
    public string Name = string.Empty;

    /// <summary>Result entity prototype id, used to draw the icon. Null for reagent-only recipes.</summary>
    public string? Result;

    /// <summary>Raw material -> amount required (before any flatpack multiplier).</summary>
    public Dictionary<string, int> Materials = new();

    /// <summary>True if at least one linked flatpacker can flatpack this item.</summary>
    public bool Flatpackable;

    /// <summary>How many linked machines can print this (for display; duplicates are squashed to one line).</summary>
    public int SourceCount;
}

/// <summary>A machine that can be linked to the console (shown in the config tab).</summary>
[Serializable, NetSerializable]
public sealed class RequisitionLinkEntry
{
    public NetEntity Machine;
    public string Label = string.Empty;
    public bool Linked;
    public bool InRange;
    public bool Flatpacker;
}

/// <summary>A single line the customer is buying.</summary>
[Serializable, NetSerializable]
public struct RequisitionCartItem
{
    public string RecipeId;
    public int Quantity;
    public bool Flatpack;
}

// ---------------------------------------------------------------------------
// Customer messages
// ---------------------------------------------------------------------------

/// <summary>
/// Sent when the customer confirms their cart. Any raw materials the customer physically inserted into the
/// console beforehand are applied automatically to lower the bill.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequisitionCheckoutMessage : BoundUserInterfaceMessage
{
    public List<RequisitionCartItem> Items;

    public RequisitionCheckoutMessage(List<RequisitionCartItem> items)
    {
        Items = items;
    }
}

/// <summary>
/// The customer reclaims everything they've inserted but not yet committed: pending spesos are spat back as
/// cash and contributed sheets are ejected. Not access-gated — it's the customer's own property.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequisitionCancelMessage : BoundUserInterfaceMessage
{
}

// ---------------------------------------------------------------------------
// Operator (access-gated) messages — the server re-checks access on every one.
// ---------------------------------------------------------------------------

/// <summary>Link or unlink a nearby printing machine.</summary>
[Serializable, NetSerializable]
public sealed class ToggleRequisitionLinkMessage : BoundUserInterfaceMessage
{
    public NetEntity Machine;

    public ToggleRequisitionLinkMessage(NetEntity machine)
    {
        Machine = machine;
    }
}

/// <summary>Set (or clear, when price &lt; 0) the price of a raw material.</summary>
[Serializable, NetSerializable]
public sealed class RequisitionSetMaterialPriceMessage : BoundUserInterfaceMessage
{
    public string Material;
    public int Price;

    public RequisitionSetMaterialPriceMessage(string material, int price)
    {
        Material = material;
        Price = price;
    }
}

/// <summary>Add a new fee or edit an existing one (matched by <see cref="RequisitionFee.Id"/>).</summary>
[Serializable, NetSerializable]
public sealed class RequisitionSetFeeMessage : BoundUserInterfaceMessage
{
    public RequisitionFee Fee;

    public RequisitionSetFeeMessage(RequisitionFee fee)
    {
        Fee = fee;
    }
}

/// <summary>Remove a fee by id. The automatic flatpack fee cannot be removed.</summary>
[Serializable, NetSerializable]
public sealed class RequisitionRemoveFeeMessage : BoundUserInterfaceMessage
{
    public string Id;

    public RequisitionRemoveFeeMessage(string id)
    {
        Id = id;
    }
}

/// <summary>Withdraw the stored balance as physical currency.</summary>
[Serializable, NetSerializable]
public sealed class RequisitionWithdrawMessage : BoundUserInterfaceMessage
{
}
