using System;

namespace home_rental_tool.Domain.Reporting
{
    public interface ISummaryVisitor
    {
        void Visit(RentalSummary summary);
    }

    public sealed record RentalSummary(
        Guid RentalId,
        MembershipLevel Membership,
        ToolTier Tier,
        TimeWindow Window,
        Money BasePrice,
        Money FinalPrice,
        Credits EarnedCredits,
        Credits SpentCredits,
        Money LateFees,
        Money InsuranceCost,
        Money DeliveryCost
    )
    {
        public void Accept(ISummaryVisitor v) => v.Visit(this);
    }

    public sealed class ConsoleSummaryVisitor : ISummaryVisitor
    {
        public void Visit(RentalSummary s)
        {
            Console.WriteLine("---- Rental Summary ----");
            Console.WriteLine($"Rental: {s.RentalId}");
            Console.WriteLine($"Member: {s.Membership}  Tier: {s.Tier}  Window: {s.Window}");
            Console.WriteLine($"Base: {s.BasePrice}  Final: {s.FinalPrice}");
            Console.WriteLine($"Credits +{s.EarnedCredits.Value}  -{s.SpentCredits.Value}");
            Console.WriteLine($"Late: {s.LateFees}  Insurance: {s.InsuranceCost}  Delivery: {s.DeliveryCost}");
            Console.WriteLine("------------------------");
        }
    }
}
