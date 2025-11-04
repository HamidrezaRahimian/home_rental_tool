using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using home_rental_tool.Domain;

namespace home_rental_tool.Infra.Csv
{
    public static class Loaders
    {
        public static List<MembershipRow> LoadMembership(string path)
        {
            var rows = Csv.Read(path).SkipHeader(r => r[0].StartsWith("Stufe", StringComparison.OrdinalIgnoreCase), r =>
            {
                var level = r[0].Trim() switch
                {
                    "Pay-as-you-go" => MembershipLevel.PayAsYouGo,
                    "DIY Basic" => MembershipLevel.Basic,
                    "DIY Plus" => MembershipLevel.Plus,
                    "Pro" => MembershipLevel.Pro,
                    "Contractor" => MembershipLevel.Contractor,
                    _ => MembershipLevel.PayAsYouGo
                };
                var discount = r[3].Replace("%", "").Trim();
                var discountDec = decimal.TryParse(discount, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d / 100m : 0m;
                var monthlyCredits = new string(r[2].Where(char.IsDigit).ToArray());
                int.TryParse(monthlyCredits, out var cr);
                return new MembershipRow(level, Money.FromString(r[1]), cr, discountDec);
            }).ToList();
            return rows;
        }

        public static List<ToolRow> LoadTools(string path)
        {
            var rows = Csv.Read(path).SkipHeader(r => r[0].StartsWith("Kategorie", StringComparison.OrdinalIgnoreCase), r =>
            {
                var tier = r[0].Contains("Tier 1") ? ToolTier.Tier1 :
                           r[0].Contains("Tier 2") ? ToolTier.Tier2 :
                           r[0].Contains("Tier 3") ? ToolTier.Tier3 :
                           r[0].Contains("Tier 4") ? ToolTier.Tier4 : ToolTier.Tier5;
                return new ToolRow(
                    tier,
                    Money.FromString(r[3]),
                    Money.FromString(r[4]),
                    Money.FromString(r[5]),
                    Money.FromString(r[6]),
                    Credits.FromString(r[7])
                );
            }).ToList();
            return rows;
        }

        public static List<SeasonRow> LoadSeasons(string path)
        {
            var rows = Csv.Read(path).SkipHeader(r => r[0].StartsWith("Saison", StringComparison.OrdinalIgnoreCase), r =>
            {
                var name = r[0];
                var range = r[1];
                var offer = r[2];

                SeasonType type;
                decimal percent = 0m;
                decimal doubleRate = 0m;

                if (offer.Contains("Doppelte Credits", StringComparison.OrdinalIgnoreCase))
                {
                    type = SeasonType.DoubleCredits;
                    doubleRate = 10m;
                }
                else
                {
                    type = SeasonType.PricePercentOff;
                    var digits = new string(offer.Where(char.IsDigit).ToArray());
                    percent = string.IsNullOrWhiteSpace(digits) ? 0m : int.Parse(digits) / 100m;
                }

                var (from, to) = ParseRange(range);
                return new SeasonRow(name, type, percent, doubleRate, from, to);
            }).ToList();
            return rows;

            static (DateTime from, DateTime to) ParseRange(string s)
            {
                var nowYear = DateTime.UtcNow.Year;
                s = s.Trim();
                if (s.Equals("November", StringComparison.OrdinalIgnoreCase))
                    return (new DateTime(nowYear, 11, 1), new DateTime(nowYear, 11, 30));
                if (s.Contains("Jun", StringComparison.OrdinalIgnoreCase))
                    return (new DateTime(nowYear, 6, 1), new DateTime(nowYear, 8, 31));
                if (s.Contains("Mär", StringComparison.OrdinalIgnoreCase) || s.Contains("Mar", StringComparison.OrdinalIgnoreCase))
                    return (new DateTime(nowYear, 3, 1), new DateTime(nowYear, 4, 30));
                if (s.Contains("Sep", StringComparison.OrdinalIgnoreCase))
                    return (new DateTime(nowYear, 9, 1), new DateTime(nowYear, 10, 31));
                if (s.Contains("Nov–Feb", StringComparison.OrdinalIgnoreCase))
                    return (new DateTime(nowYear, 11, 1), new DateTime(nowYear + 1, 2, 28));
                return (new DateTime(nowYear, 1, 1), new DateTime(nowYear, 12, 31));
            }
        }

        public static List<TimeWindowRow> LoadTimeWindows(string path)
        {
            var rows = Csv.Read(path).SkipHeader(r => r[0].StartsWith("Zeitfenster", StringComparison.OrdinalIgnoreCase), r =>
            {
                var label = r[0];
                var mult = r[1].Replace("x", "").Trim();
                decimal.TryParse(mult, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var m);
                var creditsBonus = r[2];
                decimal bonusRate = creditsBonus.Contains("%", StringComparison.OrdinalIgnoreCase)
                    ? int.Parse(new string(creditsBonus.Where(char.IsDigit).ToArray())) / 100m * 0.5m
                    : 0m;
                return new TimeWindowRow(label, m == 0m ? 1m : m, bonusRate, r[3]);
            }).ToList();
            return rows;
        }

        public static List<LateFeeRow> LoadLateFees(string path)
        {
            static string Norm(string s)
                => s.Replace("–", "-", StringComparison.Ordinal)
                    .Replace("×", "x", StringComparison.Ordinal)
                    .Trim();

            var rows = new List<LateFeeRow>();

            foreach (var r in Csv.Read(path))
            {
                if (r[0].StartsWith("Verspätung", StringComparison.OrdinalIgnoreCase)) continue;

                var band = Norm(r[0]);
                if (band.Contains("0-1", StringComparison.OrdinalIgnoreCase)) continue;

                bool isHourBand = r[1].Contains("/h", StringComparison.OrdinalIgnoreCase) ||
                                  r[2].Contains("/h", StringComparison.OrdinalIgnoreCase);

                if (isHourBand)
                {
                    Money ParsePerHour(string s) => Money.FromString(Norm(s).Replace("/h", "", StringComparison.OrdinalIgnoreCase));
                    var perHourMap = new Dictionary<MembershipLevel, Money>
                    {
                        { MembershipLevel.PayAsYouGo, ParsePerHour(r[1]) },
                        { MembershipLevel.Basic,      ParsePerHour(r[2]) },
                        { MembershipLevel.Plus,       ParsePerHour(r[3]) },
                        { MembershipLevel.Pro,        ParsePerHour(r[4]) },
                        { MembershipLevel.Contractor, ParsePerHour(r[5]) },
                    };
                    rows.Add(new LateFeeRow(band, perHourMap, new Dictionary<MembershipLevel, decimal>()));
                    continue;
                }

                decimal ParseFactor(string s)
                {
                    var clean = Norm(s).ToLowerInvariant();
                    var digits = new string(clean.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
                    return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 1m;
                }

                var factorMap = new Dictionary<MembershipLevel, decimal>
                {
                    { MembershipLevel.PayAsYouGo, ParseFactor(r[1]) },
                    { MembershipLevel.Basic,      ParseFactor(r[2]) },
                    { MembershipLevel.Plus,       ParseFactor(r[3]) },
                    { MembershipLevel.Pro,        ParseFactor(r[4]) },
                    { MembershipLevel.Contractor, ParseFactor(r[5]) },
                };
                rows.Add(new LateFeeRow(band, new Dictionary<MembershipLevel, Money>(), factorMap));
            }

            return rows;
        }
    }
}
