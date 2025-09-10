using System;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SmartphoneParserReport
{
    internal class Program
    {
        static void Main(string[] args)
        {
            RunMonitorWithConfig();
        }

        static void RunMonitorWithConfig()
        {
            string equipment = ConfigManager.GetOrCreateConfig();
            string folderPath = ConfigurationManager.AppSettings["LogPath"];
            string apiURL = ConfigurationManager.AppSettings["APIURLBASE"];
            string connTest = ConfigurationManager.AppSettings["CHKCONN"];
            string apiUrl = $"{apiURL}{connTest}";

            if (!ApiService.CheckConnection(apiUrl))
            {
                MessageBox.Show(
                    "Falha ao conectar-se ao JEMSmm.",
                    "Erro de Conexão",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"------------------ SMARTPHONE PARSER REPORT ------------------\n");
            Console.WriteLine($"------------ Monitorando a pasta de logs: {folderPath} ------------\n");
            Console.ResetColor();

            var monitor = new LogMonitor(folderPath, apiUrl);

            // Rodar o monitor em uma thread separada
            Thread monitorThread = new Thread(monitor.Start) { IsBackground = true };
            monitorThread.Start();

            // Loop para escutar comandos do console
            while (true)
            {
                string command = Console.ReadLine()?.Trim().ToUpper();
                if (command == "RESET")
                {
                    // Apaga o config.ini
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                    if (File.Exists(configPath))
                        File.Delete(configPath);

                    Console.WriteLine("Configuração apagada. Reconfigurando...");

                    // Recria a configuração
                    equipment = ConfigManager.GetOrCreateConfig();

                    Console.WriteLine("Configuração concluída. Monitoramento continua...");
                }
            }
        }
    }
}
