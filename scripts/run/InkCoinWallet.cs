using Godot;

/// <summary>
/// Stores the spirit ink coins owned during the current run.
/// Merchants can use CanAfford and TrySpend without depending on any UI code.
/// </summary>
[GlobalClass]
public partial class InkCoinWallet : Node
{
    [Signal]
    public delegate void BalanceChangedEventHandler(int balance, int delta);

    [Export(PropertyHint.Range, "0,999999,1")]
    public int StartingBalance { get; set; }

    public int Balance { get; private set; }

    public override void _Ready()
    {
        Balance = Mathf.Max(StartingBalance, 0);
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && Balance >= amount;
    }

    public int AddCoins(int amount)
    {
        if (amount <= 0)
            return 0;

        int previousBalance = Balance;
        Balance = amount > int.MaxValue - Balance
            ? int.MaxValue
            : Balance + amount;
        int added = Balance - previousBalance;

        if (added > 0)
        {
            EmitSignal(
                SignalName.BalanceChanged,
                Balance,
                added
            );
        }

        return added;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || Balance < amount)
            return false;

        if (amount == 0)
            return true;

        Balance -= amount;
        EmitSignal(
            SignalName.BalanceChanged,
            Balance,
            -amount
        );
        return true;
    }

    /// <summary>Used by run loading and debug setup.</summary>
    public void SetBalance(int amount)
    {
        int nextBalance = Mathf.Max(amount, 0);
        int delta = nextBalance - Balance;

        if (delta == 0)
            return;

        Balance = nextBalance;
        EmitSignal(SignalName.BalanceChanged, Balance, delta);
    }
}
