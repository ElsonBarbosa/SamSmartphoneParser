using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;

public class ApiService
{
    private readonly HttpClient _httpClient;
    public static string _endpointUrl = ConfigurationManager.AppSettings["APIURLBASE"];



    private static string _cachedToken = null;

    private static string cachedToken = null;

    public ApiService()
    {
        _httpClient = new HttpClient();
    }

    public static async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken))
            return _cachedToken; // retorna token em cache

        var url = $"{_endpointUrl}api-external-api/api/user/adsignin";

        var handler = new HttpClientHandler
        {
            // Ignora erros de certificado
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using (var client = new HttpClient(handler))
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(@"jabil\svchua_jesmapistg"), "name");
            form.Add(new StringContent("qKzla3oBDA51Ecq=+B2_z"), "password");

            try
            {
                var response = await client.PostAsync(url, form);
                response.EnsureSuccessStatusCode();

                // Retorna exatamente o corpo da resposta, cru
                string token = await response.Content.ReadAsStringAsync();

                _cachedToken = token; // salva em cache
                return token;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao obter token: " + ex.Message);
                return string.Empty;
            }
        }
    }

    public async Task<string> GetOkToStartAsync(string serialNumber, string equipamento, string result, string failure, string filePath)
    {
        // Se não houver token em cache, pega um novo
        if (string.IsNullOrEmpty(cachedToken))
        {
            cachedToken = await GetTokenAsync();
        }

        async Task<string> MakeRequest(string token)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            var cookieContainer = new CookieContainer();
            handler.CookieContainer = cookieContainer;

            var split = token.Split(new char[] { '=' }, 2);
            var cookieName = split[0].Trim();
            var cookieValue = split.Length > 1 ? split[1].Trim(';') : "";

            cookieContainer.Add(new Uri($"{_endpointUrl}"), new Cookie(cookieName, cookieValue));

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                string url = $"{_endpointUrl}api-external-api/api/wips/oktostart?serialNumber={serialNumber}&resourceName={equipamento}";

                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    string body = await response.Content.ReadAsStringAsync();

                    if ((int)response.StatusCode == 500)
                    {
                        // Token expirado, pega novo token e tenta novamente
                        cachedToken = await GetTokenAsync();
                        return await MakeRequest(cachedToken);
                    }

                    if ((int)response.StatusCode == 200)
                    {
                        int wipId = GetWipID(serialNumber);

                        if (wipId != 0) // se encontrado
                        {
                            // Chama SendSerialNumberFVTSync de forma assíncrona sem bloquear
                            Task.Run(() => SendSerialNumberFVTSync(wipId, serialNumber, equipamento, result, failure));
                        }

                        // Retorna o WipId convertido para string
                        return wipId.ToString();
                    }

                    if ((int)response.StatusCode == 400)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Etapa do número de série {serialNumber} incorreta: Verificar o histórico no MES!");
                        Console.ResetColor();

                        // Deleta o arquivo de log correspondente ao serial
                        //string filePath = $"{serialNumber}.txt";
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                                Console.WriteLine($"Arquivo {filePath} removido para reteste.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao deletar o arquivo {filePath}: {ex.Message}");
                            }
                        }


                        return "Falha de fila";
                    }

                    return body;
                }
                catch (Exception ex)
                {
                    return $"Erro ao fazer requisição: {ex.Message}";
                }
            }
        }

        return await MakeRequest(cachedToken);
    }


    public int GetWipID(string serialNumber)
    {
        if (string.IsNullOrEmpty(cachedToken))
            cachedToken = GetTokenAsync().GetAwaiter().GetResult();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        var cookieContainer = new CookieContainer();
        handler.CookieContainer = cookieContainer;

        var split = cachedToken.Split(new char[] { '=' }, 2);
        var cookieName = split[0].Trim();
        var cookieValue = split.Length > 1 ? split[1].Trim(';') : "";
        cookieContainer.Add(new Uri("https://man-prd.jemsms.corp.jabil.org"), new Cookie(cookieName, cookieValue));

        using (var client = new HttpClient(handler))
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            string url = $"{_endpointUrl}api-external-api/api/Wips/getWipInformationBySerialNumber?SiteName=MANAUS&CustomerName=SAMSUNG&SerialNumber={serialNumber}";

            try
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var array = JArray.Parse(body);
                    if (array.Count > 0)
                    {
                        return (int)array[0]["WipId"];
                    }
                }

                //return -1; // não achou ou erro
            }
            catch
            {
                //return -1;
            }
        }

        return -1;
    }

    public string SendSerialNumberFVTSync(int wipId, string serialNumber, string equipamento, string result, string failure)
    {
        // Pega token do cache
        string token = GetTokenAsync().GetAwaiter().GetResult();

        // --- Primeira requisição: start ---
        string urlStart = $"{_endpointUrl}api-external-api/api/Wips/{wipId}/start";
        string jsonStart = $@"
        {{
            ""wipId"": {wipId},
            ""resourceName"": ""{equipamento}"",
            ""isSingleWipMode"": true
        }}";

        var contentStart = new StringContent(jsonStart, Encoding.UTF8, "application/json");

        var split = token.Split(new char[] { '=' }, 2);
        var cookieName = split[0].Trim();
        var cookieValue = split.Length > 1 ? split[1].Trim(';') : "";

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        var cookieContainer = new CookieContainer();
        handler.CookieContainer = cookieContainer;
        cookieContainer.Add(new Uri("https://man-prd.jemsms.corp.jabil.org"), new Cookie(cookieName, cookieValue));

        using (var client = new HttpClient(handler))
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            // POST start
            var responseStart = client.PostAsync(urlStart, contentStart).GetAwaiter().GetResult();

            if (responseStart.IsSuccessStatusCode)
            {
                // --- Segunda requisição: complete ---
                string urlComplete = $"{_endpointUrl}api-external-api/api/Wips/{wipId}/complete";
                string jsonComplete = $@"
            {{
                ""wipId"": {wipId},
                ""isSingleWipMode"": true
            }}";

                var contentComplete = new StringContent(jsonComplete, Encoding.UTF8, "application/json");
                var responseComplete = client.PostAsync(urlComplete, contentComplete).GetAwaiter().GetResult();

                if (responseComplete.IsSuccessStatusCode)
                {

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Serial APROVADO e enviado para o JEMSmm com sucesso!");
                    Console.ResetColor();
                    return "Serial APROVADO e enviado para o JEMSmm com sucesso!";
                }
                else
                {

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Serial REPROVADO e enviado para o JEMSmm com sucesso!");
                    Console.ResetColor();
                    return $"COMPLETE_FAILED: {responseComplete.StatusCode}";
                }
            }
            else
            {

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Etapa do número de série {serialNumber} incorreta: Verificar o histórico no MES!");
                Console.ResetColor();
                return "Falha de fila";
            }
        }
    }

    public string SendFailureSerialNumberFVT(string serialNumber, string equipamento, string result, string failure, string step)
    {
        string json;

        json = $@"
        {{
            ""Serial"": ""{serialNumber}"",
            ""Customer"": ""Samsung"",
            ""Division"": ""Samsung"",
            ""Equipment"": ""{equipamento}"",
            ""Step"": ""{step}"",
            ""TestResult"": ""F"",
            ""FailureLabel"": ""{failure}""
        }}";


        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using (var client = new HttpClient())
        {
            var response = client.PostAsync("http://10.56.17.58/Mes4Api/Test/SendTestMes", content).GetAwaiter().GetResult(); // síncrono
            string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();


            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Serial REPROVADO e enviado para o JEMSmm com sucesso!");
            Console.ResetColor();

            return "DEFEITO ENVIADO AO MES";
        }
    }



    public static bool CheckConnection(string apiUrl)
    {
        bool conectado = false;
        int tentativas = 2;

        Console.WriteLine("Iniciando conexão com o JEMSmm...");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using (HttpClient client = new HttpClient(handler))
        {
            while (!conectado && tentativas > 0)
            {
                try
                {
                    Console.WriteLine($"Tentativa de conexão... Tentativas restantes: {tentativas}");
                    HttpResponseMessage response = client.GetAsync(apiUrl).GetAwaiter().GetResult();

                    if (response.IsSuccessStatusCode)
                    {
                        conectado = true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Conectado ao JEMSmm!");
                        Console.ResetColor();
                    }
                    else
                    {
                        tentativas--;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Falha ao conectar-se. Status: {(int)response.StatusCode}. Tentativas restantes: {tentativas}");
                        Console.ResetColor();
                        System.Threading.Thread.Sleep(2000);
                    }
                }
                catch (Exception ex)
                {
                    tentativas--;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Erro ao conectar-se: {ex.Message}. Tentativas restantes: {tentativas}");
                    Console.ResetColor();
                    System.Threading.Thread.Sleep(2000);
                }
            }
        }

        return conectado;
    }









    public async Task SendSerialNumberAsync(string serialNumber, string equipamento, string result, string failure, string step)
    {
        string json;

        if (result == "FAIL")
        {

            json = $@"
        {{
            ""Serial"": ""{serialNumber}"",
            ""Customer"": ""Samsung"",
            ""Division"": ""Samsung"",
            ""Equipment"": ""{equipamento}"",
            ""Step"": ""{step}"",
            ""TestResult"": ""F"",
            ""FailureLabel"": ""{failure}""
        }}";
        }
        else
        {

            json = $@"
        {{
            ""Serial"": ""{serialNumber}"",
            ""Customer"": ""Samsung"",
            ""Division"": ""Samsung"",
            ""Equipment"": ""{equipamento}"",
            ""Step"": ""{step}"",
            ""TestResult"": ""P""
        }}";
        }

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_endpointUrl, content);
        string responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (result == "PASS")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Serial APROVADO e enviado para o JEMSmm com sucesso!");
            }
            else if (result == "FAIL")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Serial REPROVADO e enviado para o JEMSmm com sucesso!");
            }

            Console.ResetColor();
            Console.WriteLine("Resposta da API: " + responseContent);
        }
        else
        {
            if (responseContent.IndexOf("Wip start", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Etapa do número de série {serialNumber} incorreta: Verificar o histórico no MES!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Perda de comunicação com a API!");
                Console.ResetColor();
            }

            Console.WriteLine("Resposta da API: " + responseContent);
        }
        Console.WriteLine("\n------------------------------------------------------------\n");
    }

}