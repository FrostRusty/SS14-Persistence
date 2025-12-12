using Content.Client.Cargo.UI;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Events;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Cargo.BUI;

public sealed class CargoPalletConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CargoPalletMenu? _menu;

    public CargoPalletConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CargoPalletMenu>();
        _menu.AppraiseRequested += OnAppraisal;
        _menu.SellRequested += OnSell;
        _menu.ChangeMoneyMode += OnChangeMoneyMode;
    }

    private void OnAppraisal()
    {
        SendMessage(new CargoPalletAppraiseMessage());
    }

    private void OnSell()
    {
        SendMessage(new CargoPalletSellMessage());
    }

    private void OnChangeMoneyMode()
    {
        SendMessage(new CargoPalletChangeMoneyMode());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CargoPalletConsoleInterfaceState palletState)
            return;
        _menu?.SetStation(palletState.StationName);
        _menu?.SetEnabled(palletState.Enabled);
        _menu?.SetAppraisal(palletState.Appraisal, palletState.Tax, palletState.CashMode);
        _menu?.SetCount(palletState.Count);
        _menu?.SetMoneyMode(palletState.CashMode);
    }
}
