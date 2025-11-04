using System;
using System.Linq;

namespace home_rental_tool.Domain.Rentals
{
    public sealed class CsvLateFeeStrategy : ILateFeeStrategy
    {
        private readonly System.Collections.Generic.List<LateFeeRow> _rows;
        public CsvLateFeeStrategy(System.Collections.Generic.IEnumerable<LateFeeRow> rows) => _rows = rows.ToList();

        public Money Calculate(MembershipLevel level, TimeSpan late, Money dayRate)
        {
            if (late <= TimeSpan.FromHours(1)) return Money.Zero;

            if (late <= TimeSpan.FromHours(4))
            {
                if (TryGetPerHour("1–4", out var perHour, level) || TryGetPerHour("1-4", out perHour, level))
                    return perHour * (decimal)late.TotalHours;
                return Money.Zero;
            }

            if (late <= TimeSpan.FromHours(24))
            {
                if (TryGetPerHour("4–24", out var perHour, level) || TryGetPerHour("4-24", out perHour, level))
                    return perHour * (decimal)late.TotalHours;
                return Money.Zero;
            }

            if (late <= TimeSpan.FromDays(3))
            {
                if (TryGetFactor("1–3", out var factor, level) || TryGetFactor("1-3", out factor, level))
                    return dayRate * factor;
                return Money.Zero;
            }

            if (TryGetFactor("3+ Tage", out var factor3p, level) || TryGetFactor("3+", out factor3p, level))
                return dayRate * factor3p;

            return Money.Zero;
        }

        private bool TryGetPerHour(string bandContains, out Money rate, MembershipLevel level)
        {
            var row = _rows.FirstOrDefault(r =>
                r.Band.Contains(bandContains, StringComparison.OrdinalIgnoreCase) &&
                r.PerHourMap.Count > 0);

            if (row is null) { rate = Money.Zero; return false; }
            rate = row.PerHour(level);
            return rate.Amount > 0m;
        }

        private bool TryGetFactor(string bandContains, out decimal factor, MembershipLevel level)
        {
            var row = _rows.FirstOrDefault(r =>
                r.Band.Contains(bandContains, StringComparison.OrdinalIgnoreCase) &&
                r.FactorMap.Count > 0);

            if (row is null) { factor = 0m; return false; }
            factor = row.Factor(level);
            return factor > 0m;
        }
    }
}
