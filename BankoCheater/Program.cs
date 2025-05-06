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
        // Hvor mange plader vil vi hente?
        private const int AntalPlader = 200;

        static void Main(string[] args)
        {
            var plader = HentBankoPlader(AntalPlader);

            Console.WriteLine("\n Bankoplader hentet. Klar til at råbe tal!");
            Console.WriteLine("Skriv et tal (1-90) eller 'q' for at afslutte.\n");

            var kaldteTal = new HashSet<int>();

            while (true)
            {
                Console.Write("Råbt tal: ");
                string? input = Console.ReadLine();

                if (input is null) continue;
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) break;

                if (!int.TryParse(input, out int tal) || tal is < 1 or > 90)
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

                foreach (var plade in plader)
                {
                    for (int r = 0; r < 3; r++)
                    {
                        // HashSet.Remove returnerer true, hvis tallet var i rækken
                        if (plade.ManglendeTal[r].Remove(tal))
                        {
                            nogenHarTallet = true;
                            Console.WriteLine($" Plade '{plade.ID}' har {tal} i række {r + 1}");

                            // 1-række-banko?
                            if (!plade.RækkeErFærdig[r] && plade.ManglendeTal[r].Count == 0)
                            {
                                plade.RækkeErFærdig[r] = true;
                                Console.WriteLine($" BANKO på 1 række – plade {plade.ID}");
                            }

                            // fuld plade?
                            if (plade.PladenErFærdig && !plade.FuldPladeRåbt)
                            {
                                plade.FuldPladeRåbt = true;
                                Console.WriteLine($" FULD PLADE til {plade.ID}!");
                            }
                        }
                    }
                }

                if (!nogenHarTallet)
                    Console.WriteLine(" Ingen plader har dette tal.");

                Console.WriteLine();
            }

            Console.WriteLine(" Program afsluttet.");
        }

        // --------------------------------------------------
        // Henter bankoplader fra websitet
        // --------------------------------------------------
        private static List<BankoPlade> HentBankoPlader(int antal)
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new"); // hurtigere, nyere headless-tilstand

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl("https://mercantech.github.io/Banko/");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var plader = new List<BankoPlade>();

            // Sikre at siden er klar
            wait.Until(d => d.FindElement(By.Id("tekstboks")).Displayed);

            // Gem første værdi af p111 (den plade der ligger dér fra starten)
            string sidsteP111 = driver.FindElement(By.Id("p111")).Text;

            for (int p = 1; p <= antal; p++)
            {
                string navn = $"plade_{p}_{DateTime.Now.Ticks}";

                // Skriv navn → klik "Generer plader"
                var tekstboks = driver.FindElement(By.Id("tekstboks"));
                tekstboks.Clear();
                tekstboks.SendKeys(navn);
                driver.FindElement(By.Id("knap")).Click();

                // Vent på at P111 ændrer sig (garanterer NYE tal)
                wait.Until(d =>
                {
                    try
                    {
                        return d.FindElement(By.Id("p111")).Displayed;
                    }
                    catch { return false; }
                });

                // Opdater reference-teksten til næste runde
                sidsteP111 = driver.FindElement(By.Id("p111")).Text;

                // ---------- Scrap pladen ----------
                var plade = new BankoPlade { ID = navn };

                // VIGTIGT: id-prefikset er ALTID p1rc – websiden nulstiller hver gang
                for (int r = 1; r <= 3; r++)
                {
                    for (int c = 1; c <= 9; c++)
                    {
                        string cellId = $"p1{r}{c}";
                        var cellText = driver.FindElement(By.Id(cellId)).Text;

                        if (int.TryParse(cellText, out int tal))
                            plade.ManglendeTal[r - 1].Add(tal); // spring blanke felter over
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