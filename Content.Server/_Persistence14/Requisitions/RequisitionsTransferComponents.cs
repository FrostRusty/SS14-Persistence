namespace Content.Server._Persistence14.Requisitions;

/// <summary>
/// Runtime marker on a lathe: requisition prints it currently owes. Each finished item matching a job releases
/// that job's escrowed payment to the console, and (if bound for a flatpacker) is moved into the console's
/// internal flatpack storage. If the lathe loses power mid-print, the outstanding jobs' payments and contributed
/// materials are refunded to the customer. Not persisted.
/// </summary>
[RegisterComponent]
public sealed partial class RequisitionsLatheJobComponent : Component
{
    public List<RequisitionJob> Jobs = new();
}

/// <summary>One in-progress requisition print on a lathe.</summary>
public struct RequisitionJob
{
    /// <summary>The recipe being printed (matched against finished items).</summary>
    public string Recipe;

    /// <summary>The console that took the order and is holding the escrow.</summary>
    public EntityUid Console;

    /// <summary>Escrowed payment released to the console when this item finishes (or refunded on failure).</summary>
    public int Cost;

    /// <summary>Materials the customer contributed toward this job, refunded to them if the print fails.</summary>
    public Dictionary<string, int>? Cover;

    /// <summary>If set, the finished board is routed into the console's flatpack storage instead of delivered.</summary>
    public EntityUid? Flatpacker;
}
