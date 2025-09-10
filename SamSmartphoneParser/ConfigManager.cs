using System;
using System.IO;

namespace SmartphoneParserReport
{
    internal static class ConfigManager
    {
        private static readonly string RootPath = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string ConfigFile = Path.Combine(RootPath, "config.ini");

        public static string GetOrCreateConfig()
        {
            if (!File.Exists(ConfigFile))
            {
                CreateConfig();
            }

            // 🔹 Lê o conteúdo do arquivo e retorna o Equipment
            foreach (var line in File.ReadAllLines(ConfigFile))
            {
                if (line.StartsWith("Equipment"))
                {
                    return line.Split('=')[1].Trim();
                }
            }

            return string.Empty;
        }

        private static void CreateConfig()
        {
            Console.WriteLine("Configuração inicial necessária.");
            Console.WriteLine("Escolha a estação: ");
            Console.WriteLine("1 - DL");
            Console.WriteLine("2 - BT");
            Console.WriteLine("3 - FT");
            //Console.WriteLine("4 - FNI");

            string stationChoice = Console.ReadLine();
            string station = "";

            switch (stationChoice)
            {
                case "1":
                    station = "DL";
                    break;
                case "2":
                    station = "BT";
                    break;
                case "3":
                    station = "FT";
                    break;
                //case "4":
                //    station = "FNI";
                //    break;
                default:
                    Console.WriteLine("Opção inválida. Saindo...");
                    Environment.Exit(0);
                    break;
            }

            string equipment;
            if (station == "DL")
            {
                Console.Write("Digite o número do PC (1 - 20): ");
                int pcNumber = int.Parse(Console.ReadLine());
                equipment = $"SMART01-{station}-{pcNumber:00}-"; // hífen no final
            }
            else
            {
                Console.Write("Digite o número do equipamento (1 - 40): ");
                int eqNumber = int.Parse(Console.ReadLine());
                equipment = $"SMART01-{station}-{eqNumber:00}";
            }

            using (StreamWriter sw = new StreamWriter(ConfigFile))
            {
                sw.WriteLine("[TEST PRESETS]");
                sw.WriteLine($"Equipment = {equipment}");
            }

            Console.WriteLine($"Configuração salva em {ConfigFile}");
        }
    }
}
