using home_rental_tool.Domain;
using home_rental_tool.Domain.Pricing;
using home_rental_tool.Domain.Rentals;
using home_rental_tool.Domain.Reporting;
using home_rental_tool.Domain.CreditSystem;   // CreditEngine, EarnCredits, etc.
using home_rental_tool.Infra.Csv;            // Loaders lives here



namespace home_rental_tool
{
    internal static class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("== home_rental_tool console boot ==");
            var baseDir = AppContext.BaseDirectory;
            var model = Path.Combine(baseDir, "Model");
            if (!Directory.Exists(model))
            {
                Console.WriteLine($"Model folder not found: {model}");
                Console.WriteLine("Create a 'Model' folder next to the exe and drop the CSVs in there. Exiting.");
                return;
            }

            // Load CSV-backed data
            var memberPath = Path.Combine(model, "Mitgliedschaftsstufen.csv");
            var toolsPath = Path.Combine(model, "WerkzeugkategorienUndPreise.csv");
            var seasonsPath = Path.Combine(model, "SaisonaleAngebote.csv");
            var windowsPath = Path.Combine(model, "ZeitfensterMultiplikatorenCredits.csv");
            var lateFeesPath = Path.Combine(model, "VerspätungsgebuhrenStaffel.csv");

            Console.WriteLine("\n-- Loading CSVs --");
            var membership = Loaders.LoadMembership(memberPath);
            var tools = Loaders.LoadTools(toolsPath);
            var seasons = Loaders.LoadSeasons(seasonsPath);
            var windows = Loaders.LoadTimeWindows(windowsPath);
            var lateFees = Loaders.LoadLateFees(lateFeesPath);

            Console.WriteLine($"Membership rows: {membership.Count}");
            Console.WriteLine($"Tool price rows: {tools.Count}");
            Console.WriteLine($"Season rows:     {seasons.Count}");
            Console.WriteLine($"Time windows:    {windows.Count}");
            Console.WriteLine($"Late fee rows:   {lateFees.Count}");

            // Compose strategies and pipeline
            IPricingStrategy pricing = new CsvPricingStrategy(tools);

            var pipeline = new MembershipDiscountHandler(membership);
            pipeline
                .SetNext(new SeasonalOfferHandler(seasons))
                .SetNext(new TimeWindowMultiplierHandler(windows))
                .SetNext(new BehaviorBonusHandler(new CreditRules()));

            ILateFeeStrategy late = new CsvLateFeeStrategy(lateFees);
            var creditEngine = new CreditEngine();

            var engine = new PricingEngine(pricing, pipeline, late, creditEngine);

            // Demo Use case
            var start = new DateTime(2025, 11, 7, 18, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2025, 11, 9, 20, 0, 0, DateTimeKind.Utc);

            var rental = new Rental(MembershipLevel.Plus, ToolTier.Tier3, TimeWindow.Weekend, start, end, early: true);

            Console.WriteLine("\n-- Rental lifecycle --");
            Console.WriteLine("State: Reserved -> Active -> Returned -> Inspected -> Closed");
            rental.State.Activate();
            rental.State.Return();
            rental.State.Inspect(passed: true, clean: true);
            rental.State.Close();

            Console.WriteLine("\n-- Pricing pipeline --");
            Console.WriteLine("Stages: Membership -> Season -> TimeWindow -> BehaviorBonus");

            var summary = engine.QuoteAndApply(rental, weekendDelivery: true, InsurancePlan.Premium, late: TimeSpan.FromHours(3));

            Console.WriteLine("\n-- Summary --");
            summary.Accept(new ConsoleSummaryVisitor());

            Console.WriteLine("\n-- Credits --");
            Console.WriteLine($"Credit balance: {creditEngine.Balance.Value}");
            Console.WriteLine("Ledger:");
            foreach (var l in creditEngine.Ledger) Console.WriteLine($" - {l}");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nPress any key to exit.");
            Console.ResetColor();
            Console.ReadKey();
        }
    }
}
