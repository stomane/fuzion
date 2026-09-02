using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Fuzion.Extensions;
using Fuzion.Programs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Fuzion.AI
{
    internal static class GeminiGameClassifier
    {
        private const int MaxBatchSize = 75;
        private static readonly HttpClient httpClient = CreateHttpClient();

        private sealed class BatchCandidate
        {
            public int Index { get; set; }
            public Program Program { get; set; }
        }

        internal sealed class ClassificationResult
        {
            public Dictionary<string, string> Games { get; }

            // Programs Gemini actually finished classifying (successful batches only).
            // Programs missing from this set had no Gemini verdict - e.g. their batch's
            // API call failed - and callers should fall back to other checks for them
            // instead of treating "not in Games" as an authoritative "not a game".
            public HashSet<string> EvaluatedNames { get; }

            public ClassificationResult(Dictionary<string, string> games, HashSet<string> evaluatedNames)
            {
                Games = games;
                EvaluatedNames = evaluatedNames;
            }
        }

        public static ClassificationResult ClassifyGames(IEnumerable<Program> programs)
        {
            var games = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var evaluatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Classification now goes through the Fuzion backend, which resolves in the order
            // cache -> IGDB -> Gemini and caches every verdict, so what matters here is having
            // a backend URL rather than a Gemini key.
            if (string.IsNullOrWhiteSpace(Constants.BackendBaseUrl) || programs == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Classify] Skipping: BackendUrl='{Constants.BackendBaseUrl}', ProgramsNull={programs == null}");
                return new ClassificationResult(games, evaluatedNames);
            }

            var candidates = programs
                .Where(ShouldClassify)
                .Select((program, index) => new BatchCandidate { Index = index, Program = program })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[Gemini] Found {candidates.Count} candidates to classify out of {programs.Count()} total programs");

            if (candidates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[Gemini] No candidates to classify, returning empty result");
                return new ClassificationResult(games, evaluatedNames);
            }

            int batchNum = 0;
            foreach (var batch in Batch(candidates, MaxBatchSize))
            {
                batchNum++;
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Classify] Processing batch {batchNum} with {batch.Count} items");
                    var batchResult = ClassifyBatch(batch);
                    System.Diagnostics.Debug.WriteLine($"[Classify] Batch {batchNum}: {batchResult.Games.Count} games, {batchResult.Evaluated.Count} of {batch.Count} resolved");

                    // Only names the backend returned a verdict for count as evaluated. It
                    // reports anything it could not resolve - a failed Gemini batch, a
                    // rate-limited IGDB - separately, and those must fall through to the
                    // caller's other checks rather than being read as "not a game".
                    foreach (var evaluated in batchResult.Evaluated)
                    {
                        evaluatedNames.Add(evaluated);
                    }

                    foreach (var classification in batchResult.Games)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Classify] {classification.Key} => {classification.Value}");
                        games[classification.Key] = classification.Value;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Gemini] Batch {batchNum} failed: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Gemini] Inner exception: {ex.InnerException.Message}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Gemini] Classification complete: {games.Count} games identified, {evaluatedNames.Count} programs evaluated");
            return new ClassificationResult(games, evaluatedNames);
        }

        private static bool ShouldClassify(Program program)
        {
            if (program == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(program.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Skipping: null DisplayName");
                return false;
            }

            if (program.IsGame)
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Skipping {program.DisplayName}: already marked as game");
                return false;
            }

            if (program.DisplayName.IsFalsePositive())
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Skipping {program.DisplayName}: false positive");
                return false;
            }

            if (LocalDatabase.IsGame(program.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Skipping {program.DisplayName}: in local game database");
                return false;
            }

            if (LocalDatabase.IsProgram(program.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Skipping {program.DisplayName}: in local program database");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[Gemini.Filter] Including {program.DisplayName}: eligible for classification");
            return true;
        }

        private sealed class BatchClassification
        {
            public Dictionary<string, string> Games { get; }

            // Names the backend returned a verdict for, game or not. Anything it listed as
            // unresolved is deliberately absent so callers keep their own fallbacks.
            public HashSet<string> Evaluated { get; }

            public BatchClassification(Dictionary<string, string> games, HashSet<string> evaluated)
            {
                Games = games;
                Evaluated = evaluated;
            }
        }

        /// <summary>
        /// Asks the Fuzion backend to classify a batch. The backend answers from its shared
        /// verdict cache first, then IGDB, then Gemini, and caches whatever it learns - so a
        /// second run over the same library resolves without reaching either service.
        /// </summary>
        private static BatchClassification ClassifyBatch(List<BatchCandidate> batch)
        {
            var games = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var evaluated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var items = new JArray();
            foreach (var item in batch)
            {
                items.Add(new JObject
                {
                    ["detectedName"] = item.Program.DisplayName,
                    ["publisher"] = string.IsNullOrWhiteSpace(item.Program.Publisher) ? "unknown" : item.Program.Publisher,
                    ["launcher"] = item.Program.Launcher.ToString(),
                    ["exeName"] = string.IsNullOrWhiteSpace(item.Program.ExeName) ? "unknown" : item.Program.ExeName
                });
            }

            var requestBody = new JObject { ["items"] = items };
            string url = Constants.BackendBaseUrl + "/classify/programs";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = httpClient.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult())
                {
                    string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"Classification request failed: {(int)response.StatusCode} {response.ReasonPhrase} {responseContent}");
                    }

                    JObject parsed = JObject.Parse(responseContent);

                    JObject stats = parsed["stats"] as JObject;
                    if (stats != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Classify] backend stats: cache={stats["fromCache"]} igdb={stats["fromIgdb"]} gemini={stats["fromGemini"]} unresolved={stats["unresolved"]}");
                    }

                    JArray results = parsed["results"] as JArray;
                    if (results == null)
                    {
                        throw new InvalidOperationException("Classification response contained no results array.");
                    }

                    foreach (JObject entry in results.OfType<JObject>())
                    {
                        string detectedName = entry["detectedName"]?.Value<string>()?.Trim();
                        if (string.IsNullOrWhiteSpace(detectedName))
                        {
                            continue;
                        }

                        evaluated.Add(detectedName);

                        bool isGame = entry["isGame"]?.Value<bool>() ?? false;
                        if (!isGame)
                        {
                            continue;
                        }

                        string canonicalTitle = entry["canonicalTitle"]?.Value<string>()?.Trim();
                        games[detectedName] = string.IsNullOrWhiteSpace(canonicalTitle) ? detectedName : canonicalTitle;
                    }

                    return new BatchClassification(games, evaluated);
                }
            }
        }

        private static IEnumerable<List<BatchCandidate>> Batch(List<BatchCandidate> items, int batchSize)
        {
            for (int i = 0; i < items.Count; i += batchSize)
            {
                yield return items.Skip(i).Take(batchSize).ToList();
            }
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
        }
    }
}