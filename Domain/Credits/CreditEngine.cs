using System.Collections.Generic;

namespace home_rental_tool.Domain.CreditSystem
{
    public sealed class CreditEngine
    {
        public Credits Balance { get; private set; } = Credits.Zero;
        public IReadOnlyList<string> Ledger => _ledger;
        private readonly List<string> _ledger = new();

        public void Execute(CreditCommand cmd)
        {
            switch (cmd)
            {
                case EarnCredits(var amount, var why):
                    Balance += amount;
                    _ledger.Add($"+{amount.Value} - {why}");
                    break;
                case SpendCredits(var amount, var why):
                    var spend = amount.Value > Balance.Value ? Balance : amount;
                    Balance -= spend;
                    _ledger.Add($"-{spend.Value} - {why}");
                    break;
            }
        }
    }
}
