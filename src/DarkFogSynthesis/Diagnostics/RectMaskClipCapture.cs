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
        /// <summary>
        /// Observe the masks applied to this rectangle's actual graphic, not every ancestor mask.
        /// Layout-only or ambiguous rectangles cannot establish masking and refuse diagnostics.
        /// This is read-only: do not add a proxy Graphic or change the installed mask hierarchy.
        /// </summary>
        internal static Rect[] ScreenClips(RectTransform rect, Canvas rootCanvas)
        {
            if (rect == null) throw new ArgumentNullException(nameof(rect));
            if (rootCanvas == null) throw new ArgumentNullException(nameof(rootCanvas));
            var graphics = rect.GetComponents<MaskableGraphic>().Where(g => g.IsActive()).ToArray();
            if (graphics.Length != 1)
                throw new InvalidOperationException("Mask capture requires one active MaskableGraphic on the observed rectangle.");
            var graphic = graphics[0];
            if (graphic.canvas == null || graphic.canvas.rootCanvas != rootCanvas)
                throw new InvalidOperationException("Graphic and observation use different root canvases.");
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && rootCanvas.worldCamera == null)
                throw new InvalidOperationException("Non-overlay canvas has no explicit camera.");
            if (!graphic.maskable) return Array.Empty<Rect>();

            var clips = new List<Rect>();
            var parentMask = MaskUtilities.GetRectMaskForClippable(graphic);
            if (parentMask != null)
            {
                // Both selection stages are required: the closest applicable mask AND the
                // applicable ancestors of that mask. uGUI handles overrideSorting boundaries,
                // disabled masks and the fact that a RectMask2D does not clip its own graphic.
                var masks = new List<RectMask2D>();
                MaskUtilities.GetRectMasksForClip(parentMask, masks);
                foreach (var mask in masks) clips.Add(ScreenBounds(mask, rootCanvas));
            }

            // Stencil masks use the same native sorting boundary, but not the rectangular
            // clipper selection algorithm. Keep only the active mask on each parent level,
            // including the stopping canvas itself as GetStencilDepth does. Their bounding
            // rectangles remain an approximation, not pixel/irregular-shape certification.
            var stopAfter = MaskUtilities.FindRootSortOverrideCanvas(rect);
            if (stopAfter == null) throw new InvalidOperationException("No native mask sorting boundary.");
            int stencilCount = 0;
            if (rect != stopAfter)
            {
                for (var parent = rect.parent; parent != null; parent = parent.parent)
                {
                    foreach (var mask in parent.GetComponents<Mask>())
                    {
                        if (!mask.MaskEnabled() || mask.graphic == null || !mask.graphic.IsActive()) continue;
                        clips.Add(ScreenTransformBounds(mask.rectTransform, rootCanvas));
                        stencilCount++;
                        break;
                    }
                    if (parent == stopAfter) break;
                }
            }
            if (stencilCount > 8 || stencilCount != MaskUtilities.GetStencilDepth(rect, stopAfter))
                throw new InvalidOperationException("Stencil mask inventory does not match the native supported depth.");
            return clips.ToArray();
        }

        private static Rect ScreenTransformBounds(RectTransform rect, Canvas rootCanvas)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
            if (points.Any(p => float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.y)))
                throw new InvalidOperationException("The stencil mask projects to nonfinite coordinates.");
            float left = points.Min(p => p.x), bottom = points.Min(p => p.y);
            float right = points.Max(p => p.x), top = points.Max(p => p.y);
            if (right <= left || top <= bottom)
                throw new InvalidOperationException("The stencil mask projects to an empty rectangle.");
            return Rect.MinMaxRect(left, bottom, right, top);
        }

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
