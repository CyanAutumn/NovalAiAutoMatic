using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Globalization;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Net;
using AutoNai3Tools.body;
using AutoNai3Tools.novalai;
using SkiaSharp;

namespace AutoNai3Tools.utils {
    class VibeData:Object {
        public string imagePath {  get; set; }
        public string base64Image {  get; set; }
        public float informationExtracted { get; set; }
        public float referenceStrength { get; set; }
    }

    class Nai3Body {
        public string ToJson() {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, new Newtonsoft.Json.JsonSerializerSettings {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                FloatFormatHandling = Newtonsoft.Json.FloatFormatHandling.DefaultValue,
                FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Double,
                Formatting = Newtonsoft.Json.Formatting.None
            });
        }
    }

    class Nai3GenerateImageBody : Nai3Body {
        public string input { get; set; }
        public string model { get; set; }
        public string action { get; set; }
        public Nai3Parmeters parameters = null;

        public Nai3GenerateImageBody(string input = "1girl", string model = "nai-diffusion-3",
            string action = "generate", Nai3Parmeters parameters = null) {
            this.input = input;
            this.model = model;
            this.action = action;
            this.parameters = parameters;
        }
    }

    class Nai3Parmeters {
        public int params_version { get; set; } = 3;
        public int width { get; set; } = 832;
        public int height { get; set; } = 1216;
        public List<string> characterPrompts { get; set; }
        public float scale { get; set; } = 5;
        public string sampler { get; set; } = "k_euler";
        public int steps { get; set; } = 28;
        public int extra_noise_seed { get; set; } = 0;
        public int n_samples { get; set; } = 1;
        public int ucPreset { get; set; } = 0;
        public bool qualityToggle { get; set; } = true;
        public bool sm { get; set; } = false;
        public bool sm_dyn { get; set; } = false;
        public bool dynamic_thresholding { get; set; } = false;
        public int controlnet_strength { get; set; } = 1;
        public bool legacy { get; set; } = false;
        public bool add_original_image { get; set; } = true;
        public int uncond_scale { get; set; } = 1;
        public float cfg_rescale { get; set; } = 0f;
        public string noise_schedule { get; set; } = "karras";
        public bool legacy_v3_extend { get; set; } = false;
        public string image { get; set; } = null;
        public float? strength { get; set; } = null;
        public float? noise { get; set; } = null;
        public int seed { get; set; } = 0;
        public int? skip_cfg_above_sigma { get; set; }
        public bool deliberate_euler_ancestral_bug { get; set; } = false;
        public bool prefer_brownian { get; set; } = true;
        public string negative_prompt { get; set; } = null;
        public string image_format { get; set; } = null;
        public V4Prompt v4_negative_prompt { get; set; } = null;
        public V4Prompt v4_prompt { get; set; } = null;
        public List<string> reference_image_multiple { get; set; } = null;
        public List<float> reference_information_extracted_multiple { get; set; } = null;
        public List<float> reference_strength_multiple { get; set; } = null;

        public Nai3Parmeters(
            int params_version = 1,
            int width = 832,
            int height = 1216,
            float scale = 5,
            string sampler = "k_euler",
            int steps = 28,
            int n_samples = 1,
            int ucPreset = 0,
            bool qualityToggle = true,
            bool sm = false,
            bool sm_dyn = false,
            bool dynamic_thresholding = false,
            int controlnet_strength = 1,
            bool legacy = false,
            bool add_original_image = true,
            int uncond_scale = 1,
            float cfg_rescale = 0f,
            string noise_schedule = "karras",
            bool legacy_v3_extend = false,
            string image = null,
            float? strength = null,
            float? noise = null,
            int? seed = null,
            int? skip_cfg_above_sigma = null,
            string negative_prompt = null,
            List<string> reference_image_multiple = null,
            List<float> reference_information_extracted_multiple = null,
            List<float> reference_strength_multiple = null,
            List<string> characterPrompts = null,
            V4Prompt v4_negative_prompt = null,
            V4Prompt v4_prompt = null
        ) {
            this.width = width;
            this.height = height;
            this.scale = scale;
            this.sampler = sampler;
            this.steps = steps;
            this.n_samples = n_samples;
            this.ucPreset = ucPreset;
            this.qualityToggle = qualityToggle;
            this.sm = sm;
            this.sm_dyn = sm_dyn;
            this.dynamic_thresholding = dynamic_thresholding;
            this.controlnet_strength = controlnet_strength;
            this.legacy = legacy;
            this.add_original_image = add_original_image;
            this.uncond_scale = uncond_scale;
            this.cfg_rescale = cfg_rescale;
            this.noise_schedule = noise_schedule;
            this.legacy_v3_extend = legacy_v3_extend;
            this.image = image;
            this.strength = strength;
            this.noise = noise;
            Random random = new Random();
            if (seed == null) {
                this.seed = random.Next(0, 1000000000);
            }
            else {
                this.seed = (int)seed;
            }

            this.skip_cfg_above_sigma = skip_cfg_above_sigma;
            this.extra_noise_seed = this.seed;
            this.negative_prompt = negative_prompt;
            this.reference_image_multiple = new List<string>();
            this.characterPrompts = new List<string>();
            if (reference_image_multiple != null)
                this.reference_image_multiple.AddRange(reference_image_multiple);
            this.reference_information_extracted_multiple = new List<float>();
            if (reference_information_extracted_multiple != null)
                this.reference_information_extracted_multiple.AddRange(reference_information_extracted_multiple);
            this.reference_strength_multiple = new List<float>();
            if (reference_strength_multiple != null)
                this.reference_information_extracted_multiple.AddRange(reference_strength_multiple);
            if (characterPrompts != null)
                this.characterPrompts.AddRange(characterPrompts);
        }
    }

    //准备废弃
    class CaptionNo {
        public string base_caption { get; set; }
        public List<string> char_captions { get; set; }

        public CaptionNo(string base_caption, List<string> char_captions = null) {
            this.base_caption = base_caption;
            if (char_captions == null)
                this.char_captions = new List<string>();
        }
    }

    //准备废弃
    class V4PromptNo {
        public Caption caption { get; set; }
        public bool? use_coords { get; set; }
        public bool? use_order { get; set; }

        public V4PromptNo(Caption caption) {
            this.caption = caption;
        }
    }

    class Nai3DirectorToolsBody : Nai3Body {
        public int height { get; set; }
        public int width { get; set; }
        public string image { get; set; }
        public string req_type { get; set; }
        public string prompt { get; set; }
        public int? defry { get; set; }

        public Nai3DirectorToolsBody(int height, int width, string image, string req_type, string prompt = null,
            int? defry = null) {
            this.height = height;
            this.width = width;
            this.image = image;
            this.req_type = req_type;
            this.prompt = prompt;
            this.defry = defry;
        }
    }


    internal class NovalAi {
        private string GetTimestamp() {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        private string SanitizeForFileName(string value, string fallback, int maxLength = 120) {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string normalized = Regex.Replace(value, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(normalized))
                return fallback;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(normalized.Length);
            foreach (char c in normalized) {
                builder.Append(invalidChars.Contains(c) ? '_' : c);
            }

            string sanitized = builder.ToString().Trim();
            if (maxLength > 0 && sanitized.Length > maxLength) {
                sanitized = sanitized.Substring(0, maxLength).Trim();
            }

            return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
        }

        private string EnsureUniqueFileName(string directory, string baseName) {
            int counter = 1;
            string candidate = baseName;
            while (File.Exists(Path.Combine(directory, candidate + ".png")) ||
                   File.Exists(Path.Combine(directory, candidate + ".webp")) ||
                   File.Exists(Path.Combine(directory, candidate + ".txt"))) {
                candidate = $"{baseName} ({counter})";
                counter++;
            }
            return candidate;
        }

        private string BuildFileName(string prompt, long seed, string outputDirectory, SettingProperty settingProps,
            string artistSummary) {
            var format = settingProps?.OutputFileNameFormat ?? OutputFileNameFormat.NovalAI;
            string baseName;
            switch (format) {
                case OutputFileNameFormat.AllArtists:
                    var artistPart = SanitizeForFileName(artistSummary, GetTimestamp(), 230);
                    baseName = $"{artistPart}_{seed}";
                    break;
                case OutputFileNameFormat.DateTime:
                    baseName = GetTimestamp();
                    break;
                default:
                    string promptPart = SanitizeForFileName(prompt, GetTimestamp());
                    baseName = $"{promptPart} s-{seed}";
                    break;
            }

            return EnsureUniqueFileName(outputDirectory, baseName);
        }

        private static Bitmap TryDecodeWebpPreview(byte[] bytes, string imagePath) {
            try {
                using (var skData = SKData.CreateCopy(bytes))
                using (var skBitmap = SKBitmap.Decode(skData)) {
                    if (skBitmap == null) {
                        Logger.Warn("WebP 图片预览解码失败（SKBitmap.Decode 返回空），已跳过预览但仍保存文件",
                            context: Logger.Context(("path", imagePath), ("reason", "SKBitmap.Decode returned null")));
                        return null;
                    }

                    using (var image = SKImage.FromBitmap(skBitmap))
                    using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100)) {
                        if (encoded == null) {
                            Logger.Warn("WebP 图片预览转码失败（SKImage.Encode 返回空），已跳过预览但仍保存文件",
                                context: Logger.Context(("path", imagePath), ("reason", "SKImage.Encode returned null")));
                            return null;
                        }

                        using (var stream = encoded.AsStream())
                        using (var tempBitmap = new Bitmap(stream)) {
                            return new Bitmap(tempBitmap);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.Warn($"WebP 图片预览解码失败（{ex.GetType().Name}: {ex.Message}），已跳过预览但仍保存文件",
                    context: Logger.Context(("path", imagePath), ("reason", ex.Message)));
                return null;
            }
        }

        private Bitmap UnZipAndSaveImage(RestResponse response, PicProperty picProps, SettingProperty settingProps,
            string prompt, string noArtistPrompt, string artistSummary) {
            if (!response.IsSuccessful) {
                Logger.Warn("生图请求失败",
                    context: Logger.Context(("statusCode", response.StatusCode),
                        ("description", response.StatusDescription)));
                return null;
            }

            using (MemoryStream memoryStream = new MemoryStream(response.RawBytes)) {
                Tools.IsExist(picProps.OutputPath, true);
                using (ZipArchive archive = new ZipArchive(memoryStream)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        long seed = picProps.Seeds;
                        string file_name = BuildFileName(prompt, seed, picProps.OutputPath, settingProps, artistSummary);
                        string entryFileName = Path.Combine(picProps.OutputPath, file_name);

                        using (Stream entryStream = entry.Open()) {
                            using (MemoryStream entryMemoryStream = new MemoryStream()) {
                                entryStream.CopyTo(entryMemoryStream);
                                byte[] bytes = entryMemoryStream.ToArray();
                                string extension = Path.GetExtension(entry.Name);
                                if (string.IsNullOrWhiteSpace(extension))
                                    extension = ".png";

                                string imagePath = entryFileName + extension;
                                File.WriteAllBytes(imagePath, bytes);
                                entryMemoryStream.Position = 0;

                                Bitmap bitmap = null;
                                if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase)) {
                                    bitmap = TryDecodeWebpPreview(bytes, imagePath);
                                }
                                else {
                                    try {
                                        bitmap = new Bitmap(entryMemoryStream);
                                    }
                                    catch (Exception ex) {
                                        Logger.Warn("图片预览解码失败，已跳过预览但仍保存文件",
                                            context: Logger.Context(("path", imagePath), ("reason", ex.Message)));
                                    }
                                }
                                if (picProps.SavePromptToTxt)
                                    if (picProps.SavePromptToTxtNoArtist)
                                        File.WriteAllText(entryFileName + ".txt", noArtistPrompt);
                                    else
                                        File.WriteAllText(entryFileName + ".txt", prompt);
                                return bitmap;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public async Task<Bitmap> SendDirectorToolsRequestsAsync(
            string token,
            Nai3DirectorToolsBody body,
            PicProperty picProps,
            SettingProperty settingProps,
            string proxy,
            CancellationToken cancellationToken = default) {
            try {
                string baseUrl = ApiEndpoint.ResolveBaseUrl(settingProps?.Api);
                var response = await Request.PostAsync(baseUrl, "/ai/augment-image", body.ToJson(), token, proxy,
                        cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                Bitmap pic = UnZipAndSaveImage(response, picProps, settingProps, null, null, null);
                if (pic != null) {
                    Logger.Info("绘导工具生成完成",
                        context: Logger.Context(("type", body.req_type), ("height", body.height),
                            ("width", body.width)));
                }
                return pic;
            }
            catch (OperationCanceledException) {
                Logger.Info("导演工具请求被取消",
                    context: Logger.Context(("type", body?.req_type)));
                throw;
            }
            catch (Exception ex) {
                Logger.Error("DirectorTools 请求失败", exception: ex,
                    context: Logger.Context(("type", body?.req_type)));
                return null;
            }
        }

        public async Task<Bitmap> SendGenerateRequestsAsync(
            string token,
            BodyBase body,
            string noArtistPrompt,
            string artistSummary,
            PicProperty picProps,
            SettingProperty settingProps,
            string proxy,
            string originalPrompt,
            CancellationToken cancellationToken = default) {
            try {
                string baseUrl = ApiEndpoint.ResolveBaseUrl(settingProps?.Api);
                NovalAIBase naiBody = body as NovalAIBase;
                bool trackAnlas = settingProps?.AnlasTracking ?? true;
                AnlasAccount anlasBefore = trackAnlas
                    ? await AnlasService.TryQueryAsync(baseUrl, token, proxy, cancellationToken).ConfigureAwait(false)
                    : null;

                var response = await Request.PostAsync(baseUrl, "/ai/generate-image", body.ToJson(), token, proxy,
                        cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                Bitmap pic = UnZipAndSaveImage(response, picProps, settingProps, body.prompt, noArtistPrompt,
                    artistSummary);
                if (pic != null) {
                    Logger.Info("生图完成",
                        context: Logger.Context(("originalPrompt", originalPrompt ?? body?.prompt),
                            ("seed", picProps.Seeds)));
                }
                await ReportAnlasUsageAsync(baseUrl, token, proxy, naiBody, anlasBefore, cancellationToken)
                    .ConfigureAwait(false);
                return pic;
            }
            catch (OperationCanceledException) {
                Logger.Info("生成请求被取消",
                    context: Logger.Context(("originalPrompt", originalPrompt ?? body?.prompt)));
                throw;
            }
            catch (Exception ex) {
                Logger.Error("图像生成请求失败", exception: ex,
                    context: Logger.Context(("originalPrompt", originalPrompt ?? body?.prompt)));
                return null;
            }
        }

        private async Task ReportAnlasUsageAsync(string baseUrl, string token, string proxy, NovalAIBase body,
            AnlasAccount before, CancellationToken cancellationToken) {
            if (before == null)
                return;

            try {
                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                return;
            }

            AnlasAccount after = await AnlasService.TryQueryFreshAsync(baseUrl, token, proxy, cancellationToken)
                .ConfigureAwait(false);
            if (after == null)
                return;

            int consumed = before.TotalAnlas - after.TotalAnlas;
            if (consumed < 0)
                consumed = 0;

            var parameters = body?.parameters;
            string estimateText = "-";
            string parameterText = "-";
            if (parameters != null) {
                int estimate = AnlasService.EstimateCost(parameters.width, parameters.height, parameters.steps,
                    parameters.n_samples, body.model);
                estimateText = estimate.ToString(CultureInfo.InvariantCulture);

                // 实测值按「参数组合」写入本地缓存，供生成按钮预估复用（Prompt 不影响消耗）。
                AnlasCostCache.Record(
                    AnlasCostCache.BuildKey(body.model, parameters.width, parameters.height, parameters.steps,
                        parameters.n_samples, body.action, parameters.strength),
                    consumed);
                parameterText = string.Format(CultureInfo.InvariantCulture,
                    "模型:{0} | 尺寸:{1}x{2} | Steps:{3} | 张数:{4} | 采样:{5}",
                    body.model, parameters.width, parameters.height, parameters.steps, parameters.n_samples,
                    parameters.sampler);
            }

            Logger.Info(
                string.Format(CultureInfo.InvariantCulture, "Anlas 消耗：{0}（余额 {1} → {2}）| 预估 {3} | {4}",
                    consumed, before.TotalAnlas, after.TotalAnlas, estimateText, parameterText),
                context: Logger.Context(("consumedAnlas", consumed),
                    ("anlasBefore", before.TotalAnlas),
                    ("anlasAfter", after.TotalAnlas),
                    ("estimatedAnlas", estimateText),
                    ("model", body?.model),
                    ("width", parameters?.width),
                    ("height", parameters?.height),
                    ("steps", parameters?.steps),
                    ("nSamples", parameters?.n_samples)));
        }
    }
}
