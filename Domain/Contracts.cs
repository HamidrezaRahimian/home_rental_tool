using System;

namespace home_rental_tool.Domain
{
    public interface IPricingStrategy
    {
        Money GetBasePrice(ToolTier tier, TimeWindow window);
        Credits GetBaseCredits(ToolTier tier);
    }

    public interface ILateFeeStrategy
    {
        Money Calculate(MembershipLevel level, TimeSpan lateDuration, Money dayRate);
    }

    public interface IDiscountHandler
    {
        IDiscountHandler SetNext(IDiscountHandler next);
        DiscountResult Apply(DiscountContext ctx);
    }

    public sealed record DiscountContext(
        MembershipLevel Membership,
        ToolTier Tier,
        TimeWindow Window,
        Money BasePrice,
        DateTime StartUtc,
        DateTime EndUtc,
        bool CleanReturn,
        bool EarlyReturn,
        bool WeekendPackageApplied,
        string? Season
    );

    public sealed record DiscountResult(Money PriceAfter, Credits CreditEarned);
}
