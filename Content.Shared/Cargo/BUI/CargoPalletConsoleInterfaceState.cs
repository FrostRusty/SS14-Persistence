using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.BUI;

[NetSerializable, Serializable]
public sealed class CargoPalletConsoleInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// estimated apraised value of all the entities on top of pallets on the same grid as the console
    /// </summary>
    public int Appraisal;

    /// <summary>
    /// number of entities on top of pallets on the same grid as the console
    /// </summary>
    public int Count;

    /// <summary>
    /// are the buttons enabled
    /// </summary>
    public bool Enabled;

    public bool CashMode;

    public int Tax;

    public string StationName;
    public CargoPalletConsoleInterfaceState(int appraisal, int count, bool enabled, bool cashmode, int tax, string stationName)
    {
        Appraisal = appraisal;
        Count = count;
        Enabled = enabled;
        CashMode = cashmode;
        Tax = tax;
        StationName = stationName;
    }
}
