using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DarkFogSynthesis.Diagnostics
{
    /// <summary>Explicit main-thread UI action only. Never called by import or production hooks.</summary>
    internal static class UiLayoutCapture
    {
        internal static object Capture()
        {
            var records = new List<object>();
            var errors = new List<string>();
            foreach (UITechNode node in Object.FindObjectsOfType<UITechNode>())
            {
                if (!node.gameObject.activeInHierarchy) continue;
                try
                {
                    var tech = AccessTools.Field(typeof(UITechNode), "techProto")?.GetValue(node) as TechProto;
                    var panel = AccessTools.Field(typeof(UITechNode), "panelRect")?.GetValue(node) as RectTransform;
                    if (tech == null || panel == null) continue;
                    var focus = AccessTools.Field(typeof(UITechNode), "focusState")?.GetValue(node);
                    float? amount = focus is float value ? (float?)value : null;
                    string state = !amount.HasValue ? "unknown" : Math.Abs(amount.Value) < .001f ? "normal" :
                        Math.Abs(amount.Value - 1f) < .001f ? "hover" : Math.Abs(amount.Value - 2f) < .001f ? "expanded" : "transition";
                    var canvas = CanvasFor(panel);
                    records.Add(Record("technology-" + node.GetInstanceID(), "technology", tech.ID,
                        "canvas-" + canvas.GetInstanceID() + "-page-" + tech.page, panel, canvas,
                        canvas.transform as RectTransform, state, amount));
                }
                catch (Exception error) { errors.Add("technology-" + node.GetInstanceID() + ": " + error.GetType().Name); }
            }
            foreach (UILabWindow window in Object.FindObjectsOfType<UILabWindow>())
            {
                if (!window.gameObject.activeInHierarchy) continue;
                try
                {
                    var panel = window.transform as RectTransform;
                    if (panel == null) throw new InvalidOperationException("No lab panel rectangle");
                    var canvas = CanvasFor(panel);
                    string group = "lab-" + window.GetInstanceID();
                    var custom = window.GetComponentsInChildren<RectTransform>(false)
                        .SingleOrDefault(r => r.name == "dark-fog-synthesis-matrix-choice");
                    if (custom != null && custom.gameObject.activeInHierarchy)
                    {
                        var button = custom.GetComponent<UIButton>();
                        string state = button?.button != null && button.button.interactable ? "unlocked" : "locked";
                        records.Add(Record(group + "-synthesis", "lab-choice", ProtoIds.DarkFogMatrix.Value,
                            group, custom, canvas, panel, state, null));
                    }
                    if (window.itemButtons == null) continue;
                    for (int index = 0; index < window.itemButtons.Length; index++)
                    {
                        UIButton button = window.itemButtons[index];
                        if (button == null || !button.gameObject.activeInHierarchy || !(button.transform is RectTransform rect)) continue;
                        records.Add(Record(group + "-native-" + index, "lab-native", index, group, rect, canvas, panel, "native", null));
                    }
                }
                catch (Exception error) { errors.Add("lab-" + window.GetInstanceID() + ": " + error.GetType().Name); }
            }
            return new
            {
                schemaVersion = 3,
                clipSemantics = "native-padded-rectmask2d",
                maskSelection = "native-graphic-sorting-boundaries",
                coordinateSystem = "screen-pixels-bottom-left",
                status = errors.Count == 0 ? "captured" : "partial",
                language = ObserveLanguage(),
                viewport = new { x = 0f, y = 0f, width = (float)Screen.width, height = (float)Screen.height },
                records = records.ToArray(), errors = errors.ToArray(),
                limits = "One observed frame only. Rectangle/mask bounds do not prove pixel visibility, click routing, connector clearance or all focus/language/scale states. No layout is certified."
            };
        }

        private static string ObserveLanguage()
        {
            var option = AccessTools.Field(typeof(DSPGame), "globalOption")?.GetValue(null);
            return option == null ? "unknown" : AccessTools.Field(option.GetType(), "languageLCID")?.GetValue(option)?.ToString() ?? "unknown";
        }

        private static Canvas CanvasFor(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("No parent canvas");
            canvas = canvas.rootCanvas;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
                throw new InvalidOperationException("Non-overlay canvas has no explicit camera");
            return canvas;
        }

        private static object Record(string key, string kind, int id, string group, RectTransform rect,
            Canvas canvas, RectTransform? container, string state, float? focus)
        {
            if (container == null) throw new InvalidOperationException("No container rectangle");
            var clips = RectMaskClipCapture.ScreenClips(rect, canvas)
                .Select(clip => new { x = clip.x, y = clip.y, width = clip.width, height = clip.height }).ToArray();
            return new { key, kind, prototypeId = id, group, state, focus, canvasScale = canvas.scaleFactor,
                bounds = Bounds(rect, canvas), container = Bounds(container, canvas), clips };
        }

        private static object Bounds(RectTransform rect, Canvas canvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera? camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
            float x = points.Min(p => p.x), y = points.Min(p => p.y);
            return new { x, y, width = points.Max(p => p.x) - x, height = points.Max(p => p.y) - y };
        }
    }
}
