using Content.Shared.Cargo.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Components;

/// <summary>
/// Target for approved orders to spawn at.
/// </summary>
[RegisterComponent]
public sealed partial class TradeStationComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UID = 0;

    [DataField]
    public List<ProtoId<CargoMarketPrototype>> Markets = new()
    {
        "market"
    };
}
