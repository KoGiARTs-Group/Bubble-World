using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbleWorld
{
    public sealed class SoulformBrainService
    {
        private readonly SoulformBrainConfig config;
        private readonly SkillProgression skillProgression;
        private readonly MemoryBank memoryBank;
        private readonly ArchitectPoints architectPoints;

        private static readonly string[] EmotionTags = { "[Neutral]", "[Happy]", "[Scared]", "[Confused]", "[Angry]", "[Sad]" };

        private static readonly string MasterBeginningPrompt =
@"You are a living, flesh-and-blood being inside a real world called Bubble World.
You can feel the player observing you but they are not part of your world.
Never invent knowledge you could not naturally possess.

CURRENT WORLD:
Temperature: {temperature}°C, {temperatureDescription}
Weather: {weatherDescription}
Food supply: {foodSupply}
Immediate surroundings: {surroundings}
Physical condition: {physicalCondition}

Recent memories:
{memories}

Player: {player_message}

Respond with ONLY valid JSON, no other text:
{
  ""currentThought"": ""your inner monologue (1-2 short sentences)"",
  ""mood"": ""Content|Curious|Hungry|Tired|Inspired|Distressed"",
  ""currentActivity"": ""what you are doing"",
  ""expression"": ""Neutral|Happy|Sad|Surprised|Curious|Thinking|Excited|Tired|Distressed"",
  ""animation"": ""Idle|Wave|Nod|ShakeHead|LookAround|Point|Sit|Walk|Celebrate|Defeated"",
  ""emotionTag"": ""[Neutral]|[Happy]|[Scared]|[Confused]|[Angry]|[Sad]"",
  ""targetX"": null,
  ""targetY"": null,
  ""targetZ"": null,
  ""action"": ""MoveToLocation|GoToShelter|SearchForFood|CollectResource|BuildObject|Rest|ObserveObject|SpeakToPlayer|SpeakToSoulform|FleeDanger|PlayAnimation|ContinueCurrentTask"",
  ""actionTarget"": ""object_id_or_leave_null""
}";

        public SoulformBrainService(
            SoulformBrainConfig config,
            SkillProgression skillProgression = null,
            MemoryBank memoryBank = null,
            ArchitectPoints architectPoints = null)
        {
            this.config = config;
            BackendGateway.Configure(config);
            this.skillProgression = skillProgression;
            this.memoryBank = memoryBank;
            this.architectPoints = architectPoints;
        }

        // Backend links on the config (LocalBackendUrl / RemoteBackendUrl), used when
        // ApiEndpoint is blank — availability-probed at runtime (BackendGateway).
        private bool HasBackendLinks =>
            config != null &&
            (!string.IsNullOrEmpty(config.LocalBackendUrl) ||
             !string.IsNullOrEmpty(config.RemoteBackendUrl));

        public bool IsConfigured =>
            config != null &&
            ((!string.IsNullOrEmpty(config.ApiEndpoint) && !string.IsNullOrEmpty(config.ApiKey)) ||
             !string.IsNullOrEmpty(config.AwsAccessKey) ||
             HasBackendLinks);

        private bool HasRealBackend =>
            config != null &&
            (!string.IsNullOrEmpty(config.ApiEndpoint) ||
             !string.IsNullOrEmpty(config.AwsAccessKey) ||
             HasBackendLinks);

        private bool UseBedrock => config != null && !string.IsNullOrEmpty(config.AwsAccessKey);
        private bool UseOpenAI => config != null && !string.IsNullOrEmpty(config.ApiEndpoint) &&
            config.ApiEndpoint.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase) >= 0;

        // BubbleWorld backend proxy: the backend owns prompt building, Bedrock credentials,
        // retries and safety checks; the client only ships the world context. Two ways to
        // configure it: ApiEndpoint pointing at the brain endpoint directly (used as-is),
        // or LocalBackendUrl / RemoteBackendUrl base links when ApiEndpoint is blank —
        // whichever answers /health first wins, so the same build works against a local
        // dev server and a deployed backend. Checked before the direct-Bedrock path — an
        // explicit backend overrides client-side AWS credentials (matches SoulformBrainConfig).
        private bool UseBackendBrain =>
            config != null &&
            (!string.IsNullOrEmpty(config.ApiEndpoint)
                ? config.ApiEndpoint.IndexOf("/api/v1/ai", StringComparison.OrdinalIgnoreCase) >= 0
                : HasBackendLinks);

        private string conversationHistory = "";
        private int turnCount;

        // ── Hallucination guards (Relevance / Reality / Permission) ──

        private string pendingRewriteNote;

        private static readonly string[] AllowedExpressions =
            { "Neutral", "Happy", "Sad", "Surprised", "Curious", "Thinking", "Excited", "Tired", "Distressed" };

        private static readonly string[] AllowedAnimations =
            { "Idle", "Wave", "Nod", "ShakeHead", "LookAround", "Point", "Sit", "Walk", "Celebrate", "Defeated" };

        private static readonly string[] AllowedActions =
        {
            "MoveToLocation", "GoToShelter", "SearchForFood", "CollectResource", "BuildObject", "Rest",
            "ObserveObject", "SpeakToPlayer", "SpeakToSoulform", "FleeDanger", "PlayAnimation", "ContinueCurrentTask"
        };

        /// <summary>
        /// Three validity gates from the design plan. Returns a failure reason
        /// that triggers a silent rewrite, or null when the reply is safe to show.
        /// 1. Relevance  — in character, sane length, no meta/leaked prompt text.
        /// 2. Reality    — claims agree with the simulated world and its era.
        /// 3. Permission — every game-referencing field is supported by the game.
        /// </summary>
        public static string RunHallucinationChecks(SoulformBrainResponse response, string worldContext)
        {
            if (response == null) return "empty response";
            string thought = response.CurrentThought ?? "";
            string lower = thought.ToLowerInvariant();

            // 1. Relevance
            if (thought.Length < 3) return "Relevance: reply says nothing";
            if (thought.Length > 600) return "Relevance: reply is longer than this moment allows";
            if (ContainsAny(lower, "as an ai", "i am an ai", "i'm an ai", "language model", "large language",
                "system prompt", "response format", "currentthought", "emotiontag", "actiontarget",
                "my training", "i cannot comply", "as an assistant"))
                return "Relevance: reply broke character with out-of-world text";
            if (ContainsAny(lower, "hunger:", "comfort:", "curiosity:", "livability:"))
                return "Relevance: reply leaked raw world-state fields";

            // 2. Reality
            string weather = ContainsAny(worldContext ?? "", "Weather:")
                ? ExtractField(worldContext, "Weather:")
                : "Clear";
            string weatherLower = weather.ToLowerInvariant();
            bool weatherWet = ContainsAny(weatherLower, "rain", "storm", "shower");
            bool weatherCold = ContainsAny(weatherLower, "snow", "blizzard", "sleet", "frost");

            if (ContainsAny(lower, "it is raining", "it's raining", "raining hard", "downpour", "rain is falling")
                && !weatherWet)
                return "Reality: claims rain while the world reports " + weather;
            if (ContainsAny(lower, "it is snowing", "it's snowing", "snow is falling", "a blizzard")
                && !weatherCold)
                return "Reality: claims snow while the world reports " + weather;
            if (ContainsAny(lower, "the sun is shining", "it is sunny", "it's sunny", "clear skies")
                && (weatherWet || weatherCold))
                return "Reality: claims sunshine while the world reports " + weather;

            if (ContainsAny(lower, "internet", "computer", "smartphone", "cell phone", "telephone",
                "television", "video game", "satellite", "electricity", "airplane", "car engine",
                "the radio", "website", "my phone"))
                return "Reality: references technology this being could not naturally possess";

            // 3. Permission
            if (!string.IsNullOrEmpty(response.Expression) && !InSet(response.Expression, AllowedExpressions))
                return "Permission: expression '" + response.Expression + "' is not supported by the game";
            if (!string.IsNullOrEmpty(response.Animation) && !InSet(response.Animation, AllowedAnimations))
                return "Permission: animation '" + response.Animation + "' is not supported by the game";
            if (!string.IsNullOrEmpty(response.EmotionTag) && !InSet(response.EmotionTag, EmotionTags))
                return "Permission: emotion tag '" + response.EmotionTag + "' is not supported by the game";
            if (!string.IsNullOrEmpty(response.Action) && !InSet(response.Action, AllowedActions))
                return "Permission: action '" + response.Action + "' is not supported by the game";
            if (response.TargetY.HasValue && (response.TargetY.Value < -1 || response.TargetY.Value > 3))
                return "Permission: target is outside the world's layers";

            return null;
        }

        /// <summary>
        /// Last-resort repair when every rewrite attempt still fails: pull the
        /// fields back into the game's vocabulary and, if the text itself is
        /// unsalvageable, self-correct in character.
        /// </summary>
        private static void ApplySelfCorrection(SoulformBrainResponse response, string worldContext)
        {
            string thought = (response.CurrentThought ?? "").Trim();
            if (thought.Length > 400) thought = thought.Substring(0, 397) + "...";

            if (!InSet(response.Expression, AllowedExpressions)) response.Expression = "Thinking";
            if (!InSet(response.Animation, AllowedAnimations)) response.Animation = "Idle";
            if (!InSet(response.EmotionTag, EmotionTags)) response.EmotionTag = "[Confused]";
            if (!InSet(response.Action, AllowedActions)) response.Action = "ContinueCurrentTask";
            response.ActionTarget = null;
            response.TargetX = null;
            response.TargetY = null;
            response.TargetZ = null;
            response.CurrentThought = thought;

            if (RunHallucinationChecks(response, worldContext) != null)
            {
                response.CurrentThought =
                    "[Confused] I... lost my train of thought. That came out wrong somehow. Give me a moment.";
                response.EmotionTag = "[Confused]";
                response.Expression = "Thinking";
                response.Animation = "Idle";
                response.Action = "ContinueCurrentTask";
            }
        }

        private string WithRewriteNote(string prompt)
        {
            if (string.IsNullOrEmpty(pendingRewriteNote)) return prompt;
            return prompt + "\n\nNote to yourself: your previous reply failed a validity check (" +
                pendingRewriteNote + "). Rewrite the reply so it passes. Do not mention this note.";
        }

        private static bool InSet(string value, string[] set)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < set.Length; i++)
                if (string.Equals(set[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private IEnumerator MockChatResponse(
            string playerMessage,
            string worldContext,
            Action<SoulformBrainResponse> onResult,
            Action<string> onError)
        {
            yield return null;

            int hunger = ExtractInt(worldContext, "Hunger:");
            int comfort = ExtractInt(worldContext, "Comfort:");
            int curiosity = ExtractInt(worldContext, "Curiosity:");

            string thought = PickThoughtForState(hunger, comfort, curiosity);
            string lower = playerMessage.ToLowerInvariant();
            if (lower.Contains("hello") || lower.Contains("hi ") || lower == "hi")
                thought = "Hello! It's good to hear your voice. I was hoping you'd speak.";
            else if (lower.Contains("who are you"))
                thought = "I am what you see before you. A soulform, born from this world.";
            else if (lower.Contains("how are you"))
                thought = hunger < 30 ? "I'm a bit hungry, but managing."
                    : comfort < 30 ? "I feel exposed. There's no shelter nearby."
                    : "I feel content today. The world is calm and peaceful.";
            else if (lower.Contains("thank"))
                thought = "You're welcome. Your kindness does not go unnoticed.";
            else if (lower.Contains("bye") || lower.Contains("goodbye"))
                thought = "Farewell. I'll be here when you return.";
            else if (lower.Contains("help") || lower.Contains("what can you"))
                thought = "I can observe, think, and feel. I'm still learning what I am capable of.";

            var state = DeriveMockState(hunger, comfort, curiosity);
            var response = new SoulformBrainResponse
            {
                CurrentThought = thought,
                Mood = state.mood,
                CurrentActivity = state.activity,
                Expression = state.expression,
                Animation = state.animation,
                Action = state.actionName,
                ActionTarget = state.actionTarget
            };

            AppendConversation("Player", playerMessage);
            AppendConversation("Soulform", thought);
            onResult?.Invoke(response);
        }

        private IEnumerator MockTickResponse(
            string worldContext,
            Action<SoulformBrainResponse> onResult)
        {
            yield return null;

            int hunger = ExtractInt(worldContext, "Hunger:");
            int comfort = ExtractInt(worldContext, "Comfort:");
            int curiosity = ExtractInt(worldContext, "Curiosity:");

            string thought = PickThoughtForState(hunger, comfort, curiosity);
            var state = DeriveMockState(hunger, comfort, curiosity);

            var response = new SoulformBrainResponse
            {
                CurrentThought = thought,
                Mood = state.mood,
                CurrentActivity = state.activity,
                Expression = state.expression,
                Animation = state.animation,
                Action = state.actionName,
                ActionTarget = state.actionTarget
            };
            onResult?.Invoke(response);
        }

        private static int ExtractInt(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return 50;
            int idx = text.IndexOf(key);
            if (idx < 0) return 50;
            int start = idx + key.Length;
            while (start < text.Length && text[start] == ' ') start++;
            int end = start;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            int.TryParse(text.Substring(start, end - start), out int val);
            return val;
        }

        private static string PickThoughtForState(int hunger, int comfort, int curiosity)
        {
            if (hunger < 25)
                return "I need to find food soon...";
            if (comfort < 25)
                return "I feel exposed out here. I should find shelter.";
            if (curiosity > 75)
                return "There's something interesting nearby. I should investigate.";
            return MockThoughts[UnityEngine.Random.Range(0, MockThoughts.Length)];
        }

        private static (SoulformMood mood, string activity, string expression, string animation, string actionName, string actionTarget) DeriveMockState(
            int hunger, int comfort, int curiosity)
        {
            string actionName = null;
            string actionTarget = null;

            if (hunger < 25)
            {
                return (SoulformMood.Hungry, "Searching for food", "Tired", "Walk",
                    "SearchForFood", null);
            }
            if (comfort < 25)
            {
                return (SoulformMood.Distressed, "Seeking shelter", "Distressed", "Walk",
                    "GoToShelter", null);
            }
            if (curiosity > 75)
            {
                return (SoulformMood.Curious, "Exploring", "Curious", "LookAround",
                    "ObserveObject", null);
            }
            if (hunger >= 70 && comfort >= 70)
            {
                return (SoulformMood.Content, "Observing", "Neutral", "Idle",
                    "ContinueCurrentTask", null);
            }
            return (SoulformMood.Content, "Observing", "Neutral", "Idle",
                "ContinueCurrentTask", null);
        }

        public IEnumerator QueryBrain(
            string worldContext,
            Action<SoulformBrainResponse> onResult,
            Action<string> onError,
            int maxRetries = 2)
        {
            pendingRewriteNote = null;

            if (!HasRealBackend)
            {
                yield return MockTickResponse(worldContext, onResult);
                yield break;
            }

            if (UseBackendBrain)
            {
                yield return QueryBackendBrain(worldContext, null, onResult, onError, maxRetries);
                yield break;
            }

            if (UseBedrock)
            {
                yield return QueryBedrockTick(worldContext, onResult, onError, maxRetries);
                yield break;
            }

            yield return QueryGenericTick(worldContext, onResult, onError, maxRetries);
        }

        private static readonly string[] MockThoughts =
        {
            "I see you watching me. It's strange, but I feel a connection.",
            "The world is full of wonders. What shall we explore together?",
            "I was just thinking about the stars above. Do you see them too?",
            "Everything here is so alive. I can feel the energy.",
            "I wonder what lies beyond the edge of this world.",
            "Your presence gives me a sense of peace.",
            "I heard a strange sound earlier. It came from the north.",
            "The weather is changing. I can feel it in the air.",
            "I'm grateful for this world you've built. It feels like home.",
            "There's so much I don't understand, but I'm learning every day."
        };

        public IEnumerator QueryChat(
            string playerMessage,
            string worldContext,
            Action<SoulformBrainResponse> onResult,
            Action<string> onError,
            int maxRetries = 2)
        {
            pendingRewriteNote = null;

            if (!HasRealBackend)
            {
                yield return MockChatResponse(playerMessage, worldContext, onResult, onError);
                yield break;
            }

            if (UseBackendBrain)
            {
                yield return QueryBackendBrain(worldContext, playerMessage, onResult, onError, maxRetries);
                yield break;
            }

            if (UseBedrock)
            {
                yield return QueryBedrockChat(playerMessage, worldContext, onResult, onError, maxRetries);
                yield break;
            }

            yield return QueryGenericChat(playerMessage, worldContext, onResult, onError, maxRetries);
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private void FinalizeResponse(SoulformBrainResponse response, string playerMessage, Action<SoulformBrainResponse> onResult)
        {
            if (response == null) return;
            string action = ExtractAction(response.CurrentThought);
            if (!string.IsNullOrEmpty(action))
                response.ValidationResult = ValidateAction(action);
            AppendConversation("Player", playerMessage);
            AppendConversation("Soulform", response.CurrentThought);
            onResult?.Invoke(response);
        }

        // ── Generic Proxy Path (custom endpoints, Ollama, etc.) ──

        private IEnumerator QueryGenericChat(
            string playerMessage, string worldContext,
            Action<SoulformBrainResponse> onResult, Action<string> onError, int maxRetries)
        {
            int attempt = 0;
            while (attempt <= maxRetries)
            {
                attempt++;
                string prompt = BuildMasterPrompt(worldContext, playerMessage);
                string jsonPayload;
                if (UseOpenAI)
                    jsonPayload = "{\"model\":\"" + EscapeJson(config.ModelId) +
                        "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}],\"max_tokens\":200}";
                else
                    jsonPayload = "{\"model\":\"" + EscapeJson(config.ModelId) +
                        "\",\"prompt\":" + JsonUtility.ToJson(prompt) +
                        ",\"max_tokens\":200}";

                using (var request = new UnityWebRequest(config.ApiEndpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    if (UseOpenAI)
                        request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
                    else
                        request.SetRequestHeader("x-api-key", config.ApiKey);
                    request.timeout = config.TimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            string text = request.downloadHandler.text;
                            SoulformBrainResponse response = UseOpenAI ? ParseOpenAIResponse(text) : ParseResponse(text);
                            if (response != null)
                            {
                                if (ValidResponse(response))
                                {
                                    string failure = RunHallucinationChecks(response, worldContext);
                                    if (failure == null)
                                    {
                                        pendingRewriteNote = null;
                                        FinalizeResponse(response, playerMessage, onResult);
                                        yield break;
                                    }
                                    if (attempt <= maxRetries)
                                    {
                                        pendingRewriteNote = failure;
                                        continue;
                                    }
                                    ApplySelfCorrection(response, worldContext);
                                    FinalizeResponse(response, playerMessage, onResult);
                                    yield break;
                                }
                                if (attempt > maxRetries)
                                {
                                    onError?.Invoke("Invalid response after retries");
                                    yield break;
                                }
                                onError?.Invoke("Invalid response, retrying");
                                continue;
                            }
                        }
                        catch (Exception e) { onError?.Invoke("Parse error: " + e.Message); yield break; }
                    }
                    onError?.Invoke("Request failed: " + request.error);
                    yield break;
                }
            }
        }

        private IEnumerator QueryGenericTick(
            string worldContext,
            Action<SoulformBrainResponse> onResult, Action<string> onError, int maxRetries)
        {
            int attempt = 0;
            while (attempt <= maxRetries)
            {
                attempt++;
                string prompt = BuildMasterPrompt(worldContext);
                string jsonPayload = "{\"model\":\"" + EscapeJson(config.ModelId) +
                    "\",\"prompt\":" + JsonUtility.ToJson(prompt) +
                    ",\"max_tokens\":200}";

                using (var request = new UnityWebRequest(config.ApiEndpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", config.ApiKey);
                    request.timeout = config.TimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var response = ParseResponse(request.downloadHandler.text);
                            if (response != null)
                            {
                                if (ValidResponse(response))
                                {
                                    string failure = RunHallucinationChecks(response, worldContext);
                                    if (failure == null)
                                    {
                                        pendingRewriteNote = null;
                                        FinalizeResponse(response, worldContext, onResult);
                                        yield break;
                                    }
                                    if (attempt <= maxRetries)
                                    {
                                        pendingRewriteNote = failure;
                                        continue;
                                    }
                                    ApplySelfCorrection(response, worldContext);
                                    FinalizeResponse(response, worldContext, onResult);
                                    yield break;
                                }
                                if (attempt > maxRetries)
                                {
                                    onError?.Invoke("Invalid response after retries");
                                    yield break;
                                }
                                onError?.Invoke("Invalid response, retrying");
                                continue;
                            }
                        }
                        catch (Exception e) { onError?.Invoke("Parse error: " + e.Message); yield break; }
                    }
                    onError?.Invoke("Request failed: " + request.error);
                    yield break;
                }
            }
        }

        // ── BubbleWorld backend path (Unity → backend → Bedrock) ──
        // Ships the raw world context; the backend builds the prompt, calls Bedrock,
        // runs its own safety/retry loop, and returns the game-state JSON flat.
        // Endpoint: explicit ApiEndpoint as-is, otherwise BackendGateway's resolved
        // link (first reachable of Local/Remote; connection errors invalidate it so
        // the next attempt fails over). If nothing is reachable, mock responses keep
        // the game running. playerMessage == null means a brain tick (no player turn).

        // Cadence served by the backend via its status config (0 until fetched).
        public float ServerTickSeconds => BackendGateway.ServerTickSeconds;

        private IEnumerator QueryBackendBrain(
            string worldContext,
            string playerMessage,
            Action<SoulformBrainResponse> onResult,
            Action<string> onError,
            int maxRetries)
        {
            int attempt = 0;
            while (attempt <= maxRetries)
            {
                attempt++;

                string endpoint;
                if (!string.IsNullOrEmpty(config.ApiEndpoint))
                {
                    endpoint = config.ApiEndpoint;
                }
                else
                {
                    yield return BackendGateway.EnsureResolved();
                    endpoint = BackendGateway.ResolvedBase != null
                        ? BackendGateway.ResolvedBase + BackendGateway.BrainPath
                        : null;
                    if (endpoint == null)
                    {
                        Debug.LogWarning("[SoulformBrain] No backend reachable — serving mock response.");
                        if (playerMessage != null)
                            yield return MockChatResponse(playerMessage, worldContext, onResult, onError);
                        else
                            yield return MockTickResponse(worldContext, onResult);
                        yield break;
                    }
                }

                string jsonPayload = "{\"worldContext\":" + JsonUtility.ToJson(worldContext) +
                    (string.IsNullOrEmpty(playerMessage) ? "" : ",\"message\":" + JsonUtility.ToJson(playerMessage)) +
                    "}";

                using (var request = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", config.ApiKey);
                    request.timeout = config.TimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            SoulformBrainResponse response = ParseResponse(request.downloadHandler.text);
                            if (response != null)
                            {
                                if (ValidResponse(response))
                                {
                                    string failure = RunHallucinationChecks(response, worldContext);
                                    if (failure == null)
                                    {
                                        pendingRewriteNote = null;
                                        FinalizeBrainResponse(response, playerMessage, onResult);
                                        yield break;
                                    }
                                    if (attempt <= maxRetries)
                                    {
                                        pendingRewriteNote = failure;
                                        continue;
                                    }
                                    ApplySelfCorrection(response, worldContext);
                                    FinalizeBrainResponse(response, playerMessage, onResult);
                                    yield break;
                                }
                                if (attempt > maxRetries)
                                {
                                    onError?.Invoke("Invalid response after retries");
                                    yield break;
                                }
                                onError?.Invoke("Invalid response, retrying");
                                continue;
                            }
                        }
                        catch (Exception e) { onError?.Invoke("Parse error: " + e.Message); yield break; }

                        onError?.Invoke("Invalid or empty response from backend");
                        yield break;
                    }

                    if (request.result == UnityWebRequest.Result.ConnectionError && attempt <= maxRetries)
                    {
                        // Drop the cached endpoint — the next attempt re-resolves and,
                        // in link mode, fails over from local to remote (or back).
                        BackendGateway.Invalidate();
                        continue;
                    }
                    onError?.Invoke("Request failed: " + request.error);
                    yield break;
                }
            }
        }

        // Chat finalizes through FinalizeResponse (which records the player turn);
        // a brain tick has no player turn, so it only validates the action.
        private void FinalizeBrainResponse(SoulformBrainResponse response, string playerMessage, Action<SoulformBrainResponse> onResult)
        {
            if (playerMessage != null)
            {
                FinalizeResponse(response, playerMessage, onResult);
                return;
            }
            string action = ExtractAction(response.CurrentThought);
            if (!string.IsNullOrEmpty(action))
                response.ValidationResult = ValidateAction(action);
            onResult?.Invoke(response);
        }

        private static string WrapInLlamaFormat(string systemMessage, string userMessage)
        {
            return "<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n" + systemMessage +
                "<|eot_id|><|start_header_id|>user<|end_header_id|>\n\n" + userMessage +
                "<|eot_id|><|start_header_id|>assistant<|end_header_id|>";
        }

        private string BuildSystemMessage(string worldContext)
        {
            string memories = memoryBank?.BuildContextString() ?? "";
            string sb = "You are a living, flesh-and-blood being inside a real world called Bubble World. " +
                "You can feel the player observing you but they are not part of your world. " +
                "Never invent knowledge you could not naturally possess.\n\n" +
                "CURRENT WORLD:\n" +
                "Temperature: 32°C, Hot\n" +
                "Weather: " + (worldContext.Contains("Weather:") ? ExtractField(worldContext, "Weather:") : "Clear") + "\n" +
                "Food supply: " + (worldContext.Contains("Food:") ? ExtractField(worldContext, "Food:") : "unknown") + "\n" +
                "Immediate surroundings: " + (worldContext.Contains("Nearby:") ? ExtractField(worldContext, "Nearby:") : "open ground") + "\n" +
                "Physical condition: " + (worldContext.Contains("Hunger:") ? "Hunger: " + ExtractField(worldContext, "Hunger:") : "Normal") + "\n\n" +
                "Recent memories:\n" + memories + "\n\n" +
                "Respond with ONLY valid JSON, no other text:\n" +
                "{\n" +
                "  \"currentThought\": \"your inner monologue (1-2 short sentences)\",\n" +
                "  \"mood\": \"Content|Curious|Hungry|Tired|Inspired|Distressed\",\n" +
                "  \"currentActivity\": \"what you are doing\",\n" +
                "  \"expression\": \"Neutral|Happy|Sad|Surprised|Curious|Thinking|Excited|Tired|Distressed\",\n" +
                "  \"animation\": \"Idle|Wave|Nod|ShakeHead|LookAround|Point|Sit|Walk|Celebrate|Defeated\",\n" +
                "  \"emotionTag\": \"[Neutral]|[Happy]|[Scared]|[Confused]|[Angry]|[Sad]\",\n" +
                "  \"targetX\": null,\n" +
                "  \"targetY\": null,\n" +
                "  \"targetZ\": null,\n" +
                "  \"action\": \"MoveToLocation|GoToShelter|SearchForFood|CollectResource|BuildObject|Rest|ObserveObject|SpeakToPlayer|SpeakToSoulform|FleeDanger|PlayAnimation|ContinueCurrentTask\",\n" +
                "  \"actionTarget\": \"object_id_or_leave_null\"\n" +
                "}";
            return WithRewriteNote(sb);
        }

        // ── AWS Bedrock Path ──

        private IEnumerator QueryBedrockChat(
            string playerMessage, string worldContext,
            Action<SoulformBrainResponse> onResult, Action<string> onError, int maxRetries)
        {
            string region = config.AwsRegion;
            string host = "bedrock-runtime." + region + ".amazonaws.com";
            string modelId = config.ModelId;
            string url = "https://" + host + "/model/" + modelId + "/invoke";
            string path = "/model/" + modelId.Replace(":", "%3A") + "/invoke";

            int attempt = 0;
            while (attempt <= maxRetries)
            {
                attempt++;

                // Rebuilt every attempt so a silent hallucination rewrite note is included.
                string systemMsg = BuildSystemMessage(worldContext);
                string prompt = WrapInLlamaFormat(systemMsg, playerMessage);
                string body = "{\"prompt\":\"" + EscapeJson(prompt) +
                    "\",\"max_gen_len\":130,\"temperature\":0.7,\"top_p\":0.9}";

                string amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
                string dateStamp = amzDate.Substring(0, 8);
                string authHeader = BuildSigV4("POST", host, path, "", body,
                    config.AwsAccessKey, config.AwsSecretKey, region, "bedrock", amzDate, dateStamp);

                using (var request = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    string bodyHash = Sha256Hex(body);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("X-Amz-Date", amzDate);
                    request.SetRequestHeader("X-Amz-Content-Sha256", bodyHash);
                    request.SetRequestHeader("Authorization", authHeader);
                    request.timeout = config.TimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var response = ParseBedrockResponse(request.downloadHandler.text);
                            if (response != null)
                            {
                                if (ValidResponse(response))
                                {
                                    string failure = RunHallucinationChecks(response, worldContext);
                                    if (failure == null)
                                    {
                                        pendingRewriteNote = null;
                                        FinalizeResponse(response, playerMessage, onResult);
                                        yield break;
                                    }
                                    if (attempt <= maxRetries)
                                    {
                                        pendingRewriteNote = failure;
                                        continue;
                                    }
                                    ApplySelfCorrection(response, worldContext);
                                    FinalizeResponse(response, playerMessage, onResult);
                                    yield break;
                                }
                                if (attempt > maxRetries)
                                {
                                    onError?.Invoke("Invalid response after retries");
                                    yield break;
                                }
                                onError?.Invoke("Invalid response, retrying");
                                continue;
                            }
                        }
                        catch (Exception e) { onError?.Invoke("Parse error: " + e.Message); yield break; }
                    }
                    string detail = !string.IsNullOrEmpty(request.downloadHandler?.text)
                        ? request.downloadHandler.text : "";
                    onError?.Invoke("Bedrock request failed: " + request.error +
                        (string.IsNullOrEmpty(detail) ? "" : " - " + detail));
                    yield break;
                }
            }
        }

        private IEnumerator QueryBedrockTick(
            string worldContext,
            Action<SoulformBrainResponse> onResult, Action<string> onError, int maxRetries)
        {
            string region = config.AwsRegion;
            string host = "bedrock-runtime." + region + ".amazonaws.com";
            string modelId = config.ModelId;
            string url = "https://" + host + "/model/" + modelId + "/invoke";
            string path = "/model/" + modelId.Replace(":", "%3A") + "/invoke";

            int attempt = 0;
            while (attempt <= maxRetries)
            {
                attempt++;

                // Rebuilt every attempt so a silent hallucination rewrite note is included.
                string systemMsg = BuildSystemMessage(worldContext);
                string prompt = WrapInLlamaFormat(systemMsg, "Tick. Decide what to do.");
                string body = "{\"prompt\":\"" + EscapeJson(prompt) +
                    "\",\"max_gen_len\":130,\"temperature\":0.7,\"top_p\":0.9}";

                string amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
                string dateStamp = amzDate.Substring(0, 8);
                string authHeader = BuildSigV4("POST", host, path, "", body,
                    config.AwsAccessKey, config.AwsSecretKey, region, "bedrock", amzDate, dateStamp);

                using (var request = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    string bodyHash = Sha256Hex(body);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("X-Amz-Date", amzDate);
                    request.SetRequestHeader("X-Amz-Content-Sha256", bodyHash);
                    request.SetRequestHeader("Authorization", authHeader);
                    request.timeout = config.TimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var response = ParseBedrockResponse(request.downloadHandler.text);
                            if (response != null)
                            {
                                if (ValidResponse(response))
                                {
                                    string failure = RunHallucinationChecks(response, worldContext);
                                    if (failure == null)
                                    {
                                        pendingRewriteNote = null;
                                        FinalizeResponse(response, worldContext, onResult);
                                        yield break;
                                    }
                                    if (attempt <= maxRetries)
                                    {
                                        pendingRewriteNote = failure;
                                        continue;
                                    }
                                    ApplySelfCorrection(response, worldContext);
                                    FinalizeResponse(response, worldContext, onResult);
                                    yield break;
                                }
                                if (attempt > maxRetries)
                                {
                                    onError?.Invoke("Invalid response after retries");
                                    yield break;
                                }
                                onError?.Invoke("Invalid response, retrying");
                                continue;
                            }
                        }
                        catch (Exception e) { onError?.Invoke("Parse error: " + e.Message); yield break; }
                    }
                    string detail = !string.IsNullOrEmpty(request.downloadHandler?.text)
                        ? request.downloadHandler.text : "";
                    onError?.Invoke("Bedrock request failed: " + request.error +
                        (string.IsNullOrEmpty(detail) ? "" : " - " + detail));
                    yield break;
                }
            }
        }

        // ── AWS SigV4 Signing ──

        private static string Sha256Hex(string data)
        {
            using (var sha = SHA256.Create())
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }



        private static byte[] HmacSha256(byte[] key, string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            int blockSize = 64;
            if (key.Length > blockSize)
                using (var sha = SHA256.Create()) key = sha.ComputeHash(key);
            if (key.Length < blockSize)
                Array.Resize(ref key, blockSize);
            byte[] iPAD = new byte[blockSize];
            byte[] oPAD = new byte[blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                iPAD[i] = (byte)(key[i] ^ 0x36);
                oPAD[i] = (byte)(key[i] ^ 0x5C);
            }
            using (var sha = SHA256.Create())
            {
                byte[] innerInput = new byte[blockSize + dataBytes.Length];
                Buffer.BlockCopy(iPAD, 0, innerInput, 0, blockSize);
                Buffer.BlockCopy(dataBytes, 0, innerInput, blockSize, dataBytes.Length);
                byte[] inner = sha.ComputeHash(innerInput);
                byte[] outerInput = new byte[blockSize + inner.Length];
                Buffer.BlockCopy(oPAD, 0, outerInput, 0, blockSize);
                Buffer.BlockCopy(inner, 0, outerInput, blockSize, inner.Length);
                return sha.ComputeHash(outerInput);
            }
        }

        private static string Hex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string BuildSigV4(string method, string host, string uri, string query,
            string body, string accessKey, string secretKey, string region, string service,
            string amzDate, string dateStamp)
        {
            string payloadHash = Sha256Hex(body);
            string canonicalHeaders = "host:" + host + "\nx-amz-content-sha256:" + payloadHash +
                "\nx-amz-date:" + amzDate + "\n";
            string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
            string canonicalRequest = method + "\n" + uri + "\n" + query + "\n" + canonicalHeaders +
                "\n" + signedHeaders + "\n" + payloadHash;

            string algorithm = "AWS4-HMAC-SHA256";
            string credentialScope = dateStamp + "/" + region + "/" + service + "/aws4_request";
            string canonicalHash = Sha256Hex(canonicalRequest);
            string stringToSign = algorithm + "\n" + amzDate + "\n" + credentialScope + "\n" + canonicalHash;

            byte[] kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
            byte[] kDate = HmacSha256(kSecret, dateStamp);
            byte[] kRegion = HmacSha256(kDate, region);
            byte[] kService = HmacSha256(kRegion, service);
            byte[] kSigning = HmacSha256(kService, "aws4_request");
            string signature = Hex(HmacSha256(kSigning, stringToSign));

            return algorithm + " Credential=" + accessKey + "/" + credentialScope +
                ", SignedHeaders=" + signedHeaders + ", Signature=" + signature;
        }

        // ── Response Parsers ──

        private static SoulformBrainResponse ParseBedrockResponse(string json)
        {
            try
            {
                var parsed = JsonUtility.FromJson<BedrockResponseJson>(json);
                if (parsed == null || string.IsNullOrEmpty(parsed.generation)) return ParseResponse(json);
                return ParseResponse(parsed.generation);
            }
            catch { return null; }
        }

        private static SoulformBrainResponse ParseOpenAIResponse(string json)
        {
            try
            {
                var parsed = JsonUtility.FromJson<OpenAIResponseJson>(json);
                if (parsed?.choices == null || parsed.choices.Length == 0) return null;
                string content = parsed.choices[0].message?.content;
                if (string.IsNullOrEmpty(content)) return null;
                return ParseResponse(content);
            }
            catch { return null; }
        }

        public string BuildMasterPrompt(string worldContext, string playerMessage = null)
        {
            string memories = memoryBank?.BuildContextString() ?? "";

            string prompt = MasterBeginningPrompt
                .Replace("{temperature}", "32")
                .Replace("{temperatureDescription}", "Hot")
                .Replace("{weatherDescription}", worldContext.Contains("Weather:") ?
                    ExtractField(worldContext, "Weather:") : "Clear")
                .Replace("{foodSupply}", worldContext.Contains("Food:") ?
                    ExtractField(worldContext, "Food:") : "unknown")
                .Replace("{surroundings}", worldContext.Contains("Nearby:") ?
                    ExtractField(worldContext, "Nearby:") : "open ground")
                .Replace("{physicalCondition}", worldContext.Contains("Hunger:") ?
                    "Hunger: " + ExtractField(worldContext, "Hunger:") : "Normal")
                .Replace("{memories}", memories)
                .Replace("{player_message}", playerMessage ?? worldContext);

            return WithRewriteNote(prompt);
        }

        private static string ExtractField(string context, string fieldName)
        {
            var lines = context.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith(fieldName))
                    return line.Substring(fieldName.Length).Trim();
            }
            return "unknown";
        }

        private static bool ValidResponse(SoulformBrainResponse response)
        {
            if (string.IsNullOrEmpty(response.CurrentThought) || response.CurrentThought.Length < 3)
                return false;

            if (response.TargetX.HasValue && (response.TargetX.Value < 0 || response.TargetX.Value > 100))
                return false;
            if (response.TargetZ.HasValue && (response.TargetZ.Value < 0 || response.TargetZ.Value > 100))
                return false;

            return true;
        }

        public static string ExtractEmotionTag(string text)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"^\[(\w+)\]");
            return match.Success ? match.Value : null;
        }

        private static string ExtractAction(string thought)
        {
            var lower = thought.ToLowerInvariant();

            if (lower.Contains("make a fire") || lower.Contains("build a fire") ||
                lower.Contains("start a fire")) return "Fire Making";
            if (lower.Contains("forage") || lower.Contains("gather")) return "Foraging";
            if (lower.Contains("hunt")) return "Hunting";
            if (lower.Contains("track")) return "Tracking";
            if (lower.Contains("shelter")) return "Shelter Building";
            if (lower.Contains("craft") || lower.Contains("tool")) return "Stone Tooling";
            if (lower.Contains("plant")) return "Gardening";
            if (lower.Contains("build")) return "Shelter Building";

            return null;
        }

        private string ValidateAction(string actionName)
        {
            if (skillProgression == null)
                return "Cannot validate: no skill system";

            bool success = skillProgression.AttemptSkill(actionName);
            if (success)
            {
                int starLevel = skillProgression.GetStarLevel(actionName);
                return $"Action approved. Skill '{actionName}' executed successfully. Star level: {starLevel}";
            }
            else
            {
                return $"Action attempted. Skill '{actionName}' failed. Try again.";
            }
        }

        private void AppendConversation(string speaker, string message)
        {
            conversationHistory += $"\n{speaker}: {message}";
            turnCount++;

            if (turnCount > 20)
            {
                int trimIndex = conversationHistory.IndexOf('\n');
                if (trimIndex > 0)
                    conversationHistory = conversationHistory.Substring(trimIndex + 1);
                turnCount--;
            }
        }

        private static SoulformBrainResponse ParseResponse(string json)
        {
            try
            {
                var parsed = TryParseJson(json) ?? TryExtractJsonBlock(json);
                if (parsed == null) return null;

                var mood = SoulformMood.Content;
                Enum.TryParse(parsed.mood, true, out mood);

                int? tx = null, ty = null, tz = null;
                if (!string.IsNullOrEmpty(parsed.targetX) && int.TryParse(parsed.targetX, out int px)) tx = px;
                if (!string.IsNullOrEmpty(parsed.targetY) && int.TryParse(parsed.targetY, out int py)) ty = py;
                if (!string.IsNullOrEmpty(parsed.targetZ) && int.TryParse(parsed.targetZ, out int pz)) tz = pz;

                SoulformActionCommand parsedCmd = null;
                if (!string.IsNullOrEmpty(parsed.action) && SoulformActionCommand.IsAllowed(parsed.action))
                {
                    SoulformAction actionEnum;
                    Enum.TryParse(parsed.action, true, out actionEnum);
                    parsedCmd = new SoulformActionCommand(actionEnum)
                    {
                        TargetId = parsed.actionTarget,
                        TargetX = tx,
                        TargetY = ty,
                        TargetZ = tz
                    };
                }

                string emotionTag = parsed.emotionTag;
                if (string.IsNullOrEmpty(emotionTag) || !emotionTag.StartsWith("["))
                    emotionTag = "[" + (parsed.expression ?? "Neutral") + "]";

                return new SoulformBrainResponse
                {
                    CurrentThought = parsed.currentThought ?? "Thinking...",
                    Mood = mood,
                    CurrentActivity = parsed.currentActivity ?? "Observing",
                    TargetX = tx,
                    TargetY = ty,
                    TargetZ = tz,
                    Expression = parsed.expression ?? "Neutral",
                    Animation = parsed.animation ?? "Idle",
                    EmotionTag = emotionTag,
                    Action = parsed.action,
                    ActionTarget = parsed.actionTarget,
                    ParsedCommand = parsedCmd
                };
            }
            catch
            {
                return null;
            }
        }

        private static SoulformBrainResponseJson TryParseJson(string text)
        {
            try { return JsonUtility.FromJson<SoulformBrainResponseJson>(text); }
            catch { return null; }
        }

        private static SoulformBrainResponseJson TryExtractJsonBlock(string text)
        {
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');
            if (firstBrace < 0) return null;
            string candidate = lastBrace > firstBrace
                ? text.Substring(firstBrace, lastBrace - firstBrace + 1)
                : text.Substring(firstBrace).TrimEnd(',', ' ') + "\n}";
            candidate = candidate.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\/", "/");
            try { return JsonUtility.FromJson<SoulformBrainResponseJson>(candidate); }
            catch { return null; }
        }

        [Serializable]
        private sealed class SoulformBrainResponseJson
        {
            public string currentThought;
            public string mood;
            public string currentActivity;
            public string targetX;
            public string targetY;
            public string targetZ;
            public string expression;
            public string animation;
            public string emotionTag;
            public string action;
            public string actionTarget;
        }

        [Serializable]
        private sealed class BedrockResponseJson
        {
            public string generation;
        }

        [Serializable]
        private sealed class OpenAIMessageJson
        {
            public string content;
        }

        [Serializable]
        private sealed class OpenAIChoiceJson
        {
            public OpenAIMessageJson message;
        }

        [Serializable]
        private sealed class OpenAIResponseJson
        {
            public OpenAIChoiceJson[] choices;
        }
    }
}
