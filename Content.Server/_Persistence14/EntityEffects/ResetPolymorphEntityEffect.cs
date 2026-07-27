using Content.Server.Body.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Persistence14.EntityEffects;

public sealed partial class ResetPolymorph : EntityEffectBase<ResetPolymorph> { }

public sealed partial class ResetPolymorphEnttiyEffectSystem : EntityEffectSystem<PolymorphedEntityComponent, ResetPolymorph>
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    protected override void Effect(Entity<PolymorphedEntityComponent> entity, ref EntityEffectEvent<ResetPolymorph> args)
    {
        _polymorph.QueueRevert((entity.Owner, entity.Comp));
    }
}