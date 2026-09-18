using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace AutoNai3Tools.body {
    public class BodyTools {
        public enum Model {
            [ModelInfo(typeof(Nai2), "nai-diffusion-2")]
            Nai2,
            [ModelInfo(typeof(Nai3), "nai-diffusion-3")]
            Nai3,
            [ModelInfo(typeof(Nai3Furry), "nai-diffusion-furry-3")]
            Nai3_Furry,
            [ModelInfo(typeof(Nai4Preview), "nai-diffusion-4-curated-preview", "v4curated")]
            Nai4_Preview,
            [ModelInfo(typeof(Nai4Full), "nai-diffusion-4-full", "v4full")]
            Nai4_Full,
            [ModelInfo(typeof(Nai4_5Curated), "nai-diffusion-4-5-curated", "v4-5curated")]
            Nai4_5_Curated,
            [ModelInfo(typeof(Nai4_5Full), "nai-diffusion-4-5-full", "v4-5full")]
            Nai4_5_Full,
            [ModelInfo(typeof(Nai5Curated), "nai-diffusion-5-curated", "v5curated")]
            Nai5_Curated,
            [ModelInfo(typeof(Nai5Full), "nai-diffusion-5-full", "v5full")]
            Nai5_Full,
        }

        private static readonly IReadOnlyDictionary<Model, ModelInfoAttribute> ModelInfos;
        private static readonly IReadOnlyDictionary<string, Model> ApiNameLookup;

        // NovelAI 已下线 V2（官方文档标记为 Retired），不再出现在模型选择列表中。
        private static readonly Model[] retiredModels = {
            Model.Nai2
        };

        private static readonly Model[] selectableModels = Enum.GetValues(typeof(Model))
            .Cast<Model>()
            .Where(model => !retiredModels.Contains(model))
            .ToArray();

        public static IReadOnlyList<Model> SelectableModels => selectableModels;

        public static bool IsRetired(Model modelName) => retiredModels.Contains(modelName);

        static BodyTools() {
            ModelInfos = Enum.GetValues(typeof(Model))
                .Cast<Model>()
                .ToDictionary(m => m, GetModelInfoAttribute);

            ApiNameLookup = ModelInfos.ToDictionary(
                kvp => kvp.Value.Description,
                kvp => kvp.Key,
                StringComparer.OrdinalIgnoreCase);
        }

        public static BodyBase GetBody(Model modelName, Dictionary<string, object> kwargs) {
            if (kwargs == null)
                throw new ArgumentNullException(nameof(kwargs));

            var info = ModelInfos[modelName];
            var instance = Activator.CreateInstance(info.BodyType, kwargs) as BodyBase;
            if (instance == null)
                throw new InvalidOperationException($"无法创建模型 {modelName} 对应的 Body 实例。");

            return instance;
        }

        public static string GetAnotherName(Model modelName) {
            var alias = ModelInfos[modelName].Alias;
            return string.IsNullOrWhiteSpace(alias) ? null : alias;
        }

        public static bool TryGetModelByApiName(string apiName, out Model model) {
            if (string.IsNullOrWhiteSpace(apiName)) {
                model = default;
                return false;
            }

            return ApiNameLookup.TryGetValue(apiName, out model);
        }

        public static string GetAliasByApiName(string apiName) {
            return TryGetModelByApiName(apiName, out var model) ? GetAnotherName(model) : null;
        }

        public static string GetEnumDescription(Enum value) {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute?.Description ?? value.ToString();
        }

        private static ModelInfoAttribute GetModelInfoAttribute(Model model) {
            var field = typeof(Model).GetField(model.ToString());
            var attribute = field?.GetCustomAttribute<ModelInfoAttribute>();
            if (attribute == null)
                throw new InvalidOperationException($"模型 {model} 缺少 ModelInfoAttribute 定义。");

            return attribute;
        }
    }

    public class ModelTypeConverter : System.ComponentModel.EnumConverter {
        public ModelTypeConverter() : base(typeof(BodyTools.Model)) { }

        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context) => true;

        public override bool GetStandardValuesExclusive(System.ComponentModel.ITypeDescriptorContext context) => true;

        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(
            System.ComponentModel.ITypeDescriptorContext context) {
            return new System.ComponentModel.TypeConverter.StandardValuesCollection(
                BodyTools.SelectableModels.ToArray());
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ModelInfoAttribute : DescriptionAttribute {
        public ModelInfoAttribute(Type bodyType, string apiName, string alias = null) : base(apiName) {
            BodyType = bodyType ?? throw new ArgumentNullException(nameof(bodyType));
            Alias = alias;
        }

        public Type BodyType { get; }
        public string Alias { get; }
    }
}
