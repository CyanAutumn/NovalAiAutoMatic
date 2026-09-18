using AutoNai3Tools.body;
using AutoNai3Tools.novalai;
using AutoNai3Tools.utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNai3Tools.utils {
    internal class Vibe {
        private static string GetEncodingAliasFromModelName(string modelName) =>
            BodyTools.GetAliasByApiName(modelName);

        private static JObject GetEncodingBlock(JObject encodingsObj, string alias) {
            if (encodingsObj == null || string.IsNullOrEmpty(alias))
                return null;

            JToken modelToken = encodingsObj[alias];
            if (modelToken == null) {
                modelToken = encodingsObj.Properties()
                    .FirstOrDefault(p => string.Equals(p.Name, alias, StringComparison.OrdinalIgnoreCase))?.Value;
            }

            return modelToken as JObject;
        }

        private static JObject GetModelEncodings(JObject jsonObj, BodyTools.Model model) {
            if (jsonObj == null)
                return null;

            if (!(jsonObj["encodings"] is JObject encodingsObj))
                return null;

            string alias = null;
            try {
                alias = BodyTools.GetAnotherName(model);
            }
            catch {
                alias = null;
            }

            JObject modelEncodingObject = GetEncodingBlock(encodingsObj, alias);

            if (modelEncodingObject == null) {
                string importModelName = jsonObj["importInfo"]?["model"]?.ToString();
                string importAlias = GetEncodingAliasFromModelName(importModelName);
                modelEncodingObject = GetEncodingBlock(encodingsObj, importAlias);
            }

            if (modelEncodingObject == null) {
                modelEncodingObject = encodingsObj.Properties().FirstOrDefault()?.Value as JObject;
            }

            return modelEncodingObject;
        }

        private static string ResolveEncoding(JObject jsonObj, BodyTools.Model model, float informationExtracted) {
            var modelEncodingObject = GetModelEncodings(jsonObj, model);
            if (modelEncodingObject == null)
                return null;

            var properties = modelEncodingObject.Properties().ToList();
            JProperty fallbackProperty = properties.FirstOrDefault();
            foreach (var prop in properties) {
                var infoToken = prop.Value["params"]?["information_extracted"];
                if (infoToken != null && infoToken.Type != JTokenType.Null) {
                    float infoValue = infoToken.Value<float>();
                    if (Math.Abs(infoValue - informationExtracted) < 0.0001f) {
                        return prop.Value["encoding"]?.ToString();
                    }
                }
            }

            if (fallbackProperty == null)
                return null;

            var importInfoValue = jsonObj["importInfo"]?["information_extracted"]?.Value<float?>();
            if (!importInfoValue.HasValue || Math.Abs(importInfoValue.Value - informationExtracted) < 0.0001f || properties.Count == 1) {
                return fallbackProperty.Value["encoding"]?.ToString();
            }

            return null;
        }

        private static List<float> GetInformationExtractedOptions(JObject jsonObj, BodyTools.Model model) {
            var results = new List<float>();
            var modelEncodingObject = GetModelEncodings(jsonObj, model);
            if (modelEncodingObject != null) {
                foreach (var prop in modelEncodingObject.Properties()) {
                    var infoToken = prop.Value["params"]?["information_extracted"];
                    if (infoToken != null && infoToken.Type != JTokenType.Null) {
                        float value = infoToken.Value<float>();
                        if (!results.Contains(value))
                            results.Add(value);
                    }
                }
            }

            if (results.Count == 0) {
                var importInfoValue = jsonObj["importInfo"]?["information_extracted"]?.Value<float?>();
                if (importInfoValue.HasValue)
                    results.Add(importInfoValue.Value);
            }

            return results;
        }

        public static List<VibeData> ParseNai4_UP_Vibe(BodyTools.Model model, List<VibeData> vibe_list, string token, string api) {
            for (int i = 0; i < vibe_list.Count; i++) {
                if (vibe_list[i].imagePath.EndsWith(".naiv4vibe")) {
                    try {
                        string jsonContent = File.ReadAllText(vibe_list[i].imagePath);
                        JObject jsonObj = JObject.Parse(jsonContent);
                        var encoding = ResolveEncoding(jsonObj, model, vibe_list[i].informationExtracted);
                        if (!string.IsNullOrEmpty(encoding)) {
                            vibe_list[i].base64Image = encoding;
                        }
                        else {
                            Logger.Warn("未找到匹配的信息抽取编码",
                                context: Logger.Context(("path", vibe_list[i].imagePath),
                                    ("informationExtracted", vibe_list[i].informationExtracted)));
                        }
                    }
                    catch (Exception ex) {
                        Logger.Error("解析 Vibe 文件失败", exception: ex,
                            context: Logger.Context(("path", vibe_list[i].imagePath)));
                    }
                }
                else {
                    var model_name = BodyTools.GetEnumDescription(model); ;
                    var vibe_name = Path.GetFileNameWithoutExtension(vibe_list[i].imagePath);
                    vibe_name = $"{vibe_name}_{model_name}_{vibe_list[i].informationExtracted}";
                    vibe_name = vibe_name.Replace(',', '_') + ".txt";
                    var vibe_path = Path.GetDirectoryName(vibe_list[i].imagePath) + $"\\{vibe_name}";

                    if (!File.Exists(vibe_path)) {
                        Logger.Info("未找到 Vibe 缓存文件，准备创建",
                            context: Logger.Context(("path", vibe_path)));

                        var base64img = Tools.ConvertImageToBase64(vibe_list[i].imagePath);
                        if (string.IsNullOrEmpty(base64img)) {
                            Logger.Error("图片转换失败，跳过该 Vibe",
                                context: Logger.Context(("path", vibe_list[i].imagePath)));
                            continue;
                        }

                        var vibeBase64img = NovalAIAPI.GetVibeID(base64img, vibe_list[i].informationExtracted, model_name, token, api);
                        File.WriteAllText(vibe_path, vibeBase64img, Encoding.UTF8);
                        Logger.Info("Vibe 缓存已创建",
                            context: Logger.Context(("path", vibe_path)));
                    }

                    vibe_list[i].base64Image = File.ReadAllText(vibe_path, Encoding.UTF8);
                }
            }
            return vibe_list;
        }

        public static List<VibeData> ParseOtherVibe(BodyTools.Model model, List<VibeData> vibe_list) {
            for (int i = 0; i < vibe_list.Count; i++) {
                var base64img = Tools.ConvertImageToBase64(vibe_list[i].imagePath);
                vibe_list[i].base64Image = base64img;
                if (string.IsNullOrEmpty(base64img)) {
                    Logger.Error("图片转换失败，跳过该 Vibe",
                        context: Logger.Context(("path", vibe_list[i].imagePath)));
                    continue;
                }
            }
            return vibe_list;
        }

        public static List<VibeData> GetVibe(BodyTools.Model model, List<VibeData> vibe_list, string token, string api = null) {
            if (model == BodyTools.Model.Nai4_Full || model == BodyTools.Model.Nai4_5_Full || model == BodyTools.Model.Nai4_5_Curated || model == BodyTools.Model.Nai4_Preview
                || model == BodyTools.Model.Nai5_Full || model == BodyTools.Model.Nai5_Curated) {
                return ParseNai4_UP_Vibe(model, vibe_list, token, api);
            }
            return ParseOtherVibe(model, vibe_list);
        }

        /// <summary>读取 .naiv4vibe 里可用的「信息抽取」档位；普通图片返回空列表。</summary>
        public static List<float> GetVibeInformationExtractedOptions(string vibePath, BodyTools.Model model) {
            var results = new List<float>();
            if (string.IsNullOrWhiteSpace(vibePath)) {
                return results;
            }

            if (!vibePath.EndsWith(".naiv4vibe", StringComparison.OrdinalIgnoreCase)) {
                results.Add(1.0f);
                return results;
            }

            try {
                string jsonContent = File.ReadAllText(vibePath);
                JObject jsonObj = JObject.Parse(jsonContent);
                results = GetInformationExtractedOptions(jsonObj, model);
            }
            catch (Exception ex) {
                Logger.Warn("读取 Vibe 文件失败",
                    context: Logger.Context(("path", vibePath), ("reason", ex.Message)));
            }

            if (results.Count == 0)
                results.Add(1.0f);

            return results;
        }

        /// <summary>导入 .naiv4vibebundle，把里面的每个 Vibe 落到本地缓存目录并返回配置项。</summary>
        public static List<VibeConfigData> ImportVibeBundle(string bundlePath, BodyTools.Model model) {
            var imported = new List<VibeConfigData>();
            if (string.IsNullOrWhiteSpace(bundlePath) || !File.Exists(bundlePath))
                return imported;

            string jsonContent = File.ReadAllText(bundlePath);
            JObject bundleObj = JObject.Parse(jsonContent);
            if (!(bundleObj["vibes"] is JArray vibes))
                return imported;

            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VibeCache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);

            string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
            foreach (JObject vibe in vibes) {
                try {
                    float informationExtracted = 1.0f;
                    var options = GetInformationExtractedOptions(vibe, model);
                    if (options.Count > 0)
                        informationExtracted = options[0];

                    float referenceStrength = vibe["importInfo"]?["strength"]?.Value<float>() ?? 0.6f;
                    string vibeName = vibe["name"]?.ToString() ?? "unknown";
                    string fileName =
                        $"bundle_{bundleName}_{Guid.NewGuid().ToString().Substring(0, 8)}.naiv4vibe";
                    string filePath = Path.Combine(cacheDir, fileName);

                    File.WriteAllText(filePath, vibe.ToString());

                    imported.Add(new VibeConfigData {
                        Enabled = true,
                        Name = $"{bundleName}-{vibeName}",
                        IE = informationExtracted,
                        RS = referenceStrength,
                        Path = filePath
                    });
                }
                catch (Exception ex) {
                    Logger.Warn("导入 Bundle 中的单个 Vibe 失败",
                        context: Logger.Context(("reason", ex.Message)));
                }
            }

            Logger.Info($"成功从 Bundle 导入 {imported.Count} 个 Vibe",
                context: Logger.Context(("path", bundlePath)));
            return imported;
        }
    }
}