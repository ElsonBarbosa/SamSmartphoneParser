using System;
using System.Collections.Generic;
using System.IO;

namespace SmartphoneParserReport
{
    public static class LogProcessor
    {
        private static string GetValue(string line)
        {
            int index = line.IndexOf(':');
            return index >= 0 ? line.Substring(index + 1).Trim() : "N/A";
        }

        private static string ReadEquipmentFromIni()
        {
            string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

            if (!File.Exists(configFile))
                throw new FileNotFoundException("Arquivo config.ini não encontrado.");

            foreach (var line in File.ReadAllLines(configFile))
            {
                if (line.StartsWith("Equipment"))
                {
                    return line.Split('=')[1].Trim();
                }
            }

            throw new InvalidOperationException("Chave 'Equipment' não encontrada no config.ini.");
        }

        public static void ExtractInfo(List<string> lines, string fileName)
        {
            string jig = "N/A";
            string result = "N/A";
            string failure = "N/A";
            string serialNumber = "N/A";

            // 🔹 pega direto do config.ini
            string equipPrefix = ReadEquipmentFromIni();

            // step = primeiros 2 chars do prefixo
            string step = equipPrefix.Length >= 2 ? equipPrefix.Substring(0, 2) : "XX";

            foreach (var line in lines)
            {
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("JIG"))
                    jig = GetValue(trimmed);
                else if (trimmed.StartsWith("RESULT"))
                    result = GetValue(trimmed);
                else if (trimmed.StartsWith("FAILITEM"))
                    failure = GetValue(trimmed);
                else if (trimmed.StartsWith("P/N"))
                    serialNumber = GetValue(trimmed);
            }

            // --- Define equipamento final ---
            string equipamento;
            if (equipPrefix.Contains("DL"))
            {
                // DL → concatena JIG
                if (int.TryParse(jig, out int jigNumber))
                    equipamento = $"{equipPrefix}{jigNumber:00}";
                else
                    equipamento = $"{equipPrefix}XX"; // fallback se JIG não for numérico
            }
            else
            {
                // Outros → usa direto
                equipamento = equipPrefix;
            }

            var api = new ApiService();

            // --- Caminho da pasta do dia ---
            string basePath = @"C:\JABIL\LOG\LogsProcessed";
            string dateFolder = DateTime.Now.ToString("dd-MM");
            string fullPath = Path.Combine(basePath, dateFolder);

            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            // --- Limpa pastas antigas ---
            foreach (var dir in Directory.GetDirectories(basePath))
            {
                string folderName = Path.GetFileName(dir);
                if (folderName != dateFolder)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Pasta antiga deletada: {folderName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao deletar pasta {folderName}: {ex.Message}");
                    }
                }
            }

            // --- Sufixo de arquivo ---
            string fileSuffix = result == "FAIL" ? "_FAIL" : result == "PASS" ? "_PASS" : "";
            string logFileName = Path.Combine(fullPath, $"{serialNumber}{fileSuffix}.txt");

            // Evita processar PASS duplicado
            if (result == "PASS" && File.Exists(logFileName))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERIAL: {serialNumber} já foi processado. Ignorando...");
                return;
            }

            // --- Conteúdo dev-friendly ---
            string content =
                $"SerialNumber: {serialNumber}{Environment.NewLine}" +
                $"Equipment: {equipamento}{Environment.NewLine}" +
                $"Result: {result}{Environment.NewLine}" +
                $"Failure: {failure}{Environment.NewLine}" +
                "------------------------" + Environment.NewLine;

            File.AppendAllText(logFileName, content);

            // --- Processa log via RawLogProcessor ---
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processando raw log de {serialNumber}...");
            RawLogProcessor.ProcessRawLog(logFileName, step);
        }
    }
}
