using MelonLoader;
using ModSettings;

namespace CalmRopeDown
{
    /// <summary>Descent speed source for the constant-speed slide.</summary>
    public enum DownSpeedMode
    {
        /// <summary>Lock to the vanilla "fully rested" descent speed (snapshotted at runtime).</summary>
        FullyRested = 0,
        /// <summary>Lock to a user-chosen fixed speed.</summary>
        Fixed = 1,
    }

    /// <summary>
    /// In-game settings panel (ModSettings framework, bilingual labels).
    /// Values persist to Mods/CalmRopeDown.json and take effect immediately on CONFIRM.
    /// Field map: docs/DEVELOPMENT.md §8.
    /// </summary>
    internal sealed class ModSettingsPanel : JsonModSettings
    {
        [Section("功能开关 / Toggle")]
        [Name("启用登山绳下滑修改 / Enable Rope Descent Mod")]
        [Description("关闭后立即恢复原版行为（无需重启）/ Disable to instantly restore vanilla behaviour (no restart required)")]
        public bool Enabled = true;

        [Section("下滑速度 / Descent Speed")]
        [Name("速度模式 / Speed Mode")]
        [Description("匀速：锁定为原版体力充沛速度，或锁定为固定速度 / Constant speed: vanilla fully-rested speed, or a fixed speed")]
        [Choice(new string[] { "原版充沛速度 / Vanilla Rested", "固定速度 / Fixed" })]
        public DownSpeedMode SpeedMode = DownSpeedMode.FullyRested;

        [Name("固定下滑速度 (米/秒) / Fixed Descent Speed (m/s)")]
        [Description("仅“固定速度”模式下生效。匀速滑降的实际线速度 / Only applies in Fixed mode. Actual line speed while descending")]
        [Slider(0.5f, 12f, 47, NumberFormat = "{0:F2}")]
        public float FixedDownSpeed = 2f;

        [Section("下滑消耗 / Descent Drain")]
        [Name("耐力/精力消耗倍率 / Stamina & Fatigue Drain Multiplier")]
        [Description("下行时耐力与精力消耗 = 原版 × 此倍率。1.0 = 原版；0 = 不消耗 / Descent drain = vanilla × this multiplier. 1.0 = vanilla; 0 = no drain")]
        [Slider(0f, 1f, 21, NumberFormat = "{0:P0}")]
        public float ConsumptionMultiplier = 0f;

        [Section("诊断 / Diagnostics")]
        [Name("调试日志 / Debug Logging")]
        [Description("记录攀爬状态切换与实时速度（写入 MelonLoader 日志）/ Log climbing state changes and live speed to the MelonLoader log")]
        public bool DebugLogging = false;

        protected override void OnConfirm()
        {
            base.OnConfirm();
            ModMain.Instance?.Tuner?.OnConfigChanged();
        }
    }
}
