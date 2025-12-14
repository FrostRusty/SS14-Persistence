using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using System.Numerics;
using Robust.Shared.EntitySerialization;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class PersistenceLoadGridCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEntitySystemManager _system = default!;

    public override string Command => "persistenceloadgrid";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length == 3 || args.Length > 6)
        {
            shell.WriteError("Must have either 2, 4, 5, or 6 arguments.");
            return;
        }

        if (!int.TryParse(args[0], out var intMapId))
        {
            shell.WriteError($"{args[0]} is not a valid integer.");
            return;
        }

        var mapId = new MapId(intMapId);

        // no loading into null space
        if (mapId == MapId.Nullspace)
        {
            shell.WriteError("Cannot load into nullspace.");
            return;
        }

        var sys = _system.GetEntitySystem<SharedMapSystem>();
        if (!sys.MapExists(mapId))
        {
            shell.WriteError($"MapID {intMapId} did not exist, creating without map init");
            sys.CreateMap(mapId, false); // doesnt runmapinit to be conservative.
        }

        Vector2 offset = default;
        if (args.Length >= 4)
        {
            if (!float.TryParse(args[2], out var x))
            {
                shell.WriteError($"{args[2]} is not a valid float.");
                return;
            }

            if (!float.TryParse(args[3], out var y))
            {
                shell.WriteError($"{args[3]} is not a valid float.");
                return;
            }

            offset = new Vector2(x, y);
        }

        Angle rot = default;
        if (args.Length >= 5)
        {
            if (!float.TryParse(args[4], out var rotation))
            {
                shell.WriteError($"{args[4]} is not a valid float.");
                return;
            }

            rot = Angle.FromDegrees(rotation);
        }

        var opts = DeserializationOptions.Default;
        if (args.Length >= 6)
        {
            if (!bool.TryParse(args[5], out var storeUids))
            {
                shell.WriteError($"{args[5]} is not a valid boolean.");
                return;
            }

            opts.StoreYamlUids = storeUids;
        }

        var path = new ResPath(args[1]);
        if (!_system.GetEntitySystem<MapLoaderSystem>().TryLoadGrid(mapId, path, out var grid, opts, offset, rot))
        {
            shell.WriteError($"Could not load the grid! Check console perhaps...");
            return;
        }
        _transform.SetLocalPositionRotation(grid.Value, offset, rot);
    }
}
