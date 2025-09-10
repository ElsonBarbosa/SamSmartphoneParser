using System;
using System.IO;

namespace SmartphoneParserReport
{
    public static class RawLogProcessor
    {
        public static void ProcessRawLog(string filePath, string step)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Arquivo {filePath} não existe.");
                return;
            }

            var lines = File.ReadAllLines(filePath);
            string serialNumber = "N/A";
            string equipamento = "N/A";
            string result = "N/A";
            string failure = "N/A";



            foreach (var line in lines)
            {
                if (line.StartsWith("SerialNumber:"))
                    serialNumber = line.Substring("SerialNumber:".Length).Trim();
                else if (line.StartsWith("Equipment:"))
                    equipamento = line.Substring("Equipment:".Length).Trim();
                else if (line.StartsWith("Result:"))
                    result = line.Substring("Result:".Length).Trim();
                else if (line.StartsWith("Failure:"))
                    failure = line.Substring("Failure:".Length).Trim();
            }



            var api = new ApiService();

            // Aqui você pode fazer qualquer coisa com os dados
            if (result.ToUpper() == "FAIL")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[RAW LOG] SERIAL: {serialNumber} | RESULT: {result} | FAILURE: {failure} | EQUIP: {equipamento}");
                
                _ = api.SendFailureSerialNumberFVT(serialNumber, equipamento, result, failure, step);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[RAW LOG] SERIAL: {serialNumber} | RESULT: {result} | EQUIP: {equipamento}");
                _ = api.GetOkToStartAsync(serialNumber, equipamento, result, failure, filePath);
            }
            Console.ResetColor();

        }
    }
}
