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
        // Hvor mange plader der hentes ved første kørsel (cache bygges).
        private const int AntalPlader = 100_000;

        static void Main(string[] args)
        {

            // 2) Indlæs plader – først lokal fil, ellers scrape

            List<BankoPlade> plader = JSON_Load_Save.LoadPlader();

            if (plader.Count == 0)
            {
                Console.WriteLine("Ingen lokale plader fundet – henter fra web …");
                plader = HentBankoPlader(AntalPlader);          // ← Selenium-metoden
                JSON_Load_Save.SavePlader(plader);
                Console.WriteLine($"Plader gemt i {JSON_Load_Save.CachePath}");
            }
            else
            {
                Console.WriteLine($"Indlæst {plader.Count} plader fra {JSON_Load_Save.CachePath}");
            }

            // 3) Klar til selve spillet
            Console.WriteLine("\n Bankoplader klar. Skriv et tal (1-90) eller 'q' for at afslutte.\n");

            var kaldteTal = new HashSet<int>();   // holder styr på de tal der allerede er råbt

            while (true)
            {
                Console.Write("Råbt tal: ");
                string? input = Console.ReadLine();

                if (input is null) continue;
                if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) break;

                // Gyldighedstjek 1-90 tjek
                if (!int.TryParse(input, out int tal) || tal < 1 || tal > 90)
                {
                    Console.WriteLine("  Ugyldigt tal. Prøv igen.\n");
                    continue;
                }

                // Har vi set tallet før?
                if (!kaldteTal.Add(tal))
                {
                    Console.WriteLine(" Tallet er allerede råbt.\n");
                    continue;
                }

                // Gå alle plader igennem og marker tallet
                foreach (BankoPlade plade in plader)
                {
                    // tre rækker på hver plade
                    for (int r = 0; r < 3; r++)
                    {
                        // Hvis tallet findes i rækken ⇒ fjern det
                        if (plade.ManglendeTal[r].Remove(tal))
                        {
                            // ---------------- BANKO på én række? ----------------
                            if (!plade.RækkeErFærdig[r] && plade.ManglendeTal[r].Count == 0)
                            {
                                plade.RækkeErFærdig[r] = true;
                                Console.WriteLine($" BANKO på 1 række – plade {plade.ID}");
                            }

                            // ------------------- FULD PLADE? -------------------
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

        // Henter bankoplader fra websitet (bruges kun første gang – eller når cachen fornyes med --refresh).

        private static List<BankoPlade> HentBankoPlader(int antal)
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");      // kør headless for hastighed

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl("https://mercantech.github.io/Banko/");

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            var plader = new List<BankoPlade>();

            // Vent til siden er klar – tekstboksen dukker op når alt er loaded
            wait.Until(d => d.FindElement(By.Id("tekstboks")).Displayed);

            for (int p = 1; p <= antal; p++)
            {
                string navn = $"plade_{p}_{DateTime.Now.Ticks}";

                // 1) Indtast navn → 2) klik "Generer plader"
                var tekstboks = driver.FindElement(By.Id("tekstboks"));
                tekstboks.Clear();
                tekstboks.SendKeys(navn);
                driver.FindElement(By.Id("knap")).Click();

                // Siden nulstiller – vent til første celle (p111) er synlig igen
                wait.Until(d =>
                {
                    try { return d.FindElement(By.Id("p111")).Displayed; }
                    catch { return false; }
                });

                // ----------- Scrape selve pladen -----------
                var plade = new BankoPlade { ID = navn };

                for (int r = 1; r <= 3; r++)
                {
                    for (int c = 1; c <= 9; c++)
                    {
                        string cellId = $"p1{r}{c}";
                        string cellText = driver.FindElement(By.Id(cellId)).Text;

                        // Tomme felter ignoreres – kun tal gemmes
                        if (int.TryParse(cellText, out int tal))
                            plade.ManglendeTal[r - 1].Add(tal);
                    }
                }

                plader.Add(plade);
            }

            driver.Quit();
            return plader;
        }
    }

    //  Model for en bankoplade

    public class BankoPlade
    {
        public string ID { get; set; } = string.Empty;

        // Tre rækker – hvert HashSet indeholder de tal, der mangler.
        public List<HashSet<int>> ManglendeTal { get; } = new()
        {
            new HashSet<int>(), // række 1
            new HashSet<int>(), // række 2
            new HashSet<int>()  // række 3
        };

        // Hjælpetilstande til status
        public bool[] RækkeErFærdig { get; } = { false, false, false };

        // True hvis ALLE tre rækker er færdige
        public bool PladenErFærdig => RækkeErFærdig.All(x => x);

        // Sikrer at vi kun råber "fuld plade" én gang
        public bool FuldPladeRåbt { get; set; }
    }
}
