using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Player;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Chat.Managers;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceSaveGridCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ActorSystem _actor = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override string Command => "persistencesavegrid";



    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Not enough arguments.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }

        var uid = _ent.GetEntity(uidNet);

        // no saving default grid
        if (!_ent.EntityExists(uid))
        {
            shell.WriteError("That grid does not exist.");
            return;
        }

        if (!_ent.TryGetComponent<MapGridComponent>(uid, out var mapGrid))
        {
            shell.WriteError("That grid does not contain a MapGridComponent...");
            return;
        }
        DumpChildren((uid, mapGrid));

        bool saveSuccess = _ent.System<MapLoaderSystem>().TrySaveGrid(uid, new ResPath(args[1]));
        if (saveSuccess)
        {
            shell.WriteLine("Save successful. Look in the user data directory.");
        }
        else
        {
            shell.WriteError("Save unsuccessful!");
        }

        _ent.QueueDeleteEntity(uid);
    }


    // From the Arrivals System
    // This behaviour should probably be made in a helper or system

    private void DumpChildren(Entity<MapGridComponent> gridUid)
    {
        var transformQuery = _ent.GetEntityQuery<TransformComponent>();

        var gridXform = transformQuery.GetComponent(gridUid);
        var fromMatrix = _transform.GetWorldMatrix(gridXform);
        var fromMapUid = gridXform.MapUid;
        var fromRotation = _transform.GetWorldRotation(gridXform);
        if (!fromMapUid.HasValue) return;

        var actorQuery = _ent.GetEntityQuery<ActorComponent>();
        var blacklistQuery = _ent.GetEntityQuery<ArrivalsBlacklistComponent>();

        // Teleport shuttle temporarily

        var gridWorldAABB =
            _transform.GetWorldMatrix(gridXform)
            .TransformBox(gridUid.Comp.LocalAABB)
            .Enlarged(0.2f);


        var toDump = new List<Entity<TransformComponent>>();
        FindDumpChildren(transformQuery, actorQuery, blacklistQuery, gridUid, toDump);
        foreach (var (ent, transform) in toDump)
        {
            var rotation = transform.LocalRotation;

            // From the current child's position, teleport it to somewhere close but outside the grid
            var entityPos = Vector2.Transform(transform.LocalPosition, fromMatrix);
            var direction = entityPos - gridWorldAABB.Center;
            if (direction.EqualsApprox(Vector2.Zero))
                direction = _random.NextAngle().RotateVec(Vector2.One);
            var newEntityPos = gridWorldAABB.ClosestPoint(entityPos + direction.Normalized() * gridWorldAABB.MaxDimension);

            _transform.SetCoordinates(ent, new EntityCoordinates(fromMapUid.Value, newEntityPos));
            _transform.SetWorldRotation(ent, fromRotation + rotation);
            if (_actor.TryGetSession(ent, out var session))
            {
                _chat.DispatchServerMessage(session!, "YEEEEEEET");
            }
        }
    }

    private static void FindDumpChildren(
        EntityQuery<TransformComponent> xformQuery, EntityQuery<ActorComponent> actorQuery, EntityQuery<ArrivalsBlacklistComponent> blacklistQuery,
        EntityUid uid, List<Entity<TransformComponent>> toDump
    )
    {
        var xform = xformQuery.GetComponent(uid);

        if (actorQuery.HasComponent(uid) || blacklistQuery.HasComponent(uid))
        {
            toDump.Add((uid, xform));
            return;
        }

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            FindDumpChildren(xformQuery, actorQuery, blacklistQuery, child, toDump);
        }
    }
}
