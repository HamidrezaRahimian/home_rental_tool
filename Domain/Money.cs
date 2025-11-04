using System;
using System.Globalization;

namespace home_rental_tool.Domain
{
    public readonly record struct Money(decimal Amount)
    {
        public static Money Zero => new(0m);

        public static Money FromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Zero;
            var clean = s.Replace("£", "", StringComparison.Ordinal)
                         .Replace(",", "", StringComparison.Ordinal)
                         .Trim();
            var tokens = clean.Split(new[] { ' ', '+', '=', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var last = tokens.Length > 0 ? tokens[^1] : null;
            return decimal.TryParse(last, NumberStyles.Number | NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out var val)
                ? new Money(val)
                : Zero;
        }

        public override string ToString() => $"£{Amount:0.##}";
        public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
        public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
        public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);
    }
}
