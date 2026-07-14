using Content.Server.Atmos.EntitySystems;
using Content.Shared._Persistence14.Atmos.Geyser;
using Content.Shared.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Atmos.Geyser;

public sealed partial class GasGeyserSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTime = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private const string Sawmill = "gas-geyser";

    public override void Update(float frameTime)
    {
        var geysers = EntityQueryEnumerator<GasGeyserComponent>();

        while (geysers.MoveNext(out var uid, out var geyserComp))
        {
            if (_gameTime.CurTime < geyserComp.NextEruptionTime)
                continue;

            if (!TryGetValidEnvironment((uid, geyserComp), out var environment))
                continue;

            _log.GetSawmill(Sawmill).Info("Geyser errupting");
            Errupt((uid, geyserComp), environment);
        }
    }

    public void Errupt(Entity<GasGeyserComponent> geyser, GasMixture environment)
    {
        var merger = new GasMixture(geyser.Comp.Moles, 1);
        _atmos.Merge(environment, merger);

        geyser.Comp.NextEruptionTime = _gameTime.CurTime + geyser.Comp.EruptionDelay;
        Dirty(geyser);

        RaiseNetworkEvent(new GasGeyserErruptedEvent(GetNetEntity(geyser.Owner)));
    }

    private bool TryGetValidEnvironment(Entity<GasGeyserComponent> geyserEnt, out GasMixture environment)
    {
        var (uid, geyser) = geyserEnt;
        var transform = Transform(uid);
        environment = default!;

        var mixture = _atmos.GetTileMixture((uid, transform), true);

        if (mixture is null)
            return false;

        if (mixture.TotalMoles >= geyser.MaxExternalMoles)
            return false;

        if (mixture.Pressure >= geyser.MaxExternalPressure)
            return false;

        environment = mixture!;
        return true;
    }
}