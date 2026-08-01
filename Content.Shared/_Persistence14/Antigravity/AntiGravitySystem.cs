using Content.Shared.Gravity;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Persistence14.Antigravity;

/// <summary>
/// A version of <see cref="Content.Shared.Clothing.EntitySystems.AntiGravityClothingSystem"/> t
/// hat works on the root entity instead of clothing.
/// 
/// Does not depend on standing status.
/// </summary>
public sealed partial class AntiGravitySystem : EntitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AntiGravityComponent, StatusEffectAppliedEvent>(OnApplyStatusEffect);
        SubscribeLocalEvent<AntiGravityComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<AntiGravityComponent, StatusEffectRemovedEvent>(OnRemoveStatusEffect);
        SubscribeLocalEvent<AntiGravityComponent, ComponentRemove>(OnComponentRemoved);
        SubscribeLocalEvent<AntiGravityComponent, IsWeightlessEvent>(OnWeightless);
    }

    public void OnApplyStatusEffect(Entity<AntiGravityComponent> entity, ref StatusEffectAppliedEvent args)
    {
        _gravity.RefreshWeightless(entity.Owner, true);
    }

    public void OnComponentStartup(Entity<AntiGravityComponent> entity, ref ComponentStartup args)
    {
        _gravity.RefreshWeightless(entity.Owner, true);
    }

    public void OnRemoveStatusEffect(Entity<AntiGravityComponent> entity, ref StatusEffectRemovedEvent args)
    {
        _gravity.RefreshWeightless(entity.Owner, false);
    }

    public void OnComponentRemoved(Entity<AntiGravityComponent> entity, ref ComponentRemove args)
    {
        _gravity.RefreshWeightless(entity.Owner, false);
    }

    private void OnWeightless(Entity<AntiGravityComponent> entity, ref IsWeightlessEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.IsWeightless = true; // Always be weightless!
    }
}