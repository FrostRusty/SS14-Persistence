using Content.Server.Construction.Completions;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.GridControl.Components;
using Content.Shared.GridControl.Systems;
using Content.Shared.Interaction;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System;
using System.Globalization;
using System.Linq;
using static Content.Shared.GridControl.Components.GridConfigComponent;
using static Content.Shared.GridControl.Components.StationCreatorComponent;

namespace Content.Server.GridControl.Systems;

[UsedImplicitly]
public sealed class GridConfigSystem : SharedGridConfigSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridConfigComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<GridConfigComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<GridConfigComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<StationCreatorComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<StationCreatorComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<GridConfigComponent, AfterInteractEvent>(AfterInteractOn);
        SubscribeLocalEvent<GridConfigComponent, GridConfigDoAfterEvent>(OnDoAfter);


        Subs.BuiEvents<GridConfigComponent>(GridConfigUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<GridConfigChangeName>(OnChangeName);
            subs.Event<GridConfigTargetSelect>(OnTargetSelect);
            subs.Event<GridConfigChangeMode>(OnChangeMode);
            subs.Event<GridConfigConnect>(OnConnect);
            subs.Event<GridConfigDisconnect>(OnDisconnect);
        });



        Subs.BuiEvents<StationCreatorComponent>(StationCreatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<StationCreatorFinish>(OnStationCreate);
        });

    }

    private void OnRemoved(EntityUid uid, GridConfigComponent component, EntityEventArgs args)
    {
        component.ConnectedStation = null;
        UpdateUserInterface(uid, component, args);
    }
    private void OnRemoved(EntityUid uid, StationCreatorComponent component, EntityEventArgs args)
    {
        component.ConnectedStation = null;
        UpdateUserInterface(uid, component, args);
    }
    private void OnChangeMode(EntityUid uid, GridConfigComponent component, GridConfigChangeMode args)
    {
        component.PersonalMode = !component.PersonalMode;
        UpdateUserInterface(uid, component, args);
    }
    private void OnTargetSelect(EntityUid uid, GridConfigComponent component, GridConfigTargetSelect args)
    {
        component.ConnectedStation = args.Target;
        UpdateUserInterface(uid, component, args);
    }

    private bool Validate(EntityUid uid, GridConfigComponent component, bool requireTarget = true)
    {
        EntityUid? owningStation = _station.GetOwningStation(uid);
        bool owningPersonal = false;
        string? owningPerson = null;
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }
        if (!idPresent) return false;
        if (owningStation == null)
        {
            owningPerson = _station.GetOwningStationPersonal(uid);
            if (owningPerson != null)
            {
                owningPersonal = true;
            }
        }
        else
        {
            if (TryComp<StationDataComponent>(owningStation, out var oSD) && oSD != null)
            {
                owningPerson = oSD.StationName;

            }
        }
        bool isOwner = false;
        bool isAuth = false;
        if (owningStation != null)
        {
            if (TryComp<StationDataComponent>(owningStation, out var owningSD) && owningSD != null)
            {
                if (owningSD.Owners.Contains(privilegedName))
                {
                    isOwner = true;
                    isAuth = true;
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(owningStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(owningStation, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    if (crewAssignment.CanClaim)
                                    {
                                        isAuth = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (owningPersonal)
        {
            if (owningPerson == privilegedName)
            {
                isOwner = true;
                isAuth = true;
            }
        }
        if (!owningPersonal && owningStation == null) isAuth = true;
        if (!requireTarget) return isAuth;
        if(isAuth)
        {
            isAuth = false;
            if (component.PersonalMode) return true;
            if (component.ConnectedStation == null) return false;
            var targetStation = _station.GetStationByID(component.ConnectedStation.Value);
            if (targetStation == null) return false;
            if (TryComp<StationDataComponent>(targetStation, out var owningSD) && owningSD != null)
            {
                if (owningSD.Owners.Contains(privilegedName))
                {
                    isAuth = true;
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(targetStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(targetStation, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    if (crewAssignment.CanClaim)
                                    {
                                        isAuth = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return isAuth;
    }
    private void OnChangeName(EntityUid uid, GridConfigComponent component, GridConfigChangeName args)
    {
        if(Validate(uid, component, false))
        {
            var grid = _transform.GetGrid(uid);
            if (grid != null)
            {
                _metaData.SetEntityName(grid.Value, args.Name);
            }
        }
            UpdateUserInterface(uid, component, args);
    }
    private void OnConnect(EntityUid uid, GridConfigComponent component, GridConfigConnect args)
    {
        if (Validate(uid, component))
        {
            var grid = _transform.GetGrid(uid);
            if (grid != null)
            {
                if (component.PersonalMode)
                {
                    if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
                    {
                        if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
                        {
                            _station.AddGridToPerson(id.FullName, grid.Value);
                        }

                    }
                }
                else if(component.ConnectedStation != null)
                {
                    var station = _station.GetStationByID(component.ConnectedStation.Value);
                    if (station != null)
                    {
                        _station.AddGridToStation(station.Value, grid.Value);
                    }
                }
            }
        }
        UpdateUserInterface(uid, component, args);
    }

    private void OnStationCreate(EntityUid uid, StationCreatorComponent component, StationCreatorFinish args)
    {
        string? realName = null;
        EntityUid? idCard = null;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idc)
        {
            idCard = idc;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
               realName = id.FullName;
            }

        }
        if (realName == null || realName == "") return;
        if (args.StationName == null || args.StationName == "") return;
        StationConfig config = new();
        config.StationPrototype = "StandardNanotrasenStation";
        _station.InitializeNewStation(config, null, args.StationName, realName);
        if(idCard != null)
        {
            _popupSystem.PopupEntity($"The station {args.StationName} was created.", idCard.Value);
            _itemSlots.TryEjectToHands(idCard.Value, component.PrivilegedIdSlot, args.Actor);
        }
        EntityManager.TryQueueDeleteEntity(uid);
    }

    private void OnDisconnect(EntityUid uid, GridConfigComponent component, GridConfigDisconnect args)
    {

        if (Validate(uid, component))
        {
            var grid = _transform.GetGrid(uid);
            if (grid != null)
            {
                var station = _station.GetOwningStation(grid.Value);
                if(station != null)
                {
                    _station.RemoveGridFromStation(station.Value, grid.Value);
                }
                else
                {
                    var ownername = _station.GetOwningStationPersonal(grid.Value);
                    if(ownername != null)
                    {
                        _station.RemoveGridFromPerson(grid.Value);
                    }
                }

            }
        }
        UpdateUserInterface(uid, component, args);
    }

    private void AfterInteractOn(EntityUid uid, GridConfigComponent component, AfterInteractEvent args)
    {
        return;
        if (args.Target == null || !TryComp(args.Target, out AccessReaderComponent? accessReader))
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.User, (EntityUid) args.Target))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfter, new GridConfigDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
    }


    private void OnDoAfter(EntityUid uid, GridConfigComponent component, GridConfigDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target != null)
        {
            _userInterface.OpenUi(uid, GridConfigUiKey.Key, args.User);
            UpdateUserInterface(uid, component, args);
        }

        args.Handled = true;
    }


    private void UpdateUserInterface(EntityUid uid, GridConfigComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        EntityUid? station = null;
        if (component.ConnectedStation != null)
        {
            station = _station.GetStationByID(component.ConnectedStation.Value);
        }
        EntityUid? owningStation = _station.GetOwningStation(uid);
        string? owningPerson = null;
        if (owningStation == null)
        {
            owningPerson = _station.GetOwningStationPersonal(uid);
        }
        else
        {
            if (TryComp<StationDataComponent>(owningStation, out var oSD) && oSD != null)
            {
                owningPerson = oSD.StationName;
            }
        }
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }
        bool isOwner = false;
        bool isAuth = false;
        string gridName = string.Empty;
        var grid = _transform.GetGrid(uid);
        if (grid != null)
        {
            gridName = Name(grid.Value);
        }

        if (privilegedName != "")
        {
            if (owningStation != null)
            {
                if (TryComp<StationDataComponent>(owningStation, out var owningSD) && owningSD != null)
                {
                    if (owningSD.Owners.Contains(privilegedName))
                    {
                        isOwner = true;
                        isAuth = true;
                    }
                    else
                    {
                        if (TryComp<CrewRecordsComponent>(owningStation, out var owningCrew) && owningCrew != null)
                        {
                            if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                            {
                                if (TryComp<CrewAssignmentsComponent>(owningStation, out var crewAssignments) && crewAssignments != null)
                                {
                                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                    {
                                        if (crewAssignment.CanClaim)
                                        {
                                            isAuth = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (owningPerson != null)
            {
                if (owningPerson == privilegedName)
                {
                    isOwner = true;
                    isAuth = true;
                }
            }
            else
            {
                if(grid != null)
                {
                    isOwner = true;
                    isAuth = true;
                }
            }

        }
        String? targetName = null;
        int? targetStation = null;
        Dictionary<int, string>? possibleStations = null;
        if (component.PersonalMode)
        {
            targetName = privilegedName;
        }
        else
        {
            if (TryComp<StationDataComponent>(station, out var SD) && SD != null)
            {
                targetName = SD.StationName;
                targetStation = SD.UID;
            }
            var stations = _station.GetStations();
            possibleStations = new Dictionary<int, string>();
            foreach (var iStation in stations)
            {
                bool auth = false;
                var station_name = "";
                int stationID = 0;
                if (TryComp<StationDataComponent>(iStation, out var owningSD) && owningSD != null)
                {
                    stationID = owningSD.UID;
                    if (owningSD.StationName != null)
                        station_name = owningSD.StationName;
                    if (owningSD.Owners.Contains(privilegedName))
                    {
                        auth = true;
                    }
                    else
                    {
                        if (TryComp<CrewRecordsComponent>(iStation, out var owningCrew) && owningCrew != null)
                        {
                            if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                            {
                                if (TryComp<CrewAssignmentsComponent>(iStation, out var crewAssignments) && crewAssignments != null)
                                {
                                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                    {
                                        if (crewAssignment.CanClaim)
                                        {
                                            auth = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (auth)
                {
                    possibleStations.Add(stationID, station_name);
                }

            }
        }

        GridConfigBoundUserInterfaceState newState;
        bool allowed = true;

        newState = new GridConfigBoundUserInterfaceState(idPresent, isOwner, isAuth, component.PersonalMode, possibleStations, targetName, owningPerson, gridName, privilegedIdName, targetStation);

        _userInterface.SetUiState(uid, GridConfigUiKey.Key, newState);
    }

    private void UpdateUserInterface(EntityUid uid, StationCreatorComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }

        StationCreatorBoundUserInterfaceState newState;

        newState = new StationCreatorBoundUserInterfaceState(idPresent, privilegedIdName, privilegedName);

        _userInterface.SetUiState(uid, StationCreatorUiKey.Key, newState);
    }

}
