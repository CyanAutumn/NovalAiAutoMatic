using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoNai3Tools.utils;

namespace AutoNai3Tools.body {
    class Nai5Full : NovalAIBase {
        public Nai5Full(Dictionary<string, object> kwargs) : base(kwargs) {
            this.model = BodyTools.GetEnumDescription(BodyTools.Model.Nai5_Full);
            ApplyNai5Parameters();
        }

        protected void ApplyNai5Parameters() {
            parameters.params_version = 4;
            parameters.legacy_v3_extend = false;
            parameters.noise_schedule = "karras";
            parameters.skip_cfg_above_sigma = null;
            parameters.dynamic_thresholding = false;
            parameters.autoSmea = false;
            parameters.use_coords = null;
            parameters.sm = null;
            parameters.sm_dyn = null;
            parameters.qualityPresetId = parameters.qualityToggle == true ? "standard" : "none";
            parameters.ucPresetId = "none";
            parameters.qualityToggle = null;
            parameters.ucPreset = null;

            bool useBrownian = UsesBrownianNoise();
            parameters.prefer_brownian = useBrownian;
            parameters.deliberate_euler_ancestral_bug = !useBrownian;

            if (parameters.v4_prompt != null)
                parameters.v4_prompt.legacy_uc = false;
            if (parameters.v4_negative_prompt != null) {
                parameters.v4_negative_prompt.use_coords = false;
                parameters.v4_negative_prompt.use_order = false;
                parameters.v4_negative_prompt.legacy_uc = false;
            }

            if (parameters.reference_image_multiple != null && parameters.reference_image_multiple.Count > 0)
                Logger.Warn("V5 官方前端未开放 Vibe Transfer，参考图可能被忽略",
                    context: Logger.Context(("count", parameters.reference_image_multiple.Count)));
        }

        // 官方规则：k_euler_ancestral 且噪声表非 native 时启用 brownian 噪声。
        private bool UsesBrownianNoise() {
            return string.Equals(parameters.sampler, "k_euler_ancestral", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(parameters.noise_schedule, "native", StringComparison.OrdinalIgnoreCase);
        }
    }
}
