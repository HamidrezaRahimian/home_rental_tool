namespace home_rental_tool.Domain.CreditSystem
{
    public abstract record CreditCommand;
    public sealed record EarnCredits(Credits Amount, string Reason) : CreditCommand;
    public sealed record SpendCredits(Credits Amount, string Reason) : CreditCommand;
}
