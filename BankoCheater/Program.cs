using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace BankoCheater
{
    internal class Program
    {
        // Hvor mange plader vil vi hente første gang?
        private const int AntalPlader = 100000;

        static void Main(string[] args)
        {
            // --------------------------------------------------
            // 1) Tjek om brugeren vil forny cachefilen
            // --------------------------------------------------
            bool refresh = args.Any(a => a.Equals("--refresh", StringComparison.OrdinalIgnoreCase));
            if (refresh)
                JSON_Load_Save.DeleteCache();

            // --------------------------------------------------
            // 2) Forsøg at indlæse plader fra lokal JSON
            //    – ellers hent fra web og gem
            // --------------------------------------------------
            List<BankoPlade> plader = JSON_Load_Save.LoadPlader();

            if (plader.Count == 0)
            {
                Console.WriteLine("Ingen lokale plader fundet – henter fra web …");
                plader = HentBankoPlader(AntalPlader);          // ← Selenium‑metoden
                JSON_Load_Save.SavePlader(plader);
                Console.WriteLine($"Plader gemt i {JSON_Load_Save.CachePath}");
            }
            else
            {
                Console.WriteLine($"Indlæst {plader.Count} plader fra {JSON_Load_Save.CachePath}");
            }

            // --------------------------------------------------
            // 3) Klar til spillet
            // --------------------------------------------------
            Console.WriteLine("\n Bankoplader klar. Skriv et tal (1‑90) eller 'q' for at afslutte.\n");

            var kaldteTal = new HashSet<int>();

            while (true)
            {
                Console.Write("Råbt tal: ");
                string? input = Console.ReadLine();

                if (input is null) continue;
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) break;

                if (!int.TryParse(input, out int tal) || tal < 1 || tal > 90)
                {
                    Console.WriteLine("  Ugyldigt tal. Prøv igen.\n");
                    continue;
                }

                if (!kaldteTal.Add(tal))
                {
                    Console.WriteLine(" Tallet er allerede råbt.\n");
                    continue;
                }

                bool nogenHarTallet = false;

                foreach (BankoPlade plade in plader)
                {
                    for (int r = 0; r < 3; r++)
                    {
                        if (plade.ManglendeTal[r].Remove(tal))
                        {
                            // BANKO på én række?
                            if (!plade.RækkeErFærdig[r] && plade.ManglendeTal[r].Count == 0)
                            {
                                plade.RækkeErFærdig[r] = true;
                                Console.WriteLine($" BANKO på 1 række – plade {plade.ID}");
                            }

                            // Fuld plade?
                            if (plade.PladenErFærdig && !plade.FuldPladeRåbt)
                            {
                                plade.FuldPladeRåbt = true;
                                Console.WriteLine($" FULD PLADE til {plade.ID}!");
                            }
                        }
                    }
                }              

            }

            Console.WriteLine(" Program afsluttet.");
        }

        // --------------------------------------------------
        // Henter bankoplader fra websitet (kun brugt første gang)
        // --------------------------------------------------
        private static List<BankoPlade> HentBankoPlader(int antal)
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new"); // hurtigere, nyere headless‑tilstand

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl("https://mercantech.github.io/Banko/");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var plader = new List<BankoPlade>();

            // Sikre at siden er klar
            wait.Until(d => d.FindElement(By.Id("tekstboks")).Displayed);

            for (int p = 1; p <= antal; p++)
            {
                string navn = $"plade_{p}_{DateTime.Now.Ticks}";

                // Skriv navn → klik "Generer plader"
                var tekstboks = driver.FindElement(By.Id("tekstboks"));
                tekstboks.Clear();
                tekstboks.SendKeys(navn);
                driver.FindElement(By.Id("knap")).Click();

                // Vent på at P111 er synlig (siden nulstiller)
                wait.Until(d =>
                {
                    try { return d.FindElement(By.Id("p111")).Displayed; }
                    catch { return false; }
                });

                // ----------- Scrap pladen -----------
                var plade = new BankoPlade { ID = navn };

                for (int r = 1; r <= 3; r++)
                {
                    for (int c = 1; c <= 9; c++)
                    {
                        string cellId = $"p1{r}{c}";
                        var cellText = driver.FindElement(By.Id(cellId)).Text;

                        if (int.TryParse(cellText, out int tal))
                            plade.ManglendeTal[r - 1].Add(tal);  // spring blanke felter over
                    }
                }

                plader.Add(plade);
            }

            driver.Quit();
            return plader;
        }
    }

    // --------------------------------------------------
    //  Model for en bankoplade
    // --------------------------------------------------
    public class BankoPlade
    {
        public string ID { get; set; } = string.Empty;

        // tre rækker – hvert HashSet indeholder tallene, der stadig mangler
        public List<HashSet<int>> ManglendeTal { get; } = new()
        {
            new HashSet<int>(), // række 1
            new HashSet<int>(), // række 2
            new HashSet<int>()  // række 3
        };

        // hjælpetilstand
        public bool[] RækkeErFærdig { get; } = { false, false, false };
        public bool PladenErFærdig => RækkeErFærdig.All(x => x);
        public bool FuldPladeRåbt { get; set; }
    }
}