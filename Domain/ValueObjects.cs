using System;
using System.Globalization;
using System.Linq;

namespace home_rental_tool.Domain
{
    public readonly record struct Credits(int Value)
    {
        public static Credits Zero => new(0);
        public static Credits FromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Zero;
            var clean = new string(s.Where(char.IsDigit).ToArray());
            return int.TryParse(clean, out var v) ? new Credits(v) : Zero;
        }
        public override string ToString() => $"{Value} cr";
        public static Credits operator +(Credits a, Credits b) => new(a.Value + b.Value);
        public static Credits operator -(Credits a, Credits b) => new(Math.Max(0, a.Value - b.Value));
        public static Credits FromMoney(Money m, decimal rate) =>
            new((int)Math.Round(m.Amount * rate, MidpointRounding.AwayFromZero));
    }

    // CSV-mapped domain rows
    public sealed record MembershipRow(MembershipLevel Level, Money Fee, int MonthlyCredits, decimal DiscountPercent);
    public sealed record ToolRow(ToolTier Tier, Money Price4h, Money PriceDay, Money PriceWeekend, Money PriceWeek, Credits Credits);

    public enum SeasonType { PricePercentOff, DoubleCredits }
    public sealed record SeasonRow(string Name, SeasonType Type, decimal PercentOff, decimal CreditRateForDouble,
                                   DateTime From, DateTime To)
    {
        public bool IsActive(DateTime start, DateTime end) => start.Date <= To.Date && end.Date >= From.Date;
    }

    public sealed record TimeWindowRow(string Label, decimal PriceMultiplier, decimal ExtraCreditBonusRate, string Availability)
    {
        public bool Matches(DateTime start, DateTime end)
        {
            var duration = end - start;
            if (Label.Contains("Langzeit", StringComparison.OrdinalIgnoreCase)) return duration.TotalDays >= 28;
            if (Label.Contains("Wochenend-Pauschale", StringComparison.OrdinalIgnoreCase)) return IsWeekendPackage(start, end);
            if (Label.Contains("Wochenend-Standard", StringComparison.OrdinalIgnoreCase)) return IsWeekend(start);
            if (Label.StartsWith("Abend", StringComparison.OrdinalIgnoreCase)) return start.Hour >= 18 && start.Hour < 22;
            return start.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday && start.Hour >= 8 && start.Hour < 18;
        }
        private static bool IsWeekend(DateTime start) =>
            start.DayOfWeek == DayOfWeek.Saturday || start.DayOfWeek == DayOfWeek.Sunday;
        private static bool IsWeekendPackage(DateTime start, DateTime end) =>
            start.DayOfWeek == DayOfWeek.Friday && start.Hour >= 18 && end.DayOfWeek == DayOfWeek.Monday && end.Hour <= 8;
    }

    public sealed record LateFeeRow(string Band,
        System.Collections.Generic.Dictionary<MembershipLevel, Money> PerHourMap,
        System.Collections.Generic.Dictionary<MembershipLevel, decimal> FactorMap)
    {
        public Money PerHour(MembershipLevel l) => PerHourMap.TryGetValue(l, out var m) ? m : Money.Zero;
        public decimal Factor(MembershipLevel l) => FactorMap.TryGetValue(l, out var f) ? f : 1m;
    }

    public sealed class CreditRules
    {
        public decimal EarlyReturnPercent { get; init; } = 0.10m; // +10% of rental cost
        public Credits CleanReturnFixed { get; init; } = new(20);
        public Credits EarlyReturnPercentOfCost(Money cost) =>
            Credits.FromMoney(cost, EarlyReturnPercent * 5); // £1 = 5 credits mapping for bonuses
    }
}
