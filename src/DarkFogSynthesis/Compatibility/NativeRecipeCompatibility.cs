using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Compatibility;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>
    /// A recipe-scoped lab choice and visual-state adapter. This never changes matrixIds,
    /// matrixPoints, native research slots, recipe execution data, or production speeds.
    /// Public-reference compilation establishes signatures, not in-game validation.
    /// </summary>
    [HarmonyPatch]
    internal static class NativeRecipeCompatibility
    {
        private const string ChoiceName = "dark-fog-synthesis-matrix-choice";
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("DarkFogSynthesis.Lab");
        private static readonly Dictionary<UILabWindow, LabChoice> Choices = new Dictionary<UILabWindow, LabChoice>();

        internal static void ValidateSupportedMatrixRegistry()
        {
            if (!NativeMatrixContract.IsSupported(LabComponent.matrixIds))
                throw new InvalidOperationException("The native research-matrix registry was changed. This build supports the six vanilla matrix IDs in their original order; global matrix overhauls need separate compatibility work. / 科研矩阵列表已改变；当前版本只支持原版六色矩阵，未修改其他 Mod 的列表。");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UILabWindow), "_OnCreate")]
        private static void OnCreate(UILabWindow __instance)
        {
            if (Choices.ContainsKey(__instance)) return;
            GameObject? clone = null;
            try
            {
                if (__instance.itemButtons == null || __instance.itemButtons.Length == 0)
                    throw new InvalidOperationException("Native lab choice buttons are unavailable.");

                // Clone a vanilla visual, but never append it to native index-addressed arrays.
                UIButton source = __instance.itemButtons[__instance.itemButtons.Length - 1];
                clone = Object.Instantiate(source.gameObject, source.transform.parent, false);
                clone.name = ChoiceName;
                clone.SetActive(false);
                var button = clone.GetComponent<UIButton>();
                if (button == null) throw new InvalidOperationException("Cloned native UIButton is unavailable.");

                // Drop all cloned native event delegates and Unity persistent click listeners.
                // Only this clone is touched. No click can reach OnItemButtonClick(6).
                ClearDelegateFields(button);
                foreach (Button unityButton in clone.GetComponentsInChildren<Button>(true))
                    unityButton.onClick = new Button.ButtonClickedEvent();
                button.data = 0;
                button.highlighted = false;
                button.updating = true;
                button.tips = default;
                button.tips.type = UIButton.ItemTipType.Other;
                button.tips.corner = 3;
                button.tips.delay = 0.25f;
                button.tips.width = 280;
                button.tips.tipTitle = "dark_fog_synthesis.recipe.dark_fog_matrix.name".Translate();
                button.tips.tipText = "dark_fog_synthesis.recipe.dark_fog_matrix.description".Translate();

                // Keep native font/art. Slot position is a candidate awaiting in-game layout QA.
                var rect = clone.GetComponent<RectTransform>();
                if (rect == null) throw new InvalidOperationException("Cloned native RectTransform is unavailable.");
                float right = float.MinValue;
                float bottom = float.MaxValue;
                foreach (UIButton existing in __instance.itemButtons)
                {
                    var existingRect = existing.transform as RectTransform;
                    if (existingRect == null) continue;
                    right = Math.Max(right, existingRect.anchoredPosition.x);
                    bottom = Math.Min(bottom, existingRect.anchoredPosition.y);
                }
                rect.anchoredPosition = new Vector2(right + Math.Max(rect.rect.width, 60f) + 12f, bottom);
                foreach (Text label in clone.GetComponentsInChildren<Text>(true)) label.text = string.Empty;
                SetChildActive(clone.transform, "fg", false);
                SetChildActive(clone.transform, "locked", false);
                var icon = clone.transform.Find("icon")?.GetComponent<Image>();
                if (icon == null) throw new InvalidOperationException("Native lab icon is unavailable.");
                foreach (Transform child in icon.transform) child.gameObject.SetActive(false);
                icon.color = Color.white;
                icon.enabled = true;

                var choice = new LabChoice(__instance, button, icon);
                button.onClick += choice.Select;
                Choices.Add(__instance, choice);
            }
            catch (Exception ex)
            {
                if (clone != null) Object.Destroy(clone);
                Log.LogError("Lab synthesis selector could not be created; no native UI arrays were changed: " + ex);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UILabWindow), "_OnUpdate")]
        private static void OnUpdate(UILabWindow __instance, PlanetFactory? ___factory,
            FactorySystem? ___factorySystem, Player? ___player, GameHistoryData? ___history)
        {
            // Harmony injects original private fields, avoiding direct publicized-field access.
            if (Choices.TryGetValue(__instance, out LabChoice choice))
                choice.UpdateContext(___factory, ___factorySystem, ___player, ___history);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UILabWindow), "_OnClose")]
        private static void OnClose(UILabWindow __instance)
        {
            if (Choices.TryGetValue(__instance, out LabChoice choice)) choice.ClearContext();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UILabWindow), "_OnDestroy")]
        private static void OnDestroy(UILabWindow __instance)
        {
            if (!Choices.TryGetValue(__instance, out LabChoice choice)) return;
            choice.Dispose();
            Choices.Remove(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LabComponent), nameof(LabComponent.InternalUpdateAssemble))]
        private static void NormalizeMatrixVisualState(ref LabComponent __instance, ref uint __result)
        {
            // The native return encodes a visual matrix index, not output count. Preserve idle
            // state and every other recipe. Native six-matrix white index is 6; no production
            // buffer, time, extra product, energy, or proliferation field is written here.
            // A rejected ID collision must not adapt somebody else's recipe with this ID.
            if (Plugin.Instance != null && Plugin.Instance.Ready &&
                __instance.recipeId == ProtoIds.DarkFogMatrix.Value && __result != 0U)
                __result = NativeMatrixContract.IsSupported(LabComponent.matrixIds) ? 6U : 0U;
        }

        internal static void Dispose()
        {
            foreach (LabChoice choice in Choices.Values) choice.Dispose();
            Choices.Clear();
        }

        private static void ClearDelegateFields(UIButton button)
        {
            foreach (FieldInfo field in typeof(UIButton).GetFields(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) field.SetValue(button, null);
            }
        }

        private static void SetChildActive(Transform parent, string name, bool active)
        {
            Transform child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private sealed class LabChoice : IDisposable
        {
            private readonly UILabWindow window;
            private readonly UIButton button;
            private readonly Image icon;
            private PlanetFactory? factory;
            private FactorySystem? system;
            private Player? player;
            private GameHistoryData? history;

            internal LabChoice(UILabWindow window, UIButton button, Image icon)
            {
                this.window = window;
                this.button = button;
                this.icon = icon;
            }

            internal void UpdateContext(PlanetFactory? factory, FactorySystem? system, Player? player, GameHistoryData? history)
            {
                this.factory = factory;
                this.system = system;
                this.player = player;
                this.history = history;
                bool ready = Plugin.Instance != null && Plugin.Instance.Ready;
                int id = window.labId;
                bool show = ready && system != null && history != null && id > 0 && id < system.labCursor &&
                    id < system.labPool.Length && system.labPool[id].id == id &&
                    !system.labPool[id].researchMode && system.labPool[id].recipeId == 0;
                button.gameObject.SetActive(show);
                if (!show) return;
                icon.sprite = LDB.items.Select(VanillaIds.Items.DarkFogMatrix.Value)?.iconSprite;
                button.tips.tipTitle = "dark_fog_synthesis.recipe.dark_fog_matrix.name".Translate();
                button.tips.tipText = "dark_fog_synthesis.recipe.dark_fog_matrix.description".Translate();
                bool unlocked = history!.RecipeUnlocked(ProtoIds.DarkFogMatrix.Value);
                if (button.button != null) button.button.interactable = unlocked;
                icon.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.65f);
            }

            internal void ClearContext()
            {
                if (button != null) button.gameObject.SetActive(false);
                factory = null;
                system = null;
                player = null;
                history = null;
            }

            internal void Select(int unused)
            {
                if (Plugin.Instance == null || !Plugin.Instance.Ready || factory == null || system == null ||
                    player == null || history == null || !ReferenceEquals(player, GameMain.mainPlayer) ||
                    !ReferenceEquals(history, GameMain.history)) return;
                int recipeId = ProtoIds.DarkFogMatrix.Value;
                if (!history.RecipeUnlocked(recipeId)) return;
                RecipeProto recipe = LDB.recipes.Select(recipeId);
                if (recipe == null || recipe.Type != ERecipeType.Research) return;

                // Native synchronization can reset other labs. Validate the entire connected
                // stack before any mutation, and refuse if even one lab needs a refund.
                if (!TryGetEmptyStack(system, window.labId, out List<int> stack))
                {
                    UIRealtimeTip.Popup("Clear every lab in this stack with the native back button first. / 请先用原版返回按钮清空整组研究站。", false);
                    return;
                }
                try
                {
                    int root = stack[0];
                    system.labPool[root].SetFunction(false, recipeId, 0, factory.entitySignPool);
                    system.SyncLabFunctions(player, root);
                    system.SyncLabForceAccMode(player, root);
                    foreach (int id in stack)
                    {
                        if (system.labPool[id].recipeId != recipeId || system.labPool[id].researchMode)
                            throw new InvalidOperationException("Native lab synchronization did not select the recipe for the entire stack.");
                    }
                    button.gameObject.SetActive(false);
                }
                catch (Exception ex)
                {
                    Log.LogError("Native lab selection failed. Inspect this copied-save session before continuing: " + ex);
                    UIRealtimeTip.Popup("Lab selection failed; inspect the BepInEx log. / 研究站配方选择失败，请查看日志。", false);
                }
            }

            public void Dispose()
            {
                ClearContext();
                if (button == null) return;
                button.onClick -= Select;
                Object.Destroy(button.gameObject);
            }
        }

        private static bool TryGetEmptyStack(FactorySystem system, int selected, out List<int> stack)
        {
            stack = new List<int>();
            if (selected <= 0 || selected >= system.labCursor || selected >= system.labPool.Length ||
                system.labPool[selected].id != selected) return false;
            var preceding = new Dictionary<int, int>();
            var ambiguous = new HashSet<int>();
            for (int i = 1; i < system.labCursor && i < system.labPool.Length; i++)
            {
                if (system.labPool[i].id != i || system.labPool[i].nextLabId <= 0) continue;
                int next = system.labPool[i].nextLabId;
                if (preceding.ContainsKey(next)) ambiguous.Add(next);
                else preceding.Add(next, i);
            }
            var seen = new HashSet<int>();
            int root = selected;
            while (preceding.TryGetValue(root, out int previous))
            {
                if (ambiguous.Contains(root) || !seen.Add(root)) return false;
                root = previous;
            }
            seen.Clear();
            for (int id = root; id != 0; id = system.labPool[id].nextLabId)
            {
                if (id <= 0 || id >= system.labCursor || id >= system.labPool.Length ||
                    !seen.Add(id) || ambiguous.Contains(id)) return false;
                LabComponent lab = system.labPool[id];
                if (lab.id != id || lab.recipeId != 0 || lab.researchMode || lab.techId != 0 ||
                    lab.replicating || lab.time != 0 || lab.extraTime != 0 ||
                    lab.cycleCount != 0 || lab.extraCycleCount != 0 ||
                    HasAnyValue(lab.served) || HasAnyValue(lab.incServed) || HasAnyValue(lab.produced) ||
                    HasAnyValue(lab.matrixServed) || HasAnyValue(lab.matrixIncServed)) return false;
                stack.Add(id);
            }
            return stack.Contains(selected);
        }

        private static bool HasAnyValue(int[]? values)
        {
            if (values == null) return false;
            foreach (int value in values) if (value != 0) return true;
            return false;
        }
    }
}
