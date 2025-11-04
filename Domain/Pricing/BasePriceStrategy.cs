using System.Collections.Generic;
using home_rental_tool.Domain;

namespace home_rental_tool.Domain.Pricing
{
    public sealed class CsvPricingStrategy : IPricingStrategy
    {
        private readonly Dictionary<(ToolTier, TimeWindow), Money> _prices = new();
        private readonly Dictionary<ToolTier, Credits> _credits = new();

        public CsvPricingStrategy(IEnumerable<ToolRow> toolRows)
        {
            foreach (var r in toolRows)
            {
                _prices[(r.Tier, TimeWindow.FourHours)] = r.Price4h;
                _prices[(r.Tier, TimeWindow.Day)] = r.PriceDay;
                _prices[(r.Tier, TimeWindow.Weekend)] = r.PriceWeekend;
                _prices[(r.Tier, TimeWindow.Week)] = r.PriceWeek;
                _credits[r.Tier] = r.Credits;
            }
        }

        public Money GetBasePrice(ToolTier tier, TimeWindow window) =>
            _prices.TryGetValue((tier, window), out var m) ? m : Money.Zero;

        public Credits GetBaseCredits(ToolTier tier) =>
            _credits.TryGetValue(tier, out var c) ? c : Credits.Zero;
    }
}
