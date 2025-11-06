using System;
using System.Globalization;
using System.IO;
using System.Linq;
using home_rental_tool.Domain;
using home_rental_tool.Domain.Pricing;
using home_rental_tool.Domain.Rentals;
using home_rental_tool.Domain.Reporting;
using home_rental_tool.Domain.CreditSystem;
using home_rental_tool.Infra.Csv;

namespace home_rental_tool
{
    internal static class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "Home Rental Tool - Debug Mode";

            try
            {
                Banner("Tool Rental Console (Diagnostic Mode)");

                var baseDir = AppContext.BaseDirectory;
                var model = Path.Combine(baseDir, "Model");

                Section("Environment");
                Console.WriteLine($"📂 BaseDirectory: {baseDir}");
                Console.WriteLine($"📁 Model path   : {model}");
                Console.WriteLine($"🕒 UTC now      : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

                if (!Directory.Exists(model))
                {
                    Fail($"Model folder NOT found at: {model}");
                    Console.WriteLine("💡 Create 'Model' folder next to the exe and put the CSV files inside.");
                    PauseExit();
                    return;
                }

                DumpModelDir(model);

                // -----------------------------------
                // Load CSV-backed data
                // -----------------------------------
                Info("📥 Loading CSV files...");
                var membership = TryLoad(() => Loaders.LoadMembership(Path.Combine(model, "Mitgliedschaftsstufen.csv")), "Mitgliedschaftsstufen.csv");
                var tools = TryLoad(() => Loaders.LoadTools(Path.Combine(model, "WerkzeugkategorienUndPreise.csv")), "WerkzeugkategorienUndPreise.csv");
                var seasons = TryLoad(() => Loaders.LoadSeasons(Path.Combine(model, "SaisonaleAngebote.csv")), "SaisonaleAngebote.csv");
                var windows = TryLoad(() => Loaders.LoadTimeWindows(Path.Combine(model, "ZeitfensterMultiplikatorenCredits.csv")), "ZeitfensterMultiplikatorenCredits.csv");
                var lateFees = TryLoad(() => Loaders.LoadLateFees(Path.Combine(model, "VerspätungsgebuhrenStaffel.csv")), "VerspätungsgebuhrenStaffel.csv");

                Section("CSV row counts");
                PrintCount("Membership", membership.Count);
                PrintCount("Tools", tools.Count);
                PrintCount("Season offers", seasons.Count);
                PrintCount("Time windows", windows.Count);
                PrintCount("Late fee bands", lateFees.Count);

                // -----------------------------------
                // Composition of core modules
                // -----------------------------------
                Section("Composing business logic modules");
                IPricingStrategy pricing = new CsvPricingStrategy(tools);
                var pipeline = new MembershipDiscountHandler(membership);
                pipeline
                    .SetNext(new SeasonalOfferHandler(seasons))
                    .SetNext(new TimeWindowMultiplierHandler(windows))
                    .SetNext(new BehaviorBonusHandler(new CreditRules()));
                ILateFeeStrategy late = new CsvLateFeeStrategy(lateFees);
                var creditEngine = new CreditEngine();

                Success("✅ Pricing pipeline built successfully!");
                Console.WriteLine("Pipeline order:");
                Console.WriteLine("   1️⃣ MembershipDiscountHandler");
                Console.WriteLine("   2️⃣ SeasonalOfferHandler");
                Console.WriteLine("   3️⃣ TimeWindowMultiplierHandler");
                Console.WriteLine("   4️⃣ BehaviorBonusHandler");
                Console.WriteLine();

                // -----------------------------------
                // Demo use-case
                // -----------------------------------
                Section("Simulating rental process");

                var start = new DateTime(2025, 11, 7, 18, 0, 0, DateTimeKind.Utc);
                var end = new DateTime(2025, 11, 9, 20, 0, 0, DateTimeKind.Utc);
                var rental = new Rental(MembershipLevel.Plus, ToolTier.Tier3, TimeWindow.Weekend, start, end, early: true);

                Console.WriteLine($"🧾 Rental created:");
                Console.WriteLine($"   ID: {rental.Id}");
                Console.WriteLine($"   Member: {rental.Membership}");
                Console.WriteLine($"   Tier: {rental.Tier}");
                Console.WriteLine($"   Window: {rental.Window}");
                Console.WriteLine($"   Duration: {(end - start).TotalHours} hours");

                Section("State flow simulation");
                Console.WriteLine($"➡️ Initial state: {rental.State.GetType().Name}");
                rental.State.Activate(); Console.WriteLine($"✔️  Activated -> {rental.State.GetType().Name}");
                rental.State.Return(); Console.WriteLine($"✔️  Returned -> {rental.State.GetType().Name}");
                rental.State.Inspect(passed: true, clean: true); Console.WriteLine($"✔️  Inspected (passed=true, clean=true) -> {rental.State.GetType().Name}");
                rental.State.Close(); Console.WriteLine($"✔️  Closed -> {rental.State.GetType().Name}");

                // -----------------------------------
                // Pricing and credit calculation
                // -----------------------------------
                Section("Pricing calculation");
                var engine = new PricingEngine(pricing, pipeline, late, creditEngine);

                Console.WriteLine("🧮 Running QuoteAndApply() ...");
                var summary = engine.QuoteAndApply(rental, weekendDelivery: true, InsurancePlan.Premium, late: TimeSpan.FromHours(3));
                Console.WriteLine("✅ Calculation complete!\n");

                summary.Accept(new ConsoleSummaryVisitor());

                // Additional price breakdown
                Console.WriteLine("\n💰 Breakdown details:");
                Console.WriteLine($"   Base price      : {summary.BasePrice}");
                Console.WriteLine($"   Final price     : {summary.FinalPrice}");
                Console.WriteLine($"   Insurance       : {summary.InsuranceCost}");
                Console.WriteLine($"   Delivery        : {summary.DeliveryCost}");
                Console.WriteLine($"   Late fees       : {summary.LateFees}");
                Console.WriteLine($"   Earned credits  : {summary.EarnedCredits}");
                Console.WriteLine($"   Spent credits   : {summary.SpentCredits}");

                // -----------------------------------
                // Credit overview
                // -----------------------------------
                Section("Credit system");
                Console.WriteLine($"💳 Current balance: {creditEngine.Balance.Value}");
                if (creditEngine.Ledger.Count > 0)
                {
                    Console.WriteLine("📒 Ledger entries:");
                    foreach (var l in creditEngine.Ledger)
                        Console.WriteLine($"   • {l}");
                }
                else
                    Console.WriteLine("📒 Ledger empty — no credit operations executed.");

                // -----------------------------------
                // Diagnostics
                // -----------------------------------
                Section("Diagnostics & sanity checks");
                Console.WriteLine("Loaded late fee bands:");
                foreach (var x in lateFees.Take(5))
                    Console.WriteLine($"   {x.Band,-15} | perHour:{x.PerHourMap.Count,2} | factor:{x.FactorMap.Count,2}");

                Console.WriteLine("\n🏁 Program completed all demo steps successfully.");
            }
            catch (Exception ex)
            {
                Section("FATAL ERROR");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }
            finally
            {
                PauseExit();
            }
        }

        // ---------- helpers ----------
        static void Banner(string text)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(new string('=', 70));
            Console.WriteLine(text);
            Console.WriteLine(new string('=', 70));
            Console.ResetColor();
        }

        static void Section(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n--- {title} ---");
            Console.ResetColor();
        }

        static void Info(string m) => Console.WriteLine($"[info] {m}");
        static void Success(string m) => Console.WriteLine($"[ok]   {m}");
        static void Fail(string m) => Console.WriteLine($"[err]  {m}");
        static void PrintCount(string label, int count) => Console.WriteLine($"   {label,-20}: {count}");

        static void DumpModelDir(string modelPath)
        {
            Section("Model folder contents");
            var files = Directory.GetFiles(modelPath, "*.csv", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine("⚠️  No CSV files found in Model folder.");
                return;
            }
            foreach (var f in files.OrderBy(x => x))
                Console.WriteLine($"   • {Path.GetFileName(f)}");
        }

        static T TryLoad<T>(Func<T> loadFunc, string fileName)
        {
            try
            {
                var data = loadFunc();
                Success($"Loaded {fileName} ✅ ({(data as System.Collections.ICollection)?.Count ?? 0} rows)");
                return data;
            }
            catch (Exception ex)
            {
                Fail($"Error loading {fileName}: {ex.Message}");
                return default!;
            }
        }

        static void PauseExit()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Press Enter to exit...");
            Console.ResetColor();
            Console.ReadLine();
        }
    }
}
