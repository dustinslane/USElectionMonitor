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
        private const string ConfigFileName = "Config.json";
        private static Program Instance { get; set; }
        private WebClient Web { get; }
        private DataSummary Last { get; set; }
        private CandidateDefinition Candidates { get; }
        private Configuration Config { get; }
        private Program(CancellationTokenSource src)
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
            
            Web = new WebClient();
            Candidates = CandidateDefinition.FromJson(Web.DownloadString(Config.ManifestEndpoint));
            Console.CancelKeyPress += (sender, args) => { src.Cancel(); };
            Loop(src.Token).GetAwaiter().GetResult();
        }
        
        public static void Main(string[] args)
        {
            CancellationTokenSource src = new CancellationTokenSource();
            Instance = new Program(src);
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var dataSummary = await Update();
                    if (Last != null)
                    {
                        List<string> diffs = new List<string>();
                        int longestCandidateName = Candidates.Candidates.Values.Max(a => a.Last.Length);
                        int longestFilteredName = Config.FilterPresidentLastNames?.Any() ?? false
                            ? Config.FilterPresidentLastNames.Max(a => a.Length)
                            : 0;
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
                                string candidateName =
                                    Candidates.Candidates.TryGetValue(candidateId, out var candidate)
                                        ? candidate.Last.Trim()
                                        : "unk";

                                if (Config.FilterPresidentLastNames != null && Config.FilterPresidentLastNames.Any() &&
                                    !Config.FilterPresidentLastNames.Contains(candidateName))
                                {
                                    // If specified names but this one ain't it skip
                                    continue;
                                }

                                if (!Last.Results.TryGetValue(state, out var lastStateResult)) continue;
                                var nowResults = cResult;
                                var lastResults = lastStateResult[0].Summary.Results
                                    .FirstOrDefault(a => a.CandidateId == candidateId);

                                if (lastResults == null) continue;
                                long mutation = nowResults.VoteCount - lastResults.VoteCount;

                                if (mutation < 1) continue;

                                string mutationString =
                                    mutation > 0 ? $"+ {mutation:N0}" : $"- {Math.Abs(mutation):N0}";
                                string formatted = string.Format(
                                    "{0,-6}| {1, " + candidateNameLength + "} | {2,10} -> {3,10} |{4,16}",
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

                            Console.WriteLine(
                                $"{new string('-', numDashes)}| {DateTime.Now:g} |{new string('-', numDashes)}");

                            bool definedImportantRegions =
                                Config.ImportantStates != null && Config.ImportantStates.Any();
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
                                    Console.ForegroundColor =
                                        definedImportantRegions ? ConsoleColor.DarkGray : ConsoleColor.Gray;
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

                    Last = dataSummary;
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error getting data: {e.Message}");
                    Console.ResetColor();
                }

                await Task.Delay(1000 * 60, token);
            }
        }

        private async Task<DataSummary> Update()
        {
            return DataSummary.FromJson(await Web.DownloadStringTaskAsync(Config.ResultsEndpoint));
        }
    }
}