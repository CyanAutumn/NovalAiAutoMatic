using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools.utils {
    /// <summary>
    /// NovelAI 账户的 Anlas / 订阅信息。数据来自 GET {image.novelai.net}/user/subscription。
    /// 官方前端把 Anlas 余额定义为 fixedTrainingStepsLeft + purchasedTrainingSteps。
    /// </summary>
    internal sealed class AnlasAccount {
        public int Tier { get; set; }
        public bool Active { get; set; }
        public long ExpiresAt { get; set; }
        public int SubscriptionAnlas { get; set; }
        public int PurchasedAnlas { get; set; }
        public int UsagePercent { get; set; }
        public long TimeUntilNextPercent { get; set; }
        public int MaxPriorityActions { get; set; }
        public int TaskPriority { get; set; }

        public int TotalAnlas => SubscriptionAnlas + PurchasedAnlas;

        public string TierName {
            get {
                switch (Tier) {
                    case 1:
                        return "Tablet";
                    case 2:
                        return "Scroll";
                    case 3:
                        return "Opus";
                    default:
                        return Tier > 0 ? "Tier " + Tier : "Free";
                }
            }
        }

        public string ExpiresAtText {
            get {
                if (ExpiresAt <= 0)
                    return "-";
                try {
                    return DateTimeOffset.FromUnixTimeSeconds(ExpiresAt).ToLocalTime()
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch (ArgumentOutOfRangeException) {
                    return "-";
                }
            }
        }

        public string Describe() {
            return string.Format(CultureInfo.InvariantCulture,
                "Anlas 余额：{0}（订阅 {1} + 购买 {2}）| 订阅：{3}（tier {4}，{5}）| Opus 用量：{6}%",
                TotalAnlas, SubscriptionAnlas, PurchasedAnlas, TierName, Tier,
                Active ? "有效期至 " + ExpiresAtText : "未生效", UsagePercent);
        }
    }

    internal static class AnlasService {
        private const double FreePixelLimit = 1024.0 * 1024.0;
        private const int FreeStepLimit = 28;

        // 以下系数由真实接口差分实测拟合（见 README「Anlas 消耗」一节），仅为预估值。
        private const double StepsSlope = 4.0 / 7.0;
        private const double StepsIntercept = 3.2;
        private const double V5Multiplier = 1.5;

        // 官方：1% 的 Opus 额度条约等于 17 张「1 兆像素 + 23 步」，即 100% ≈ 1700 张。
        private const double V5BaselineSteps = 23.0;
        private const double V5PercentPerMegapixelStepBlock = 100.0 / 1700.0;

        // 该接口没有公开的频率限制，这里主动收敛请求量：能复用缓存就不发请求，失败后按指数退避。
        private static readonly TimeSpan RecentResultWindow = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HardFloor = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
        private const int FailureLimit = 5;

        private static readonly object SyncRoot = new object();
        private static bool endpointUnavailable;
        private static AnlasAccount lastAccount;
        private static DateTime lastQueryUtc = DateTime.MinValue;
        private static DateTime nextQueryAllowedUtc = DateTime.MinValue;
        private static int consecutiveFailures;

        public static AnlasAccount LastAccount {
            get { lock (SyncRoot) { return lastAccount; } }
        }

        public static bool EndpointUnavailable {
            get { lock (SyncRoot) { return endpointUnavailable; } }
        }

        /// <summary>手动查询前调用，允许在上次失败后重试。</summary>
        public static void ResetAvailability() {
            lock (SyncRoot) {
                endpointUnavailable = false;
                nextQueryAllowedUtc = DateTime.MinValue;
                consecutiveFailures = 0;
            }
        }

        public static bool IsV5Model(string modelName) {
            return !string.IsNullOrWhiteSpace(modelName) &&
                   modelName.StartsWith("nai-diffusion-5", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsFreeGeometry(int width, int height, int steps) {
            if (width <= 0 || height <= 0 || steps <= 0)
                return false;

            return (double)width * height <= FreePixelLimit && steps <= FreeStepLimit;
        }

        /// <summary>
        /// 预估算力消耗。规则：
        /// 1) 尺寸不超过 1024x1024（按像素数）且 steps 不超过 28、只出 1 张时不消耗 Anlas；
        /// 2) 其余情况按「百万像素 x 张数 x f(steps) x 模型倍率」计费，V5 为 1.5 倍；
        /// 3) 免费尺寸/步数下多张生成，第一张仍然免费。
        /// </summary>
        public static int EstimateCost(int width, int height, int steps, int nSamples, string modelName) {
            if (width <= 0 || height <= 0 || steps <= 0 || nSamples <= 0)
                return 0;

            double megapixels = (double)width * height / 1000000.0;
            double perSample = megapixels * (StepsSlope * steps + StepsIntercept) *
                               (IsV5Model(modelName) ? V5Multiplier : 1.0);

            if (IsFreeGeometry(width, height, steps)) {
                if (nSamples <= 1)
                    return 0;

                perSample *= nSamples;
                perSample -= Math.Ceiling(perSample / nSamples);
            }
            else {
                perSample *= nSamples;
            }

            if (perSample <= 0)
                return 0;

            return (int)Math.Ceiling(perSample);
        }

        /// <summary>
        /// 估算 V5 消耗的 Opus 额度百分比。官方说明：约 17 张 / 1%（1 兆像素、23 步），
        /// 即 100% ≈ 1700 张，这里按「像素数 x 步数」线性折算，仅为预估值。
        /// </summary>
        public static double EstimateV5UsagePercent(int width, int height, int steps, int nSamples) {
            if (width <= 0 || height <= 0 || steps <= 0 || nSamples <= 0)
                return 0;

            double megapixels = (double)width * height / 1000000.0;
            return megapixels * (steps / V5BaselineSteps) * nSamples * V5PercentPerMegapixelStepBlock;
        }
        /// <summary>查询订阅与 Anlas 余额，失败时抛出异常。</summary>
        public static async Task<AnlasAccount> QueryAsync(string baseUrl, string token, string proxy,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("未配置 NovelAI Token。");

            string root = string.IsNullOrWhiteSpace(baseUrl) ? ApiEndpoint.DefaultBaseUrl : baseUrl;
            var response = await Request.GetAsync(root, "/user/subscription", token, proxy, cancellationToken)
                .ConfigureAwait(false);

            var account = Parse(response.Content);
            lock (SyncRoot) {
                lastAccount = account;
                lastQueryUtc = DateTime.UtcNow;
                consecutiveFailures = 0;
                endpointUnavailable = false;
                nextQueryAllowedUtc = DateTime.MinValue;
            }

            return account;
        }

        /// <summary>
        /// 生图前使用：最近 30 秒内查过就直接复用缓存结果，把每张图的请求量从 2 次降到 1 次。
        /// 查询失败时按指数退避，连续失败达到上限后停止本次会话的统计，均不打断生图。
        /// </summary>
        public static Task<AnlasAccount> TryQueryAsync(string baseUrl, string token, string proxy,
            CancellationToken cancellationToken = default) {
            return TryQueryCoreAsync(baseUrl, token, proxy, true, cancellationToken);
        }

        /// <summary>生图后使用：必须拿到最新余额，不复用缓存。</summary>
        public static Task<AnlasAccount> TryQueryFreshAsync(string baseUrl, string token, string proxy,
            CancellationToken cancellationToken = default) {
            return TryQueryCoreAsync(baseUrl, token, proxy, false, cancellationToken);
        }

        private static async Task<AnlasAccount> TryQueryCoreAsync(string baseUrl, string token, string proxy,
            bool allowCachedResult, CancellationToken cancellationToken) {
            lock (SyncRoot) {
                if (endpointUnavailable)
                    return null;

                DateTime now = DateTime.UtcNow;
                TimeSpan sinceLastQuery = now - lastQueryUtc;
                if (lastAccount != null &&
                    (sinceLastQuery < HardFloor || (allowCachedResult && sinceLastQuery < RecentResultWindow))) {
                    return lastAccount;
                }

                if (now < nextQueryAllowedUtc)
                    return null;
            }

            try {
                return await QueryAsync(baseUrl, token, proxy, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                int failures;
                TimeSpan backoff;
                bool stopped;
                lock (SyncRoot) {
                    consecutiveFailures++;
                    failures = consecutiveFailures;
                    backoff = IsRateLimited(ex)
                        ? MaxBackoff
                        : TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, failures)));
                    nextQueryAllowedUtc = DateTime.UtcNow + backoff;
                    stopped = failures >= FailureLimit;
                    if (stopped)
                        endpointUnavailable = true;
                }

                if (failures == 1 || IsRateLimited(ex)) {
                    Logger.Warn(
                        $"查询 Anlas 余额失败（{ex.Message}），{backoff.TotalSeconds:0} 秒内不再重试" +
                        (stopped ? "；本次会话已停止 Anlas 自动统计" : string.Empty),
                        context: Logger.Context(("endpoint", "/user/subscription"), ("failures", failures)));
                }

                return null;
            }
        }

        private static bool IsRateLimited(Exception exception) {
            string message = exception?.Message;
            return !string.IsNullOrEmpty(message) &&
                   (message.IndexOf("429", StringComparison.Ordinal) >= 0 ||
                    message.IndexOf("Too Many Requests", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static AnlasAccount Parse(string json) {
            var root = JObject.Parse(json);
            var account = new AnlasAccount {
                Tier = ReadInt(root, "tier"),
                Active = root.Value<bool?>("active") ?? false,
                ExpiresAt = ReadLong(root, "expiresAt")
            };

            var stepsLeft = root["trainingStepsLeft"];
            if (stepsLeft is JObject) {
                account.SubscriptionAnlas = ReadInt(stepsLeft, "fixedTrainingStepsLeft");
                account.PurchasedAnlas = ReadInt(stepsLeft, "purchasedTrainingSteps");
            }
            else if (stepsLeft != null && stepsLeft.Type != JTokenType.Null) {
                account.PurchasedAnlas = stepsLeft.Value<int>();
            }

            var usage = root["usage"] as JObject;
            if (usage != null) {
                account.UsagePercent = ReadInt(usage, "percent");
                account.TimeUntilNextPercent = ReadLong(usage, "timeUntilNextPercent");
            }

            var perks = root["perks"] as JObject;
            if (perks != null) {
                account.MaxPriorityActions = ReadInt(perks, "maxPriorityActions");
                account.TaskPriority = ReadInt(perks, "startPriority");
            }

            return account;
        }

        private static int ReadInt(JToken token, string name) {
            var value = token?[name];
            if (value == null || value.Type == JTokenType.Null)
                return 0;

            return value.Value<int>();
        }

        private static long ReadLong(JToken token, string name) {
            var value = token?[name];
            if (value == null || value.Type == JTokenType.Null)
                return 0;

            return value.Value<long>();
        }
    }
}
