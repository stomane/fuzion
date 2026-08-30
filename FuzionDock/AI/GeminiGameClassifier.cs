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

        public static IReadOnlyDictionary<string, string> ClassifyGames(IEnumerable<Program> programs)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Constants.HasGeminiApiKey || programs == null)
            {
                return result;
            }

            var candidates = programs
                .Where(ShouldClassify)
                .Select((program, index) => new BatchCandidate { Index = index, Program = program })
                .ToList();

            if (candidates.Count == 0)
            {
                return result;
            }

            foreach (var batch in Batch(candidates, MaxBatchSize))
            {
                try
                {
                    foreach (var classification in ClassifyBatch(batch))
                    {
                        result[classification.Key] = classification.Value;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Gemini classification batch failed: {ex.Message}");
                }
            }

            return result;
        }

        private static bool ShouldClassify(Program program)
        {
            return program != null
                && !string.IsNullOrWhiteSpace(program.DisplayName)
                && !program.IsGame
                && !program.DisplayName.IsFalsePositive()
                && !LocalDatabase.IsGame(program.DisplayName)
                && !LocalDatabase.IsProgram(program.DisplayName);
        }

        private static Dictionary<string, string> ClassifyBatch(List<BatchCandidate> batch)
        {
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
                Uri.EscapeDataString(Constants.GeminiModel),
                Uri.EscapeDataString(Constants.geminiApiKey));

            string prompt = BuildPrompt(batch);

            var requestBody = new JObject
            {
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray
                    {
                        new JObject
                        {
                            ["text"] = "Classify Windows programs and return only actual video games from the provided list. Exclude launchers, redistributables, tools, editors, drivers, anti-cheat, mods, dedicated servers, benchmarks, installers, and anything uncertain."
                        }
                    }
                },
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject
                            {
                                ["text"] = prompt
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["responseJsonSchema"] = BuildSchema(),
                    ["temperature"] = 0.1,
                    ["maxOutputTokens"] = 2048
                },
                ["store"] = false
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = httpClient.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult())
                {
                    string responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"Gemini API request failed: {(int)response.StatusCode} {response.ReasonPhrase} {responseContent}");
                    }

                    return ParseClassifications(batch, responseContent);
                }
            }
        }

        private static JObject BuildSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["games"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["index"] = new JObject { ["type"] = "integer" },
                                ["detectedName"] = new JObject { ["type"] = "string" },
                                ["canonicalTitle"] = new JObject { ["type"] = "string" }
                            },
                            ["required"] = new JArray("index", "detectedName", "canonicalTitle"),
                            ["additionalProperties"] = false,
                            ["propertyOrdering"] = new JArray("index", "detectedName", "canonicalTitle")
                        }
                    }
                },
                ["required"] = new JArray("games"),
                ["additionalProperties"] = false,
                ["propertyOrdering"] = new JArray("games")
            };
        }

        private static string BuildPrompt(List<BatchCandidate> batch)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Return JSON only.");
            builder.AppendLine("For each input item that is an actual video game, include the exact detectedName from input and a cleaned canonicalTitle.");
            builder.AppendLine("If uncertain, exclude the item.");
            builder.AppendLine("Items:");

            foreach (var item in batch)
            {
                builder.Append("- index: ").Append(item.Index)
                    .Append(" | detectedName: ").Append(item.Program.DisplayName)
                    .Append(" | publisher: ").Append(string.IsNullOrWhiteSpace(item.Program.Publisher) ? "unknown" : item.Program.Publisher)
                    .Append(" | launcher: ").Append(item.Program.Launcher)
                    .Append(" | exeName: ").Append(string.IsNullOrWhiteSpace(item.Program.ExeName) ? "unknown" : item.Program.ExeName)
                    .AppendLine();
            }

            return builder.ToString();
        }

        private static Dictionary<string, string> ParseClassifications(List<BatchCandidate> batch, string responseContent)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JObject outer = JObject.Parse(responseContent);
            string outputJson = outer["candidates"]?.FirstOrDefault()?["content"]?["parts"]?.FirstOrDefault()?["text"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(outputJson))
            {
                return result;
            }

            JObject parsed = JObject.Parse(outputJson);
            JArray games = parsed["games"] as JArray;
            if (games == null)
            {
                return result;
            }

            foreach (JObject game in games.OfType<JObject>())
            {
                int? index = game["index"]?.Value<int>();
                string detectedName = game["detectedName"]?.Value<string>()?.Trim();
                string canonicalTitle = game["canonicalTitle"]?.Value<string>()?.Trim();

                if (!index.HasValue || index.Value < 0 || index.Value >= batch.Count)
                {
                    continue;
                }

                string expectedName = batch[index.Value].Program.DisplayName;
                if (!string.Equals(expectedName, detectedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result[expectedName] = string.IsNullOrWhiteSpace(canonicalTitle) ? expectedName : canonicalTitle;
            }

            return result;
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