using System;
using System.Globalization;
using System.Threading.Tasks;
using AutoNai3Tools.body;
using AutoNai3Tools.utils;
using Newtonsoft.Json.Linq;

namespace AutoNai3Tools {
    public partial class Form1 {
        private static readonly TimeSpan AnlasQueryCooldown = TimeSpan.FromSeconds(2);

        private DateTime lastAnlasQueryUtc = DateTime.MinValue;
        private bool startupAnlasRefreshStarted;
        private bool anlasQueryBusy;

        /// <summary>启动后拉一次余额，让界面上的「剩余」有值。</summary>
        private void BeginStartupAnlasRefresh() {
            if (startupAnlasRefreshStarted || !settingProps.AnlasTracking)
                return;

            startupAnlasRefreshStarted = true;
            _ = RefreshAnlasBalanceAsync();
        }

        private void OnAnlasTrackingChanged() {
            if (settingProps.AnlasTracking)
                _ = RefreshAnlasBalanceAsync();
            PushAnlasEstimate();
        }

        /// <summary>当前参数组合的单张消耗：优先用实测缓存，其次用公式估算。</summary>
        internal int EstimateCurrentCost(out bool measured) {
            measured = false;
            int width = picProps.Width;
            int height = picProps.Height;
            int steps = picProps.Steps;
            if (width <= 0 || height <= 0 || steps <= 0)
                return 0;

            string model = BodyTools.GetEnumDescription(picProps.Model);
            string key = AnlasCostCache.BuildKey(model, width, height, steps, 1, "generate", null);
            if (AnlasCostCache.TryGet(key, out int cached)) {
                measured = true;
                return cached;
            }

            return AnlasService.EstimateCost(width, height, steps, 1, model);
        }

        /// <summary>整轮（跑图数量张）的预计消耗。</summary>
        internal int EstimateRunCost(int perImageCost, bool measured) {
            int width = picProps.Width;
            int height = picProps.Height;
            int steps = picProps.Steps;
            int count = Math.Max(1, picProps.RunNum);
            string model = BodyTools.GetEnumDescription(picProps.Model);

            if (!measured || (count > 1 && AnlasService.IsFreeGeometry(width, height, steps)))
                return AnlasService.EstimateCost(width, height, steps, count, model);

            return perImageCost * count;
        }

        internal JObject BuildAnlasJson() {
            var account = AnlasService.LastAccount;
            int cost = EstimateCurrentCost(out bool measured);
            int runCost = EstimateRunCost(cost, measured);
            string model = BodyTools.GetEnumDescription(picProps.Model);
            bool isV5 = AnlasService.IsV5Model(model);

            var json = new JObject {
                ["tracking"] = settingProps.AnlasTracking,
                ["busy"] = anlasQueryBusy,
                ["model"] = model,
                ["isV5"] = isV5,
                ["samples"] = Math.Max(1, picProps.RunNum),
                ["cost"] = cost,
                ["runCost"] = runCost,
                ["measured"] = measured,
                ["quotaPercent"] = isV5
                    ? AnlasService.EstimateV5UsagePercent(picProps.Width, picProps.Height, picProps.Steps,
                        Math.Max(1, picProps.RunNum))
                    : 0d,
                ["v5BaselinePercent"] = isV5
                    ? AnlasService.EstimateV5UsagePercent(picProps.Width, picProps.Height, picProps.Steps, 1)
                    : 0d,
                ["hasAccount"] = account != null
            };

            if (account != null) {
                json["total"] = account.TotalAnlas;
                json["subscription"] = account.SubscriptionAnlas;
                json["purchased"] = account.PurchasedAnlas;
                json["usagePercent"] = account.UsagePercent;
                json["tier"] = account.Tier;
                json["tierName"] = account.TierName;
                json["expiresAt"] = account.ExpiresAtText;
                json["describe"] = account.Describe();
            }

            return json;
        }

        internal void PushAnlasEstimate() {
            Post("anlas", BuildAnlasJson());
        }

        private async Task RefreshAnlasBalanceAsync() {
            try {
                await QueryAnlasAsync(false);
            }
            catch (Exception ex) {
                Logger.Warn("刷新 Anlas 余额失败",
                    context: Logger.Context(("reason", ex.Message)));
            }
        }

        /// <summary>查询余额；manual 为 true 时由「查询余额」按钮触发，失败会写入错误日志。</summary>
        internal async Task<bool> QueryAnlasAsync(bool manual) {
            string token = settingProps?.Token;
            if (string.IsNullOrWhiteSpace(token)) {
                if (manual) {
                    Logger.Warn("查询 Anlas 失败：请先填写 NovelAI Token");
                    PushToast("warn", "请先填写 NovelAI Token");
                }
                return false;
            }

            if (manual) {
                if (DateTime.UtcNow - lastAnlasQueryUtc < AnlasQueryCooldown) {
                    Logger.Warn("查询过于频繁，请稍后再试");
                    PushToast("warn", "查询过于频繁，请稍后再试");
                    return false;
                }

                lastAnlasQueryUtc = DateTime.UtcNow;
            }

            anlasQueryBusy = true;
            PushAnlasEstimate();

            try {
                AnlasService.ResetAvailability();
                var account = await AnlasService.QueryAsync(ApiEndpoint.ResolveBaseUrl(settingProps?.Api), token,
                    settingProps?.Proxy).ConfigureAwait(true);

                if (manual) {
                    Logger.Info(account.Describe(),
                        context: Logger.Context(("anlas", account.TotalAnlas),
                            ("subscriptionAnlas", account.SubscriptionAnlas),
                            ("purchasedAnlas", account.PurchasedAnlas),
                            ("tier", account.Tier),
                            ("usagePercent", account.UsagePercent)));
                    PushToast("ok", string.Format(CultureInfo.InvariantCulture, "余额 {0}（{1}）",
                        account.TotalAnlas, account.TierName));
                }

                return true;
            }
            catch (Exception ex) {
                if (manual) {
                    Logger.Error("查询 Anlas 失败", exception: ex,
                        context: Logger.Context(("endpoint", "/user/subscription")));
                    PushToast("err", "查询失败：" + ex.Message);
                }
                else {
                    Logger.Warn("刷新 Anlas 余额失败",
                        context: Logger.Context(("endpoint", "/user/subscription"), ("reason", ex.Message)));
                }
                return false;
            }
            finally {
                anlasQueryBusy = false;
                PushAnlasEstimate();
            }
        }
    }
}