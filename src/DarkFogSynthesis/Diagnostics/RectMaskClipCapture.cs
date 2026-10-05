using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DarkFogSynthesis.Diagnostics
{
    /// <summary>Unity-only geometry adapter, also compiled unchanged by the optional EditMode fixture.</summary>
    internal static class RectMaskClipCapture
    {
        internal static Rect ScreenBounds(RectMask2D mask, Canvas rootCanvas)
        {
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (rootCanvas == null) throw new ArgumentNullException(nameof(rootCanvas));
            var maskCanvas = mask.GetComponentInParent<Canvas>();
            if (maskCanvas == null || maskCanvas.rootCanvas != rootCanvas)
                throw new InvalidOperationException("Mask and observation use different root canvases.");
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && rootCanvas.worldCamera == null)
                throw new InvalidOperationException("Non-overlay canvas has no explicit camera.");

            // uGUI applies padding to canvasRect in ROOT-CANVAS units, not mask-local
            // units and not screen pixels. Delegate that calculation to the installed uGUI.
            // One mask per record preserves nested masks as independent clip constraints.
            bool valid;
            Rect clip = Clipping.FindCullAndClipWorldRect(new List<RectMask2D> { mask }, out valid);
            if (!valid || clip.width <= 0f || clip.height <= 0f)
                throw new InvalidOperationException("The padded mask has no visible rectangle.");
            var local = new[] {
                new Vector3(clip.xMin, clip.yMin), new Vector3(clip.xMin, clip.yMax),
                new Vector3(clip.xMax, clip.yMax), new Vector3(clip.xMax, clip.yMin)
            };
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            var points = local.Select(p => RectTransformUtility.WorldToScreenPoint(camera,
                rootCanvas.transform.TransformPoint(p))).ToArray();
            if (points.Any(p => float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y)))
                throw new InvalidOperationException("The padded mask projects to nonfinite coordinates.");
            float left = points.Min(p => p.x), bottom = points.Min(p => p.y);
            float right = points.Max(p => p.x), top = points.Max(p => p.y);
            if (right <= left || top <= bottom)
                throw new InvalidOperationException("The padded mask projects to an empty rectangle.");
            return Rect.MinMaxRect(left, bottom, right, top);
        }
    }
}
