using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BankoCheater
{
    /// <summary>
    /// Loader/gemmer Bankoplader til/fra en lokal JSON‑fil.
    /// </summary>
    internal static class JSON_Load_Save
    {
        private const string JsonSti = "plader.json";

        // DTO – kun de data, vi gemmer permanent
        private record BankoPladeDto(string ID, List<List<int>> Rækker);

        public static string CachePath => Path.GetFullPath(JsonSti);

        /// <summary>Hent alle plader fra JSON‑filen eller tom liste, hvis filen mangler.</summary>
        public static List<BankoPlade> LoadPlader()
        {
            if (!File.Exists(JsonSti))
                return new List<BankoPlade>();

            var json = File.ReadAllText(JsonSti);
            var dtos = JsonSerializer.Deserialize<List<BankoPladeDto>>(json)!;

            return dtos.Select(dto =>
            {
                var plade = new BankoPlade { ID = dto.ID };
                for (int r = 0; r < 3; r++)
                    foreach (int tal in dto.Rækker[r])
                        plade.ManglendeTal[r].Add(tal);
                return plade;
            }).ToList();
        }

        /// <summary>Gemmer alle plader til JSON‑filen (overskriver evt. eksisterende).</summary>
        public static void SavePlader(IEnumerable<BankoPlade> plader)
        {
            var dtos = plader.Select(p => new BankoPladeDto(
                p.ID,
                p.ManglendeTal.Select(set => set.ToList()).ToList()
            ));

            var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(JsonSti, json);
        }

        /// <summary>Sletter den gemte cache, så nye plader tvinges frem.</summary>
        public static void DeleteCache()
        {
            if (File.Exists(JsonSti))
                File.Delete(JsonSti);
        }
    }
}