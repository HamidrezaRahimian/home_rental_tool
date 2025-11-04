using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace home_rental_tool.Domain.Pricing
{
    public sealed class MembershipDiscountHandler : IDiscountHandler
    {
        private readonly Dictionary<MembershipLevel, decimal> _discountByLevel;
        private IDiscountHandler? _next;

        public MembershipDiscountHandler(IEnumerable<MembershipRow> members) =>
            _discountByLevel = members.ToDictionary(m => m.Level, m => m.DiscountPercent);

        public IDiscountHandler SetNext(IDiscountHandler next) { _next = next; return next; }

        public DiscountResult Apply(DiscountContext ctx)
        {
            var rate = _discountByLevel.TryGetValue(ctx.Membership, out var d) ? d : 0m;
            var price = ctx.BasePrice * (1 - rate);
            var res = new DiscountResult(price, Credits.Zero);
            return _next is null ? res : _next.Apply(ctx with { BasePrice = res.PriceAfter });
        }
    }

    public sealed class SeasonalOfferHandler : IDiscountHandler
    {
        private readonly List<SeasonRow> _seasons;
        private IDiscountHandler? _next;

        public SeasonalOfferHandler(IEnumerable<SeasonRow> seasons) => _seasons = seasons.ToList();
        public IDiscountHandler SetNext(IDiscountHandler next) { _next = next; return next; }

        public DiscountResult Apply(DiscountContext ctx)
        {
            var price = ctx.BasePrice;
            var bonusCredits = Credits.Zero;

            foreach (var s in _seasons)
            {
                if (!s.IsActive(ctx.StartUtc, ctx.EndUtc)) continue;
                if (s.Type == SeasonType.PricePercentOff) price = price * (1 - s.PercentOff);
                if (s.Type == SeasonType.DoubleCredits) bonusCredits += Credits.FromMoney(price, s.CreditRateForDouble);
            }

            var res = new DiscountResult(price, bonusCredits);
            return _next is null ? res : _next.Apply(ctx with { BasePrice = res.PriceAfter });
        }
    }

    public sealed class TimeWindowMultiplierHandler : IDiscountHandler
    {
        private readonly List<TimeWindowRow> _windows;
        private IDiscountHandler? _next;

        public TimeWindowMultiplierHandler(IEnumerable<TimeWindowRow> windows) => _windows = windows.ToList();
        public IDiscountHandler SetNext(IDiscountHandler next) { _next = next; return next; }

        public DiscountResult Apply(DiscountContext ctx)
        {
            var match = _windows.FirstOrDefault(w => w.Matches(ctx.StartUtc, ctx.EndUtc));
            var multiplier = match?.PriceMultiplier ?? 1.0m;
            var price = ctx.BasePrice * multiplier;
            var credits = Credits.FromMoney(price, match?.ExtraCreditBonusRate ?? 0m);

            var res = new DiscountResult(price, credits);
            return _next is null ? res : _next.Apply(ctx with { BasePrice = res.PriceAfter });
        }
    }

    public sealed class BehaviorBonusHandler : IDiscountHandler
    {
        private readonly CreditRules _rules;
        private IDiscountHandler? _next;

        public BehaviorBonusHandler(CreditRules rules) => _rules = rules;
        public IDiscountHandler SetNext(IDiscountHandler next) { _next = next; return next; }

        public DiscountResult Apply(DiscountContext ctx)
        {
            var price = ctx.BasePrice;
            var earned = Credits.Zero;

            if (ctx.EarlyReturn) earned += _rules.EarlyReturnPercentOfCost(price);
            if (ctx.CleanReturn) earned += _rules.CleanReturnFixed;

            var res = new DiscountResult(price, earned);
            return _next is null ? res : _next.Apply(ctx with { BasePrice = res.PriceAfter });
        }
    }
}
