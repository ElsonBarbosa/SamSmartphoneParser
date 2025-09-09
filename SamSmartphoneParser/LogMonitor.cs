using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SmartphoneParserReport
{
    public class LogMonitor
    {
        private readonly string folderPath;
        private readonly string apiUrl;

        private readonly object fileStatesLock = new object();
        private readonly Dictionary<string, FileState> fileStates = new Dictionary<string, FileState>();

        public LogMonitor(string folderPath, string apiUrl)
        {
            this.folderPath = folderPath;
            this.apiUrl = apiUrl;
        }

        public void Start()
        {
            //CheckAPIConnection(apiUrl);

            var watcher = new FileSystemWatcher(folderPath, "*.csv");
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.EnableRaisingEvents = true;

            while (true)
            {
                try
                {
                    List<string> filesToRead;

                    lock (fileStatesLock)
                        filesToRead = new List<string>(fileStates.Keys);

                    foreach (var file in filesToRead)
                    {
                        if (File.Exists(file))
                            ReadNewLines(file);
                    }

                    ProcessStalledBuffers();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erro ao ler arquivos: " + ex.Message);
                }

                Thread.Sleep(1000);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath;

            lock (fileStatesLock)
            {
                if (!fileStates.ContainsKey(file))
                {
                    var state = new FileState { FileName = Path.GetFileName(file) };

                    state.LastPosition = 0; // lê desde o início

                    fileStates[file] = state;

                    Console.WriteLine($"Arquivo adicionado ao monitoramento: {state.FileName}");
                    Console.WriteLine("\n------------------------------------------------------------\n");
                }
            }
        }

        private void ReadNewLines(string file)
        {
            FileState state;
            lock (fileStatesLock)
                state = fileStates[file];

            long lastPos = state.LastPosition;

            try
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < lastPos)
                        lastPos = 0; // arquivo truncado

                    fs.Seek(lastPos, SeekOrigin.Begin);

                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                            ProcessLine(state, line);

                        lastPos = fs.Position;
                    }
                }

                lock (fileStatesLock)
                {
                    state.LastPosition = lastPos;
                    state.LastUpdate = DateTime.Now;
                }
            }
            catch (IOException)
            {
                // arquivo em uso, ignora
            }
        }

        private void ProcessLine(FileState state, string line)
        {
            lock (state)
            {
                if (line.StartsWith("#INIT"))
                {
                    state.Buffer.Clear();
                    state.Buffer.Add(line);
                }
                else if (state.Buffer.Count > 0)
                {
                    state.Buffer.Add(line);

                    if (line.StartsWith("RESULT"))
                    {
                        LogProcessor.ExtractInfo(state.Buffer, state.FileName);
                        state.Buffer.Clear();
                    }
                }
            }
        }

        private void ProcessStalledBuffers()
        {
            List<FileState> stalledStates = new List<FileState>();

            lock (fileStatesLock)
            {
                var now = DateTime.Now;
                foreach (var kvp in fileStates)
                {
                    var state = kvp.Value;
                    lock (state)
                    {
                        if (state.Buffer.Count > 0 && (now - state.LastUpdate).TotalSeconds > 5)
                            stalledStates.Add(state);
                    }
                }
            }

            foreach (var state in stalledStates)
            {
                lock (state)
                {
                    LogProcessor.ExtractInfo(state.Buffer, state.FileName);
                    state.Buffer.Clear();
                }
            }
        }

        private void CheckAPIConnection(string apiUrl)
        {
            // mantém igual ao seu (pode extrair depois para ApiService se quiser)
        }
    }
}
