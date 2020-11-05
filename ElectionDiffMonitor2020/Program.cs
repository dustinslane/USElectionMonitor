using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ElectionDiffMonitor2020.Model;
using Newtonsoft.Json;

namespace ElectionDiffMonitor2020
{
    internal class Program
    {
        private DateTime LastUpdate = DateTime.MinValue;
        
        private const string ConfigFileName = "Config.json";
        private static Program instance { get; set; }
        private WebClient _web { get; }
        private DataSummary _last { get; set; }
        private CandidateDefinition _candidates { get; }
        private Configuration Config { get; }
        
        private static Dictionary<string, Dictionary<int, int>> Count = new Dictionary<string, Dictionary<int, int>>();
        
        public Program(CancellationTokenSource src)
        {
            if (!File.Exists(ConfigFileName))
            {
                Config = new Configuration
                {
                    ManifestEndpoint =
                        new Uri(
                            "https://interactives.ap.org/elections/live-data/production/2020-11-03/president/metadata.json"),
                    ResultsEndpoint =
                        new Uri(
                            "https://interactives.ap.org/elections/live-data/production/2020-11-03/president/summary.json"),
                    ImportantStates = new List<string>
                    {
                        "PA",
                        "GA",
                        "AZ",
                        "NV",
                        "NC"
                    },
                    FilterPresidentLastNames = new List<string>()
                };
                
                File.WriteAllText(ConfigFileName, JsonConvert.SerializeObject(Config, Formatting.Indented));
            }
            else
            {
                Config = Configuration.FromJson(File.ReadAllText(ConfigFileName));
            }
            
            _web = new WebClient();
            _candidates = CandidateDefinition.FromJson(_web.DownloadString(Config.ManifestEndpoint));
            Console.CancelKeyPress += (sender, args) => { src.Cancel(); };
            Loop(src.Token).GetAwaiter().GetResult();
        }
        
        public static void Main(string[] args)
        {
            CancellationTokenSource src = new CancellationTokenSource();
            instance = new Program(src);
        }
        
        public async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (DateTime.UtcNow > LastUpdate + TimeSpan.FromSeconds(60))
                {
                    var dataSummary = await Update();

                    if (_last != null)
                    {
                        List<string> diffs = new List<string>();
                        int longestCandidateName = _candidates.Candidates.Values.Max(a => a.Last.Length);
                        int longestFilteredName = Config.FilterPresidentLastNames?.Any() ?? false ?  Config.FilterPresidentLastNames.Max(a => a.Length) : 0;
                        int candidateNameLength = Math.Max(longestCandidateName, longestFilteredName);
                        if (candidateNameLength % 2 != 0)
                        {
                            candidateNameLength += 1;
                        }
                        foreach (var stateResult in dataSummary.Results)
                        {
                            string state = stateResult.Key;
                            var stateCandidateResults = stateResult.Value[0].Summary.Results;

                            
                            
                            foreach (var cResult in stateCandidateResults)
                            {
                                string candidateId = cResult.CandidateId;
                                string candidateName = _candidates.Candidates.TryGetValue(candidateId, out var candidate)
                                    ? candidate.Last.Trim()
                                    : "unk";

                                if (Config.FilterPresidentLastNames != null && Config.FilterPresidentLastNames.Any() &&
                                    !Config.FilterPresidentLastNames.Contains(candidateName))
                                {
                                    // If specified names but this one ain't it skip
                                    continue;
                                }
                                
                                if (!_last.Results.TryGetValue(state, out var lastStateResult)) continue;
                                var nowResults = cResult;
                                var lastResults = lastStateResult[0].Summary.Results
                                    .FirstOrDefault(a => a.CandidateId == candidateId);

                                if (lastResults == null) continue;
                                long mutation = nowResults.VoteCount - lastResults.VoteCount;

                                if (mutation < 1) continue;
                                
                                string mutationString = mutation > 0 ? $"+ {mutation:N0}" : $"- {Math.Abs(mutation):N0}";
                                string formatted = string.Format("{0,-6}| {1, " + candidateNameLength + "} | {2,10} -> {3,10} |{4,16}",
                                    state,
                                    candidateName,
                                    $"{lastResults.VoteCount:N0}",
                                    $"{nowResults.VoteCount:N0}",
                                    $"({mutationString})");
                                diffs.Add(formatted);
                            }
                        }

                        if (diffs.Any())
                        {
                            Console.WriteLine();
                            int length = diffs[0].Length;
                            length = length % 2 == 0 ? length : length + 1;
                            
                            int numDashes = (length - 22) / 2;
                            
                            Console.WriteLine($"{new string('-', numDashes)}| {DateTime.Now:g} |{new string('-', numDashes)}");

                            bool definedImportantRegions = Config.ImportantStates != null && Config.ImportantStates.Any();
                            foreach (var line in diffs)
                            {
                                if (line.StartsWith("US"))
                                {
                                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                                } 
                                else if (Config.ImportantStates.Any(a => line.StartsWith(a)))
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                }
                                else
                                {
                                    Console.ForegroundColor = definedImportantRegions ? ConsoleColor.DarkGray : ConsoleColor.Gray;
                                }

                                Console.WriteLine($" {line}");
                                Console.ForegroundColor = ConsoleColor.Gray;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(" # 2020 AP Election Watcher by Dustin Slane");
                        Console.WriteLine(" # Change values in the config.json file to finetune");
                        Console.WriteLine();
                        Console.WriteLine(" # Started Monitoring");
                        
                    }
                    _last = dataSummary;
                }

                await Task.Delay(1000 * 60, token);
            }
        }

        public async Task<DataSummary> Update()
        {
            return DataSummary.FromJson(await _web.DownloadStringTaskAsync(Config.ResultsEndpoint));
        }
    }
}