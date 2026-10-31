using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CalmRopeDown
{
    /// <summary>
    /// Patches <c>PlayerClimbRope</c> at three event points. Plain Prefix/Postfix only —
    /// no transpiler, so it cannot structurally conflict with other climb mods.
    ///   BeginClimbing (Prefix): make sure the descent fields are overwritten before the climb starts.
    ///   EndClimbing (Postfix): restore vanilla values the moment the player leaves the rope.
    ///   UpdatePosition (Prefix): pin the animation phase every frame so descent is constant-speed.
    /// Business logic: ClimbTuner. Entry list: docs/DEVELOPMENT.md §3.
    /// </summary>
    internal static class Patches
    {
        [HarmonyPatch(typeof(PlayerClimbRope), nameof(PlayerClimbRope.BeginClimbing))]
        [HarmonyPriority(Priority.High)]
        internal static class BeginClimbing_Prefix
        {
            public static void Prefix(PlayerClimbRope __instance)
            {
                MelonLogger.Msg($"[Climb] BeginClimbing fired, instance=0x{__instance.Pointer:X}");
                ModMain.Instance?.Tuner?.EnsureApplied(__instance);
            }
        }

        [HarmonyPatch(typeof(PlayerClimbRope), nameof(PlayerClimbRope.EndClimbing))]
        internal static class EndClimbing_Postfix
        {
            public static void Postfix(PlayerClimbRope __instance)
            {
                MelonLogger.Msg("[Climb] EndClimbing fired, restoring fields");
                ModMain.Instance?.Tuner?.RestoreLive(force: false, liveInstance: __instance);
            }
        }

        [HarmonyPatch(typeof(PlayerClimbRope), nameof(PlayerClimbRope.UpdatePosition))]
        [HarmonyPriority(Priority.High)]
        internal static class UpdatePosition_Prefix
        {
            public static void Prefix(PlayerClimbRope __instance)
                => ModMain.Instance?.Tuner?.PrepareMotion(__instance);
        }
    }

    /// <summary>
    /// Field-overwrite manager plus the per-frame phase pinning that yields a constant
    /// descent speed. Vanilla descends in bursts because the spline advance carries a
    /// |sin(phase)| envelope; pinning phase to π/2 holds the envelope at its peak.
    /// Derivation, vanilla formula and evidence: docs/DEVELOPMENT.md §5.
    /// Constraints that must not be broken: docs/DEVELOPMENT.md §10.
    /// </summary>
    internal sealed class ClimbTuner
    {
        private sealed class Slot
        {
            public IntPtr Pointer;
            public float SpeedDownRested;    // vanilla m_ClimbSpeedDownFullyRested
            public float SpeedDownExhausted; // vanilla m_ClimbSpeedDownExhausted
            public float StaminaDown;        // vanilla m_StaminaDrainPerSecondClimbingDown
            public float FatigueDown;        // vanilla m_FatigueDrainPerSecondClimbingDown
            public float UnitMotion;         // vanilla m_Anim_UnitMotion
        }

        private Slot _slot;
        private bool _applyLogged;      // first successful overwrite -> logged unconditionally (diagnostic evidence)
        private bool _exceptionLogged;  // first Tick exception -> logged unconditionally, then silent
        private int _suppressedExceptions;
        private ClimbingState _lastLoggedState = (ClimbingState)(-1);

        private static ModSettingsPanel Cfg => ModMain.Instance?.Settings;

        /// <summary>
        /// Called every frame: while enabled, keep the current instance overwritten (idempotent);
        /// while disabled, keep it restored. §10 #8.
        /// </summary>
        public void Tick()
        {
            if (Cfg == null || !Cfg.Enabled)
            {
                RestoreLive(force: false);
                return;
            }

            try
            {
                var comp = GameManager.GetPlayerClimbRopeComponent();
                if (comp == null) return;

                IntPtr ptr = comp.Pointer;
                if (_slot == null || _slot.Pointer != ptr)
                    BeginSlot(comp);

                ApplyValues(comp, _slot);
                MaybeDebugStateWatch(comp);
            }
            catch (Exception e)
            {
                _suppressedExceptions++;
                if (!_exceptionLogged)
                {
                    _exceptionLogged = true;
                    MelonLogger.Warning($"[Diagnostic] Tick exception (first one; further identical exceptions are silent): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        /// <summary>
        /// UpdatePosition prefix (Down state only): pin the stride phase so |sin| = 1 and the
        /// descent advances at a constant rate. Must run as a Harmony prefix — a MelonMod.OnUpdate
        /// hook has no guaranteed ordering against the game's Update. §10 #2.
        /// </summary>
        public void PrepareMotion(PlayerClimbRope comp)
        {
            if (Cfg == null || !Cfg.Enabled || comp == null) return;
            if (comp.m_ClimbingState != ClimbingState.Down) return; // §10 #3: never touch Up/Holding/Falling

            try
            {
                if (_slot == null || _slot.Pointer != comp.Pointer)
                    BeginSlot(comp);

                float unitMotion = _slot.UnitMotion;
                if (unitMotion <= 1e-5f) return; // degenerate data: fall back to field locking only (§10 #6)

                float A = comp.m_Anim_ClimbSpeedToAnimScalar;
                float S = TargetLineSpeed() / unitMotion; // matches the value ApplyValues locks in
                float dt = Time.deltaTime;

                // The method will compute phase = Repeat(this + dt*S*A, 2π) => stays π/2 => |sin| = 1.
                comp.m_Anim_Phase = (MathF.PI / 2f) - dt * S * A;
            }
            catch (Exception e)
            {
                if (!_exceptionLogged)
                {
                    _exceptionLogged = true;
                    MelonLogger.Warning($"[Diagnostic] PrepareMotion exception (first one): {e.GetType().Name}: {e.Message}");
                }
            }
        }

        /// <summary>BeginClimbing prefix: make sure this instance is overwritten.</summary>
        public void EnsureApplied(PlayerClimbRope comp)
        {
            if (Cfg == null || !Cfg.Enabled || comp == null) return;

            if (_slot == null || _slot.Pointer != comp.Pointer)
                BeginSlot(comp);

            ApplyValues(comp, _slot);
        }

        /// <summary>Fired by the panel's CONFIRM: disabled restores at once, edits rewrite at once.</summary>
        public void OnConfigChanged()
        {
            if (Cfg != null && Cfg.Enabled)
            {
                MelonLogger.Msg($"[Config] confirmed: enabled={Cfg.Enabled} mode={Cfg.SpeedMode} " +
                                $"target speed={TargetLineSpeed():0.##} m/s multiplier={Cfg.ConsumptionMultiplier:0.##}");
            }
            else
            {
                MelonLogger.Msg("[Config] disabled, restoring vanilla fields");
                RestoreLive(force: false);
            }
        }

        /// <summary>
        /// Restore the current instance's fields.
        /// force=true clears the slot unconditionally (application quit); otherwise restore only
        /// while the instance is still alive and its pointer matches, so we never write into a
        /// destroyed native object (§10 #5). EndClimbing passes the live instance to skip the lookup.
        /// </summary>
        public void RestoreLive(bool force, PlayerClimbRope liveInstance = null)
        {
            if (_slot == null) return;

            try
            {
                var comp = liveInstance ?? GameManager.GetPlayerClimbRopeComponent();
                if (comp != null && comp.Pointer == _slot.Pointer)
                    RestoreValues(comp, _slot);
            }
            catch (Exception)
            {
                // The component went away with its scene: a fresh instance rebuilds from the
                // prefab's vanilla values, so there is nothing left to do.
            }

            _slot = null;
            _lastLoggedState = (ClimbingState)(-1);
            if (force) MelonLogger.Msg("[CalmRopeDown] quitting: all fields restored");
        }

        // ---- internals ------------------------------------------------------

        /// <summary>Target constant line speed while descending, in m/s.</summary>
        private float TargetLineSpeed()
            => Cfg.SpeedMode == DownSpeedMode.Fixed ? Cfg.FixedDownSpeed : _slot.SpeedDownRested;

        private void BeginSlot(PlayerClimbRope comp)
        {
            _slot = new Slot
            {
                Pointer = comp.Pointer,
                SpeedDownRested = comp.m_ClimbSpeedDownFullyRested,
                SpeedDownExhausted = comp.m_ClimbSpeedDownExhausted,
                StaminaDown = comp.m_StaminaDrainPerSecondClimbingDown,
                FatigueDown = comp.m_FatigueDrainPerSecondClimbingDown,
                UnitMotion = comp.m_Anim_UnitMotion,
            };
        }

        private void ApplyValues(PlayerClimbRope comp, Slot slot)
        {
            // Lock both descent speed ends to the same value so the in-method lerp always yields
            // S = V/UnitMotion regardless of the player's condition; combined with the phase pin
            // (|sin| = 1) the per-frame advance is exactly dt*V/splineLength (§10 #7).
            float unitMotion = slot.UnitMotion;
            float lockSpeed = unitMotion > 1e-5f
                ? TargetLineSpeed() / unitMotion
                : TargetLineSpeed(); // degenerate UnitMotion: lock the fields directly; speed is at least controlled

            comp.m_ClimbSpeedDownFullyRested = lockSpeed;
            comp.m_ClimbSpeedDownExhausted = lockSpeed;

            // The drain chain is purely linear in the rate fields, so scaling them scales the
            // final drain exactly (docs/DEVELOPMENT.md §5).
            float mult = Cfg.ConsumptionMultiplier;
            comp.m_StaminaDrainPerSecondClimbingDown = slot.StaminaDown * mult;
            comp.m_FatigueDrainPerSecondClimbingDown = slot.FatigueDown * mult;

            if (!_applyLogged)
            {
                _applyLogged = true;
                MelonLogger.Msg(
                    "[CalmRopeDown] Located PlayerClimbRope and overwrote descent fields | " +
                    $"vanilla baseline: down speed rested={slot.SpeedDownRested:0.###} exhausted={slot.SpeedDownExhausted:0.###}, " +
                    $"UnitMotion={unitMotion:0.####}, stamina {slot.StaminaDown:0.###}/s, fatigue {slot.FatigueDown:0.###}/s -> " +
                    $"constant target {TargetLineSpeed():0.##} m/s (speed fields locked to {lockSpeed:0.###}), " +
                    $"stamina={slot.StaminaDown * mult:0.###}/s, fatigue={slot.FatigueDown * mult:0.###}/s");
            }
        }

        private void RestoreValues(PlayerClimbRope comp, Slot slot)
        {
            comp.m_ClimbSpeedDownFullyRested = slot.SpeedDownRested;
            comp.m_ClimbSpeedDownExhausted = slot.SpeedDownExhausted;
            comp.m_StaminaDrainPerSecondClimbingDown = slot.StaminaDown;
            comp.m_FatigueDrainPerSecondClimbingDown = slot.FatigueDown;
        }

        /// <summary>Debug mode: log live speed on every climbing-state change, to verify the constant speed and state gating.</summary>
        private void MaybeDebugStateWatch(PlayerClimbRope comp)
        {
            if (Cfg == null || !Cfg.DebugLogging) return;

            var state = comp.m_ClimbingState;
            if (state == _lastLoggedState) return;
            _lastLoggedState = state;
            MelonLogger.Msg($"[State] {state} | m_ClimbSpeed={comp.m_ClimbSpeed:0.###} m/s | " +
                            $"phase={comp.m_Anim_Phase:0.00} | " +
                            $"stamina rate (effective down value)={comp.m_StaminaDrainPerSecondClimbingDown:0.###}/s");
        }
    }
}
