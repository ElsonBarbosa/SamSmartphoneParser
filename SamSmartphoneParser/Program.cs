
using System;
using System.Configuration;
using System.Windows.Forms;

namespace SmartphoneParserReport
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string folderPath = ConfigurationManager.AppSettings["LogPath"];
            string apiURL = ConfigurationManager.AppSettings["APIURLBASE"];
            string connTest = ConfigurationManager.AppSettings["CHKCONN"];

            // Combina base URL + endpoint
            string apiUrl = $"{apiURL}{connTest}";

            if (!ApiService.CheckConnection(apiUrl))
            {
                MessageBox.Show(
                    "Falha ao conectar-se ao JEMSmm.",
                    "Erro de Conexão",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return; // encerra se não conectar
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"------------------ SMARTPHONE PARSER REPORT ------------------\n");
            Console.WriteLine($"------------ Monitorando a pasta de logs: {folderPath} ------------\n");
            Console.ResetColor();

            var monitor = new LogMonitor(folderPath, apiUrl);
            monitor.Start();
        }
    }
}
