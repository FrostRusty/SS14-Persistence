using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.GameTicking;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Bank;

// Shared is abstract.
public sealed partial class BankSystem : SharedBankSystem
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    private ISawmill _log = default!;

    public MoneyAccountsComponent? GetMoneyAccountsComponent()
    {

        var personalAccountQuery = AllEntityQuery<MoneyAccountsComponent>();
        while(personalAccountQuery.MoveNext(out var uid, out var comp))
        {
            return comp;
        }
        return null;
    }
    public bool TryGetBalance(EntityUid ent, out int balance)
    {
        balance = 0;
        var component = GetMoneyAccountsComponent();
        if (component == null)
        {
            return false;
        }
        MoneyAccountsComponent? accounts = component;
        var accName = Name(ent);
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            _log.Info($"TryGetBalance: {ent} has no bank account");
            return false;
        }

        balance = account!.Balance;
        return true;
    }

}
