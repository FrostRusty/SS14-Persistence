using Content.Shared.Gravity;
using Content.Shared.Standing;

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
        SubscribeLocalEvent<AntiGravityComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<AntiGravityComponent, ComponentRemove>(OnComponentRemoved);
        SubscribeLocalEvent<AntiGravityComponent, IsWeightlessEvent>(OnWeightless);
    }

    // Add weightlessness
    public void OnComponentStartup(Entity<AntiGravityComponent> entity, ref ComponentStartup args)
    {
        _gravity.RefreshWeightless(entity.Owner, true);
    }

    // Remove weightlessness.
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