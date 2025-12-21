using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.GridControl.Components;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Linq;

namespace Content.Shared.Station;

public abstract partial class SharedStationSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<StationMemberComponent> _stationMemberQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        InitializeTracker();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _stationMemberQuery = GetEntityQuery<StationMemberComponent>();
    }

    /// <summary>
    /// Gets the largest member grid from a station.
    /// </summary>
    public EntityUid? GetLargestGrid(Entity<StationDataComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return null;

        EntityUid? largestGrid = null;
        Box2 largestBounds = new Box2();

        foreach (var gridUid in ent.Comp.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid) ||
                grid.LocalAABB.Size.LengthSquared() < largestBounds.Size.LengthSquared())
                continue;

            largestBounds = grid.LocalAABB;
            largestGrid = gridUid;
        }

        return largestGrid;
    }

    /// <summary>
    /// Returns the total number of tiles contained in the station's grids.
    /// </summary>
    public int GetTileCount(Entity<StationDataComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return 0;

        var count = 0;
        foreach (var gridUid in ent.Comp.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            count += _map.GetAllTiles(gridUid, grid).Count();
        }

        return count;
    }

    [PublicAPI]
    public EntityUid? GetOwningStation(EntityUid? entity, TransformComponent? xform = null)
    {
        if (entity == null)
            return null;

        return GetOwningStation(entity.Value, xform);
    }

    /// <summary>
    /// Gets the station that "owns" the given entity (essentially, the station the grid it's on is attached to)
    /// </summary>
    /// <param name="entity">Entity to find the owner of.</param>
    /// <param name="xform">Resolve pattern, transform of the entity.</param>
    /// <returns>The owning station, if any.</returns>
    /// <remarks>
    /// This does not remember what station an entity started on, it simply checks where it is currently located.
    /// </remarks>
    public EntityUid? GetOwningStation(EntityUid entity, TransformComponent? xform = null, bool gridOwner = false)
    {
        if (!Resolve(entity, ref xform))
            throw new ArgumentException("Tried to use an abstract entity!", nameof(entity));

        if (TryComp<StationTrackerComponent>(entity, out var stationTracker) && !gridOwner)
        {
            // We have a specific station we are tracking and are tethered to.
            return stationTracker.Station;
        }

        if (HasComp<StationDataComponent>(entity))
        {
            // We are the station, just return ourselves.
            return entity;
        }

        if (HasComp<MapGridComponent>(entity))
        {
            // We are the grid, just check ourselves.
            return CompOrNull<StationMemberComponent>(entity)?.Station;
        }

        if (xform.GridUid == EntityUid.Invalid)
        {
            Log.Debug("Unable to get owning station - GridUid invalid.");
            return null;
        }

        return CompOrNull<StationMemberComponent>(xform.GridUid)?.Station;
    }

    public string? GetOwningStationPersonal(EntityUid entity, TransformComponent? xform = null)
    {
        if (!Resolve(entity, ref xform))
            throw new ArgumentException("Tried to use an abstract entity!", nameof(entity));

        if (HasComp<MapGridComponent>(entity))
        {
            // We are the grid, just check ourselves.
            return CompOrNull<PersonalMemberComponent>(entity)?.OwnerName;
        }

        if (xform.GridUid == EntityUid.Invalid)
        {
            Log.Debug("Unable to get owning station - GridUid invalid.");
            return null;
        }

        return CompOrNull<PersonalMemberComponent>(xform.GridUid)?.OwnerName;
    }

    /// <summary>
    /// Return the owning Station and/or person for the specified entity.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="owningStation"></param>
    /// <param name="owningPerson"></param>
    public void GetOwning(EntityUid uid, out EntityUid? owningStation, out string? owningPerson)
    {
        owningStation = GetOwningStation(uid);
        owningPerson = null;
        if (owningStation == null)
        {
            owningPerson = GetOwningStationPersonal(uid);
        }
        else
        {
            if (TryComp<StationDataComponent>(owningStation, out var oSD) && oSD != null)
            {
                owningPerson = oSD.StationName;
            }
        }
    }
    public List<EntityUid> GetStations()
    {
        var stations = new List<EntityUid>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            stations.Add(uid);
        }

        return stations;
    }

    public List<EntityUid> GetStationsAvailableTo(string realName)
    {
        var stations = GetStations();
        List<EntityUid> possibleStations = new();
        foreach (var iStation in stations)
        {
            if (TryComp<StationDataComponent>(iStation, out var owningSD) && owningSD != null)
            {
                if (owningSD.Owners.Contains(realName))
                {
                    possibleStations.Add(iStation);
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(iStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(realName, out var crewRecord) && crewRecord != null)
                        {
                            possibleStations.Add(iStation);
                        }
                    }
                }
            }
        }
        return possibleStations;
    }

    public EntityUid? GetStationByID(int uid)
    {
        var stations = GetStations();
        foreach (var station in stations)
        {
            if(TryComp<StationDataComponent>(station, out var stationData))
            {
                if (stationData.UID == uid) return station;
            }
        }
        return null;
    }
    public HashSet<EntityUid> GetStationsSet()
    {
        var stations = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            stations.Add(uid);
        }

        return stations;
    }

    public List<(string Name, NetEntity Entity)> GetStationNames()
    {
        var stations = GetStationsSet();
        var stats = new List<(string Name, NetEntity Station)>();

        foreach (var weh in stations)
        {
            stats.Add((MetaData(weh).EntityName, GetNetEntity(weh)));
        }

        return stats;
    }

    /// <summary>
    /// Returns the first station that has a grid in a certain map.
    /// If the map has no stations, null is returned instead.
    /// </summary>
    /// <remarks>
    /// If there are multiple stations on a map it is probably arbitrary which one is returned.
    /// </remarks>
    public EntityUid? GetStationInMap(MapId map)
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out var data))
        {
            foreach (var gridUid in data.Grids)
            {
                if (Transform(gridUid).MapID == map)
                {
                    return uid;
                }
            }
        }

        return null;
    }
}
