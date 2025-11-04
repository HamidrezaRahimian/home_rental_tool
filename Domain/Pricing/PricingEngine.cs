using System;
using home_rental_tool.Domain.CreditSystem;

namespace home_rental_tool.Domain.Pricing
{
    public sealed class PricingEngine
    {
        private readonly IPricingStrategy _pricing;
        private readonly IDiscountHandler _pipeline;
        private readonly ILateFeeStrategy _lateFees;
        private readonly CreditEngine _credits;

        public PricingEngine(IPricingStrategy pricing, IDiscountHandler pipeline, ILateFeeStrategy lateFees, CreditEngine credits)
        {
            _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _lateFees = lateFees ?? throw new ArgumentNullException(nameof(lateFees));
            _credits = credits ?? throw new ArgumentNullException(nameof(credits));
        }

        public Reporting.RentalSummary QuoteAndApply(Rentals.Rental rental, bool weekendDelivery, InsurancePlan insurance, TimeSpan? late = null)
        {
            if (rental is null) throw new ArgumentNullException(nameof(rental));

            var basePrice = _pricing.GetBasePrice(rental.Tier, rental.Window);

            var ctx = new DiscountContext(
                rental.Membership,
                rental.Tier,
                rental.Window,
                basePrice,
                rental.StartUtc,
                rental.EndUtc,
                rental.CleanReturn,
                rental.EarlyReturn,
                rental.Window == TimeWindow.Weekend,
                rental.StartUtc.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture)
            );

            var discountRes = _pipeline.Apply(ctx);

            var delivery = weekendDelivery ? DeliveryCostFor(rental.Membership, 4.5m) : Money.Zero;
            var insuranceCost = InsuranceCostPerDay(insurance) * Days(rental.Window);

            var earned = discountRes.CreditEarned + _pricing.GetBaseCredits(rental.Tier);
            var spent = Credits.Zero;

            var dayRate = _pricing.GetBasePrice(rental.Tier, TimeWindow.Day);
            var lateFees = late.HasValue ? _lateFees.Calculate(rental.Membership, late.Value, dayRate) : Money.Zero;

            var final = discountRes.PriceAfter + delivery + insuranceCost + lateFees;

            if (earned.Value > 0) _credits.Execute(new EarnCredits(earned, "Rental bonuses"));

            return new Reporting.RentalSummary(
                rental.Id, rental.Membership, rental.Tier, rental.Window,
                basePrice, final, earned, spent, lateFees, insuranceCost, delivery
            );
        }

        private static Money DeliveryCostFor(MembershipLevel level, decimal distanceKm) => level switch
        {
            MembershipLevel.PayAsYouGo => new Money(15),
            MembershipLevel.Basic => new Money(12),
            MembershipLevel.Plus => new Money(8),
            MembershipLevel.Pro => new Money(5),
            MembershipLevel.Contractor => Money.Zero,
            _ => Money.Zero
        };

        private static Money InsuranceCostPerDay(InsurancePlan plan) => plan switch
        {
            InsurancePlan.Basic => Money.Zero,
            InsurancePlan.Standard => new Money(5),
            InsurancePlan.Premium => new Money(10),
            InsurancePlan.Profi => new Money(15),
            _ => Money.Zero
        };

        private static decimal Days(TimeWindow w) => w switch
        {
            TimeWindow.FourHours => 0.5m,
            TimeWindow.Day => 1m,
            TimeWindow.Weekend => 2m,
            TimeWindow.Week => 7m,
            _ => 1m
        };
    }
}
