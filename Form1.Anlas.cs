using System;
using System.Globalization;
using System.Threading.Tasks;
using AutoNai3Tools.body;
using AutoNai3Tools.utils;

namespace AutoNai3Tools {
    public partial class Form1 {
        private static readonly TimeSpan AnlasQueryCooldown = TimeSpan.FromSeconds(2);

        private DateTime lastAnlasQueryUtc = DateTime.MinValue;
        private string generateButtonBaseText;
        private bool generateButtonRunning;
        private bool startupAnlasRefreshStarted;

        /// <summary>记录「生成」按钮的原始文案，并挂上需要刷新预估的时机。</summary>
        private void InitializeAnlasDisplay() {
            GenerateButtonBaseTextIfNeeded();

            if (generationController != null) {
                generationController.ImageReady += (iteration, bitmap) => RefreshAnlasButtonText();
                generationController.Completed += RefreshAnlasButtonText;
            }

            RefreshAnlasButtonText();
        }

        /// <summary>启动后拉一次余额，让按钮上的「剩余」有值。</summary>
        private void BeginStartupAnlasRefresh() {
            if (startupAnlasRefreshStarted || !settingProps.AnlasTracking)
                return;

            startupAnlasRefreshStarted = true;
            RefreshAnlasBalanceAsync();
        }

        private void OnAnlasTrackingChanged() {
            GenerateButtonBaseTextIfNeeded();
            if (settingProps.AnlasTracking)
                RefreshAnlasBalanceAsync();
            RefreshAnlasButtonText();
        }

        /// <summary>生成开始/结束时切换按钮基准文案（停止 / 生成），并同步刷新 Anlas 信息。</summary>
        private void SetGenerateButtonRunning(bool running) {
            generateButtonRunning = running;
            RefreshAnlasButtonText();
        }

        /// <summary>把「参数组合的预估消耗 / 剩余总量」拼到「生成」（生成中为「停止」）右侧。</summary>
        private void RefreshAnlasButtonText() {
            if (btnGenerate == null || btnGenerate.IsDisposed)
                return;

            // 生图事件来自后台线程，必须切回 UI 线程再改按钮文案。
            if (InvokeRequired) {
                if (!IsHandleCreated || IsDisposed)
                    return;

                try {
                    BeginInvoke(new Action(RefreshAnlasButtonText));
                }
                catch (InvalidOperationException) {
                    // 窗口正在关闭，忽略这一次刷新
                }
                return;
            }

            GenerateButtonBaseTextIfNeeded();

            string baseText = generateButtonRunning
                ? Properties.Resources.Button_Stop
                : generateButtonBaseText;

            string info = settingProps != null && settingProps.AnlasTracking ? BuildAnlasButtonInfo() : null;
            btnGenerate.Text = string.IsNullOrEmpty(info)
                ? baseText
                : baseText + "    " + info;
        }

        private void GenerateButtonBaseTextIfNeeded() {
            if (string.IsNullOrEmpty(generateButtonBaseText))
                generateButtonBaseText = btnGenerate?.Text ?? string.Empty;
        }

        private string BuildAnlasButtonInfo() {
            if (picProps == null || btnGenerate == null)
                return null;

            int width = picProps.Width;
            int height = picProps.Height;
            int steps = picProps.Steps;
            if (width <= 0 || height <= 0 || steps <= 0)
                return null;

            string model = BodyTools.GetEnumDescription(picProps.Model);
            string key = AnlasCostCache.BuildKey(model, width, height, steps, 1, "generate", null);

            int cost;
            bool measured = AnlasCostCache.TryGet(key, out cost);
            if (!measured)
                cost = AnlasService.EstimateCost(width, height, steps, 1, model);

            string costText = measured
                ? cost.ToString(CultureInfo.InvariantCulture)
                : "≈" + cost.ToString(CultureInfo.InvariantCulture);

            var account = AnlasService.LastAccount;
            string balanceText = account == null
                ? "?"
                : account.TotalAnlas.ToString(CultureInfo.InvariantCulture);

            // V5 走的是 Opus 额度条：优先显示本次消耗的额度百分比与剩余额度。
            bool showQuota = AnlasService.IsV5Model(model) && (account == null || account.UsagePercent > 0);
            if (showQuota) {
                double percent = AnlasService.EstimateV5UsagePercent(width, height, steps, 1);
                string quotaText = string.Format(CultureInfo.InvariantCulture, "额度 {0:0.###}% / {1}",
                    percent, account == null ? "?" : account.UsagePercent + "%");

                if (cost > 0)
                    quotaText += string.Format(CultureInfo.InvariantCulture, " + Anlas {0} / {1}", costText, balanceText);

                return quotaText;
            }

            return string.Format(CultureInfo.InvariantCulture, "Anlas {0} / {1}", costText, balanceText);
        }

        private async void RefreshAnlasBalanceAsync() {
            try {
                await QueryAnlasAsync(false);
            }
            catch (Exception ex) {
                Logger.Warn("刷新 Anlas 余额失败",
                    context: Logger.Context(("reason", ex.Message)));
            }
        }

        /// <summary>查询余额；manual 为 true 时由「查询Anlas余额」按钮触发，失败会写入错误日志。</summary>
        private async Task<bool> QueryAnlasAsync(bool manual) {
            string token = settingProps?.Token;
            if (string.IsNullOrWhiteSpace(token)) {
                if (manual)
                    Logger.Warn("查询 Anlas 失败：请先填写 NovelAI Token");
                return false;
            }

            if (manual) {
                if (DateTime.UtcNow - lastAnlasQueryUtc < AnlasQueryCooldown) {
                    Logger.Warn("查询过于频繁，请稍后再试");
                    return false;
                }

                lastAnlasQueryUtc = DateTime.UtcNow;
                btnQueryAnlas.Enabled = false;
            }

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
                }

                return true;
            }
            catch (Exception ex) {
                if (manual)
                    Logger.Error("查询 Anlas 失败", exception: ex,
                        context: Logger.Context(("endpoint", "/user/subscription")));
                else
                    Logger.Warn("刷新 Anlas 余额失败",
                        context: Logger.Context(("endpoint", "/user/subscription"), ("reason", ex.Message)));
                return false;
            }
            finally {
                if (manual)
                    btnQueryAnlas.Enabled = true;

                RefreshAnlasButtonText();
            }
        }

        private async void btnQueryAnlas_Click(object sender, EventArgs e) {
            await QueryAnlasAsync(true);
        }
    }
}
