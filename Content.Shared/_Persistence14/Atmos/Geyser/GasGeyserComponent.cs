using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Atmos.Geyser;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class GasGeyserComponent : Component
{
    /// <summary>
    /// Game time 
    /// </summary>
    [DataField, AutoPausedField, AutoNetworkedField]
    public TimeSpan NextEruptionTime = TimeSpan.Zero;

    /// <summary>
    /// Minimum time between erruptions.
    /// </summary>
    [DataField]
    public TimeSpan EruptionDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gases released into the atmosphere when the geyser erupts.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(GasArraySerializer)), AutoNetworkedField]
    public float[] Moles = new float[Atmospherics.AdjustedNumberOfGases];

    /// <summary>
    /// When external gas mixture exceeds this amount of moles, geysers cannot errupt.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxExternalMoles = float.PositiveInfinity;

    /// <summary>
    /// When external gas mixture exceeds this pressure, geysers cannot errupt.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxExternalPressure = Atmospherics.GasMinerDefaultMaxExternalPressure;

    /// <summary>
    /// Tempurature of gas spawned from the geyser.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SpawnTemperature = Atmospherics.T20C;

    [DataField]
    public string ErruptionAnimationKey = "geyser_animated";
}