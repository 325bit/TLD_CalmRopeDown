using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(CalmRopeDown.ModMain), "CalmRopeDown", "1.3.0", "Hemalurgic")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CalmRopeDown
{
    /// <summary>
    /// CalmRopeDown — turns descending a climbing rope into a constant-speed slide
    /// and scales the stamina/fatigue drain while descending; climbing up stays vanilla.
    /// Strategy: no method bodies are touched — only the four "down" input fields of the
    /// live PlayerClimbRope instance are overwritten (vanilla values snapshotted in memory
    /// and restored on leave/disable/quit). Saves are never written.
    /// Mechanism, field map and constraints: docs/DEVELOPMENT.md.
    /// </summary>
    public sealed class ModMain : MelonMod
    {
        public static ModMain Instance { get; private set; }
        internal ClimbTuner Tuner { get; private set; }
        internal ModSettingsPanel Settings { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            Tuner = new ClimbTuner();

            try
            {
                Settings = new ModSettingsPanel();
                Settings.AddToModSettings("CalmRopeDown");
                MelonLogger.Msg("In-game settings panel registered (Options -> Mod Settings -> CalmRopeDown)");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"ModSettings panel registration failed (edit Mods/CalmRopeDown.json instead): {e.Message}");
            }

            try
            {
                var harmony = new HarmonyLib.Harmony("CalmRopeDown");
                harmony.PatchAll(typeof(Patches));
                int count = harmony.GetPatchedMethods().Count();
                MelonLogger.Msg($"Harmony patches applied ({count} method(s))");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Harmony patching failed: {e}");
            }
        }

        public override void OnUpdate()
        {
            Tuner?.Tick();
        }

        public override void OnApplicationQuit()
        {
            Tuner?.RestoreLive(force: true);
        }
    }
}
