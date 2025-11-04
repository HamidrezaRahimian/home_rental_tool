using System;

namespace home_rental_tool.Domain.Rentals
{
    public sealed class Rental
    {
        public Guid Id { get; } = Guid.NewGuid();
        public MembershipLevel Membership { get; init; }
        public ToolTier Tier { get; init; }
        public TimeWindow Window { get; init; }
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public bool CleanReturn { get; set; }
        public bool EarlyReturn { get; init; }
        public RentalState State { get; set; }

        public Rental(MembershipLevel m, ToolTier t, TimeWindow w, DateTime startUtc, DateTime endUtc, bool early)
        {
            Membership = m; Tier = t; Window = w; StartUtc = startUtc; EndUtc = endUtc; EarlyReturn = early;
            State = new ReservedState(this);
        }
    }
}
