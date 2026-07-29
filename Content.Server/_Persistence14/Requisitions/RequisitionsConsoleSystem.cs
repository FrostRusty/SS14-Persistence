using System.Linq;
using Content.Server.Construction;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Persistence14.Requisitions;
using Content.Shared.Construction.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Requisitions;

/// <inheritdoc/>
public sealed class RequisitionsConsoleSystem : SharedRequisitionsConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlatpackSystem _flatpack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<LatheComponent> _latheQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionCheckoutMessage>(OnCheckout);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionCancelMessage>(OnCancel);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<RequisitionsConsoleComponent, MaterialAmountChangedEvent>(OnMaterialChanged);
        SubscribeLocalEvent<RequisitionsLatheJobComponent, LatheItemProducedEvent>(OnLatheItemProduced);
        // Run after the lathe's own power handler so its abort/refund has already returned materials to it.
        SubscribeLocalEvent<RequisitionsLatheJobComponent, PowerChangedEvent>(OnJobLathePowerChanged, after: new[] { typeof(LatheSystem) });

        _latheQuery = GetEntityQuery<LatheComponent>();
    }

    #region Cash insertion

    private void OnInteractUsing(Entity<RequisitionsConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only intercept cash; sheets fall through to MaterialStorage's own handler (customer contributions).
        if (!TryComp<StackComponent>(args.Used, out var stack) || stack.StackTypeId != ent.Comp.CashStack)
            return;

        ent.Comp.PendingBalance += stack.Count;
        QueueDel(args.Used);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("requisitions-cash-inserted", ("spesos", stack.Count)), ent.Owner, args.User);
        UpdateUi(ent, args.User);
    }

    /// <summary>Materials inserted/removed (e.g. a customer contributing sheets) — refresh the open UI live.</summary>
    private void OnMaterialChanged(Entity<RequisitionsConsoleComponent> ent, ref MaterialAmountChangedEvent args)
    {
        UpdateUi(ent);
    }

    #endregion

    #region Checkout

    private void OnCheckout(Entity<RequisitionsConsoleComponent> ent, ref RequisitionCheckoutMessage args)
    {
        RefreshLinkState(ent);

        if (args.Items.Count == 0)
            return;

        // The customer's inserted sheets (raw material units), used to discount and physically feed the print.
        var pool = _materialStorage.GetStoredMaterials(ent.Owner, localOnly: true)
            .ToDictionary(kv => kv.Key.Id, kv => kv.Value);

        var runningCost = 0;
        var producedAny = false;
        var anyFailed = false;

        Log.Debug($"[Requisitions] checkout on {ToPrettyString(ent)} by {ToPrettyString(args.Actor)}: {args.Items.Count} items, pending ${ent.Comp.PendingBalance}, {ent.Comp.LinkedMachines.Count} linked machines");

        foreach (var item in args.Items)
        {
            // A single bad line must never abort the whole order — isolate and log each one.
            try
            {
                if (!Proto.TryIndex<LatheRecipePrototype>(item.RecipeId, out var recipe))
                {
                    Log.Warning($"[Requisitions] unknown recipe '{item.RecipeId}', skipping");
                    continue;
                }

                var qty = Math.Max(1, item.Quantity);
                var flatpack = item.Flatpack && ent.Comp.FlatpackerLinked && IsFlatpackable(recipe);
                var mult = flatpack ? ent.Comp.FlatpackMaterialMultiplier : 1f;

                // Raw material need per material, how much the customer's contribution covers, and the cash cost.
                var cover = new Dictionary<string, int>();
                var itemCost = 0;
                foreach (var (mat, baseAmount) in recipe.Materials)
                {
                    var raw = (int) MathF.Ceiling(baseAmount * mult) * qty;
                    var covered = Math.Min(pool.GetValueOrDefault(mat.Id), raw);
                    cover[mat.Id] = covered;
                    itemCost += SheetCost(ent.Comp, mat.Id, raw - covered);
                }

                foreach (var fee in FeesFor(ent.Comp, item.RecipeId, flatpack))
                    itemCost += fee.Price * qty;

                // Never charge more than the customer inserted.
                if (runningCost + itemCost > ent.Comp.PendingBalance)
                {
                    Log.Debug($"[Requisitions] '{item.RecipeId}' x{qty} costs ${itemCost} but only ${ent.Comp.PendingBalance - runningCost} left of inserted money — skipping");
                    anyFailed = true;
                    continue;
                }

                var printed = flatpack
                    ? TryDispatchFlatpack(ent, recipe, qty, cover, itemCost)
                    : TryDispatchLathe(ent, recipe, qty, cover, itemCost);

                Log.Debug($"[Requisitions] '{item.RecipeId}' x{qty} flatpack={flatpack} cost=${itemCost} -> printed={printed}");

                if (!printed)
                {
                    anyFailed = true;
                    continue;
                }

                foreach (var (mat, c) in cover)
                    pool[mat] = pool.GetValueOrDefault(mat) - c;

                runningCost += itemCost;
                producedAny = true;
            }
            catch (Exception e)
            {
                Log.Error($"[Requisitions] checkout of '{item.RecipeId}' threw, continuing with the rest: {e}");
                anyFailed = true;
            }
        }

        Log.Debug($"[Requisitions] checkout done: producedAny={producedAny}, charged=${runningCost}, anyFailed={anyFailed}");

        if (!producedAny)
        {
            _popup.PopupEntity(Loc.GetString("requisitions-checkout-failed"), ent.Owner, args.Actor);
            return;
        }

        // Escrow payment for the queued prints; it's released to the operator only as each item finishes, and
        // refunded to the customer if a print is lost to power failure. The remaining money is the change.
        ent.Comp.PendingBalance -= runningCost;
        ent.Comp.EscrowBalance += runningCost;

        var change = ent.Comp.PendingBalance;
        ent.Comp.PendingBalance = 0;
        if (change > 0)
            SpawnCash(ent, change, args.Actor);

        // Hand back any inserted sheets the order didn't use.
        foreach (var leftover in _materialStorage.EjectAllMaterial(ent.Owner))
            _hands.PickupOrDrop(args.Actor, leftover);

        _popup.PopupEntity(
            Loc.GetString(anyFailed ? "requisitions-checkout-partial" : "requisitions-checkout-done", ("spesos", runningCost)),
            ent.Owner, args.Actor);

        UpdateUi(ent, args.Actor);
    }

    /// <summary>
    /// Tries every linked lathe that can print the recipe until one accepts the job, so an order spread across
    /// machines queues on all of them and a machine that's out of materials falls through to another that isn't.
    /// </summary>
    private bool TryDispatchLathe(Entity<RequisitionsConsoleComponent> ent, LatheRecipePrototype recipe, int qty, Dictionary<string, int> cover, int cost, EntityUid? flatpacker = null)
    {
        var candidates = 0;
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!_latheQuery.TryComp(machine, out var lathe))
                continue;

            if (!_lathe.GetAvailableRecipes(machine, lathe).ContainsKey(recipe.ID))
                continue;

            candidates++;
            MoveCover(ent.Owner, machine, cover, into: true);

            if (_lathe.TryAddToQueue(machine, recipe, qty))
            {
                // Start staggered rather than immediately: starting many machines on the same tick spikes their
                // combined power draw and browns out the APC. See ScheduleStart / Update.
                ScheduleStart(machine);

                // Record a job per printed item so the escrowed payment is released when it finishes (and, for
                // flatpack orders, so the finished board is transferred to a flatpacker).
                var jobs = EnsureComp<RequisitionsLatheJobComponent>(machine);
                for (var i = 0; i < qty; i++)
                {
                    jobs.Jobs.Add(new RequisitionJob
                    {
                        Recipe = recipe.ID,
                        Console = ent.Owner,
                        Cost = cost / qty + (i == 0 ? cost % qty : 0),
                        // The contribution was moved in for the whole batch; attach it to the first job.
                        Cover = i == 0 && cover.Count > 0 ? new Dictionary<string, int>(cover) : null,
                        Flatpacker = flatpacker,
                    });
                }

                Log.Debug($"[Requisitions] queued '{recipe.ID}' x{qty} on {ToPrettyString(machine)}");
                return true;
            }

            Log.Debug($"[Requisitions] {ToPrettyString(machine)} has recipe '{recipe.ID}' but couldn't queue it (not enough materials); trying next");
            MoveCover(ent.Owner, machine, cover, into: false); // revert and try the next machine
        }

        Log.Debug($"[Requisitions] no linked lathe could produce '{recipe.ID}' ({candidates} had the recipe)");
        return false;
    }

    /// <summary>
    /// Prints the board on a lathe like any other item; a transfer job (recorded on the lathe) moves each
    /// finished board into a flatpacker once it's done printing. See <see cref="OnLatheItemProduced"/>.
    /// </summary>
    private bool TryDispatchFlatpack(Entity<RequisitionsConsoleComponent> ent, LatheRecipePrototype recipe, int qty, Dictionary<string, int> cover, int cost)
    {
        if (recipe.Result is null)
            return false;

        EntityUid? flatpacker = null;
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (HasComp<FlatpackCreatorComponent>(machine))
            {
                flatpacker = machine;
                break;
            }
        }

        if (flatpacker == null)
            return false;

        return TryDispatchLathe(ent, recipe, qty, cover, cost, flatpacker);
    }

    /// <summary>Moves the covered contribution between the console and a target machine (into it, or back out).</summary>
    private void MoveCover(EntityUid console, EntityUid machine, Dictionary<string, int> cover, bool into)
    {
        foreach (var (mat, c) in cover)
        {
            if (c <= 0)
                continue;

            var sign = into ? 1 : -1;
            _materialStorage.TryChangeMaterialAmount(console, mat, sign * -c, localOnly: true);
            _materialStorage.TryChangeMaterialAmount(machine, mat, sign * c);
        }
    }

    #endregion

    #region Flatpack transfer

    /// <summary>
    /// A requisition item finished printing: release its escrowed payment to the console. If it was bound for a
    /// flatpacker, remove the printed board and hand a fresh one to that flatpacker's queue; otherwise the item
    /// is simply delivered at the lathe.
    /// </summary>
    private void OnLatheItemProduced(Entity<RequisitionsLatheJobComponent> ent, ref LatheItemProducedEvent args)
    {
        var recipeId = args.Recipe.ID;
        var result = args.Result;
        var boardProto = args.Recipe.Result;

        var idx = ent.Comp.Jobs.FindIndex(j => j.Recipe == recipeId);
        if (idx < 0)
            return;

        var job = ent.Comp.Jobs[idx];
        ent.Comp.Jobs.RemoveAt(idx);
        if (ent.Comp.Jobs.Count == 0)
            RemCompDeferred<RequisitionsLatheJobComponent>(ent);

        // Payment is earned only now that the item exists: move it from escrow to the operator-withdrawable pot.
        if (TryComp<RequisitionsConsoleComponent>(job.Console, out var console))
        {
            var release = Math.Min(job.Cost, console.EscrowBalance);
            console.EscrowBalance -= release;
            console.StoredBalance += release;
            UpdateUi((job.Console, console));
        }

        // Non-flatpack items are delivered as-is at the lathe.
        if (job.Flatpacker is not { } flatpacker || TerminatingOrDeleted(flatpacker))
            return;

        // Flatpack items: remove the printed board and queue a fresh one for the flatpacker.
        QueueDel(result);
        if (boardProto is not { } board)
            return;

        var queue = EnsureComp<RequisitionsFlatpackQueueComponent>(flatpacker);
        queue.Pending.Add(board);
        Log.Debug($"[Requisitions] board {board} produced on {ToPrettyString(ent)} -> queued for flatpacker {ToPrettyString(flatpacker)} (pending {queue.Pending.Count})");
        TryFeedFlatpacker((flatpacker, queue));
    }

    /// <summary>
    /// The lathe lost power and aborts its in-progress print, which our per-item dispatch does not resume. Refund
    /// the escrowed payment for every outstanding job back to the customer as cash, so a brownout never charges
    /// for undelivered items.
    /// </summary>
    private void OnJobLathePowerChanged(Entity<RequisitionsLatheJobComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        foreach (var job in ent.Comp.Jobs)
        {
            if (!TryComp<RequisitionsConsoleComponent>(job.Console, out var console))
                continue;

            var coords = Transform(job.Console).Coordinates;

            // Refund the escrowed money as cash at the console.
            if (job.Cost > 0)
            {
                var refund = Math.Min(job.Cost, console.EscrowBalance);
                console.EscrowBalance -= refund;
                if (refund > 0)
                    _stack.SpawnMultipleAtPosition(console.CashStack, refund, coords);
            }

            // Refund contributed materials: the aborted lathe was handed them back, so move them from the lathe
            // out to the customer at the console (department stays whole, customer made whole).
            if (job.Cover is { } cover)
            {
                foreach (var (mat, amount) in cover)
                {
                    var take = Math.Min(amount, _materialStorage.GetMaterialAmount(ent.Owner, mat));
                    if (take <= 0)
                        continue;

                    _materialStorage.SpawnMultipleFromMaterial(take, mat, coords, out var overflow);
                    _materialStorage.TryChangeMaterialAmount(ent.Owner, mat, -(take - overflow));
                }
            }

            UpdateUi((job.Console, console));
        }

        ent.Comp.Jobs.Clear();
        RemCompDeferred<RequisitionsLatheJobComponent>(ent);
    }

    /// <summary>Feeds the next queued board into an idle flatpacker and starts it packing.</summary>
    private void TryFeedFlatpacker(Entity<RequisitionsFlatpackQueueComponent> flatpacker)
    {
        var queue = flatpacker.Comp;
        if (queue.Pending.Count == 0)
            return;

        if (!TryComp<FlatpackCreatorComponent>(flatpacker, out var creator) || !_flatpack.IsIdle((flatpacker, creator)))
            return;

        var proto = queue.Pending[0];
        var board = Spawn(proto, Transform(flatpacker).Coordinates);

        if (_flatpack.TryPackBoard((flatpacker, creator), board))
        {
            queue.Pending.RemoveAt(0);
            if (queue.Pending.Count == 0)
                RemCompDeferred<RequisitionsFlatpackQueueComponent>(flatpacker);
        }
        else
        {
            // Couldn't start (e.g. the flatpacker has no materials yet). Clean up and back off so we don't
            // churn spawning/deleting a board every tick — retry in a couple of seconds.
            Del(board);
            queue.NextTry = _timing.CurTime + TimeSpan.FromSeconds(2);
            Log.Debug($"[Requisitions] flatpacker {ToPrettyString(flatpacker)} not ready to pack {proto}; backing off");
        }
    }

    private static readonly TimeSpan StartStagger = TimeSpan.FromSeconds(0.2);
    private readonly Queue<EntityUid> _pendingStarts = new();
    private readonly HashSet<EntityUid> _pendingStartSet = new();
    private TimeSpan _nextStart;

    /// <summary>Queues a lathe to be kicked into production, spaced out from other starts (see <see cref="Update"/>).</summary>
    private void ScheduleStart(EntityUid lathe)
    {
        if (_pendingStartSet.Add(lathe))
            _pendingStarts.Enqueue(lathe);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Start at most one queued lathe every StartStagger seconds so a big order doesn't flip every machine
        // into its high-power working state on the same tick and brown out the APC.
        if (_pendingStarts.Count > 0 && now >= _nextStart)
        {
            var lathe = _pendingStarts.Dequeue();
            _pendingStartSet.Remove(lathe);
            if (!TerminatingOrDeleted(lathe))
                _lathe.TryStartProducing(lathe);
            _nextStart = now + StartStagger;
        }

        var query = EntityQueryEnumerator<RequisitionsFlatpackQueueComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextTry)
                continue;

            TryFeedFlatpacker((uid, comp));
        }
    }

    /// <summary>Cash cost of a raw material amount, priced per sheet.</summary>
    private int SheetCost(RequisitionsConsoleComponent comp, string materialId, int rawAmount)
    {
        if (rawAmount <= 0 || !Proto.TryIndex<MaterialPrototype>(materialId, out var mat))
            return 0;

        var volume = _materialStorage.GetSheetVolume(mat);
        if (volume <= 0)
            volume = 1;

        return (int) MathF.Round(rawAmount / (float) volume * GetPrice(comp, materialId));
    }

    private void OnCancel(Entity<RequisitionsConsoleComponent> ent, ref RequisitionCancelMessage args)
    {
        // Reclaim the customer's own inserted money and sheets. Never touches the locked StoredBalance.
        if (ent.Comp.PendingBalance > 0)
        {
            SpawnCash(ent, ent.Comp.PendingBalance, args.Actor);
            ent.Comp.PendingBalance = 0;
        }

        _materialStorage.EjectAllMaterial(ent.Owner);
        UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Withdrawal

    private void OnWithdraw(Entity<RequisitionsConsoleComponent> ent, ref RequisitionWithdrawMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("requisitions-access-denied"), ent.Owner, args.Actor);
            return;
        }

        if (ent.Comp.StoredBalance <= 0)
            return;

        SpawnCash(ent, ent.Comp.StoredBalance, args.Actor);
        ent.Comp.StoredBalance = 0;
        UpdateUi(ent, args.Actor);
    }

    private void SpawnCash(Entity<RequisitionsConsoleComponent> console, int amount, EntityUid toHands)
    {
        if (amount <= 0)
            return;

        var coords = Transform(console).Coordinates;
        var stacks = _stack.SpawnMultipleAtPosition(console.Comp.CashStack, amount, coords);
        foreach (var stack in stacks)
            _hands.PickupOrDrop(toHands, stack);
    }

    #endregion

    #region UI state

    protected override void UpdateUi(Entity<RequisitionsConsoleComponent> ent, EntityUid? actor = null)
    {
        if (!_ui.IsUiOpen(ent.Owner, RequisitionsConsoleUiKey.Key))
            return;

        var state = new RequisitionsConsoleState
        {
            Catalogue = BuildCatalogue(ent),
            Stock = BuildStock(ent),
            Contributed = _materialStorage.GetStoredMaterials(ent.Owner, localOnly: true).ToDictionary(kv => kv.Key.Id, kv => kv.Value),
            MaterialPrices = ent.Comp.MaterialPrices.ToDictionary(kv => kv.Key.Id, kv => kv.Value),
            MaterialNames = BuildMaterialNames(ent),
            Fees = ent.Comp.Fees,
            PendingBalance = ent.Comp.PendingBalance,
            StoredBalance = ent.Comp.StoredBalance,
            FlatpackerLinked = ent.Comp.FlatpackerLinked,
            FlatpackMultiplier = ent.Comp.FlatpackMaterialMultiplier,
            // A shared BUI state can't be tailored per-viewer, so the config tab shows if any current viewer is
            // authorised. Every config action is still re-checked server-side regardless.
            HasConfigAccess = _ui.GetActors(ent.Owner, RequisitionsConsoleUiKey.Key).Any(a => HasConfigAccess(ent, a)),
            Currency = "spesos",
        };

        if (state.HasConfigAccess)
            state.Linkable = BuildLinkable(ent);

        _ui.SetUiState(ent.Owner, RequisitionsConsoleUiKey.Key, state);
    }

    private List<RequisitionCatalogueEntry> BuildCatalogue(Entity<RequisitionsConsoleComponent> ent)
    {
        // Squash by an identity of "same result + same materials" so genuinely identical items (e.g. four
        // aprons offered by different recipes/machines) collapse to one line, while variants that cost
        // differently stay separate.
        var merged = new Dictionary<string, RequisitionCatalogueEntry>();

        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!_latheQuery.TryComp(machine, out var lathe))
                continue;

            foreach (var recipeId in _lathe.GetAvailableRecipes(machine, lathe).Keys)
            {
                if (!Proto.TryIndex(recipeId, out var recipe))
                    continue;

                var materials = recipe.Materials.OrderBy(kv => kv.Key.Id).Select(kv => $"{kv.Key.Id}:{kv.Value}");
                var signature = $"{recipe.Result?.Id}|{string.Join(",", materials)}";

                if (!merged.TryGetValue(signature, out var entry))
                {
                    entry = new RequisitionCatalogueEntry
                    {
                        RecipeId = recipeId,
                        Name = Lathe.GetRecipeName(recipe),
                        Result = recipe.Result?.Id,
                        Materials = recipe.Materials.ToDictionary(kv => kv.Key.Id, kv => kv.Value),
                        Flatpackable = ent.Comp.FlatpackerLinked && IsFlatpackable(recipe),
                    };
                    merged[signature] = entry;
                }

                entry.SourceCount++;
            }
        }

        return merged.Values.OrderBy(e => e.Name).ToList();
    }

    private bool IsFlatpackable(LatheRecipePrototype recipe)
    {
        // The flatpacker packs machine/computer boards; a recipe is flatpackable if its result is one.
        if (recipe.Result is not { } result || !Proto.TryIndex<EntityPrototype>(result, out var proto))
            return false;

        return proto.Components.ContainsKey("MachineBoard") || proto.Components.ContainsKey("ComputerBoard");
    }

    private Dictionary<string, int> BuildStock(Entity<RequisitionsConsoleComponent> ent)
    {
        // Only report the stock of linked ore silos. Summing individual lathes is misleading (one loaded lathe
        // among ten would read as department-wide stock), so with no silo linked the stock panel is simply empty.
        var stock = new Dictionary<string, int>();
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!HasComp<OreSiloComponent>(machine))
                continue;

            foreach (var (mat, amount) in _materialStorage.GetStoredMaterials(machine, localOnly: true))
                stock[mat.Id] = stock.GetValueOrDefault(mat.Id) + amount;
        }

        return stock;
    }

    private Dictionary<string, string> BuildMaterialNames(Entity<RequisitionsConsoleComponent> ent)
    {
        var names = new Dictionary<string, string>();
        foreach (var mat in ent.Comp.MaterialPrices.Keys)
        {
            if (Proto.TryIndex(mat, out var proto))
                names[mat.Id] = Loc.GetString(proto.Name);
        }

        return names;
    }

    private List<RequisitionLinkEntry> BuildLinkable(Entity<RequisitionsConsoleComponent> ent)
    {
        var result = new List<RequisitionLinkEntry>();
        var seen = new HashSet<EntityUid>();

        var coords = Transform(ent).Coordinates;

        void Add(EntityUid machine)
        {
            if (!seen.Add(machine))
                return;

            result.Add(new RequisitionLinkEntry
            {
                Machine = GetNetEntity(machine),
                Label = MetaData(machine).EntityName,
                Linked = ent.Comp.LinkedMachines.Contains(machine),
                InRange = CanLink(ent.Owner, machine),
                Flatpacker = HasComp<FlatpackCreatorComponent>(machine),
            });
        }

        var nearbyLathes = new HashSet<Entity<LatheComponent>>();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, nearbyLathes);
        foreach (var machine in nearbyLathes)
            Add(machine);

        var nearbyFlatpackers = new HashSet<Entity<FlatpackCreatorComponent>>();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, nearbyFlatpackers);
        foreach (var machine in nearbyFlatpackers)
            Add(machine);

        var nearbySilos = new HashSet<Entity<OreSiloComponent>>();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, nearbySilos);
        foreach (var machine in nearbySilos)
            Add(machine);

        // Include already-linked machines even if they've drifted out of range.
        foreach (var machine in ent.Comp.LinkedMachines)
            Add(machine);

        return result;
    }

    #endregion

    private IEnumerable<RequisitionFee> FeesFor(RequisitionsConsoleComponent comp, string recipeId, bool flatpack)
    {
        foreach (var fee in comp.Fees)
        {
            switch (fee.Scope)
            {
                case RequisitionFeeScope.Flatpack when flatpack:
                case RequisitionFeeScope.All:
                    yield return fee;
                    break;
                case RequisitionFeeScope.Specific when fee.Recipes.Contains(recipeId):
                    yield return fee;
                    break;
            }
        }
    }

    private int GetPrice(RequisitionsConsoleComponent comp, string material)
    {
        return comp.MaterialPrices.TryGetValue(material, out var price) ? price : comp.FallbackMaterialPrice;
    }
}
