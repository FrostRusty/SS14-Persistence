using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.GridControl.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GridControlConsoleComponent : Component
{
    [DataField]
    public bool Active = true;

    [Serializable, NetSerializable]
    public sealed class GridControlOn : BoundUserInterfaceMessage
    {

        public GridControlOn()
        {
        }
    }

    [Serializable, NetSerializable]
    public sealed class GridControlOff : BoundUserInterfaceMessage
    {

        public GridControlOff()
        {
        }
    }


    [Serializable, NetSerializable]
    public sealed class GridControlConsoleBoundUserInterfaceState : BoundUserInterfaceState
    {
        public bool Active;

        public GridControlConsoleBoundUserInterfaceState(bool active)
        {
            Active = active;
        }
    }

    [Serializable, NetSerializable]
    public enum GridControlConsoleUiKey : byte
    {
        Key,
    }
}
