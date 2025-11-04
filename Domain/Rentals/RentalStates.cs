using System;

namespace home_rental_tool.Domain.Rentals
{
    public abstract class RentalState
    {
        protected Rental Rental { get; }
        protected RentalState(Rental r) => Rental = r;

        public virtual void Activate() => throw new InvalidOperationException("Cannot activate from current state");
        public virtual void Return() => throw new InvalidOperationException("Cannot return from current state");
        public virtual void Inspect(bool passed, bool clean) => throw new InvalidOperationException("Cannot inspect from current state");
        public virtual void Close() => throw new InvalidOperationException("Cannot close from current state");
    }

    public sealed class ReservedState : RentalState
    {
        public ReservedState(Rental r) : base(r) { }
        public override void Activate() => Rental.State = new ActiveState(Rental);
    }

    public sealed class ActiveState : RentalState
    {
        public ActiveState(Rental r) : base(r) { }
        public override void Return() => Rental.State = new ReturnedState(Rental);
    }

    public sealed class ReturnedState : RentalState
    {
        public ReturnedState(Rental r) : base(r) { }
        public override void Inspect(bool passed, bool clean)
        {
            Rental.CleanReturn = clean;
            Rental.State = new InspectedState(Rental, passed);
        }
    }

    public sealed class InspectedState : RentalState
    {
        private readonly bool _passed;
        public InspectedState(Rental r, bool passed) : base(r) => _passed = passed;
        public override void Close()
        {
            if (!_passed) throw new InvalidOperationException("Inspection failed");
            Rental.State = new ClosedState(Rental);
        }
    }

    public sealed class ClosedState : RentalState
    {
        public ClosedState(Rental r) : base(r) { }
    }
}
