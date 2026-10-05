using System;
using DarkFogSynthesis.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Optional real Unity EditMode fixture; no DSP types, game saves, or plugin initialization.
public sealed class RectMaskClipCaptureTests
{
    private GameObject root;
    private Canvas canvas;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("capture-test-canvas", typeof(RectTransform), typeof(Canvas));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(root); }

    private RectMask2D Mask(Transform parent, float width = 200f, float height = 100f)
    {
        var go = new GameObject("mask", typeof(RectTransform), typeof(RectMask2D));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(width, height);
        return go.GetComponent<RectMask2D>();
    }

    [TestCase(1f)]
    [TestCase(1.5f)]
    [TestCase(2f)]
    public void PaddingIsAppliedInRootCanvasUnitsBeforeScreenProjection(float scale)
    {
        canvas.scaleFactor = scale;
        var mask = Mask(root.transform);
        // This separate transform scale catches padding mistakenly applied in mask-local units.
        mask.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
        Canvas.ForceUpdateCanvases();
        Rect unpadded = RectMaskClipCapture.ScreenBounds(mask, canvas);
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(null, root.transform.TransformPoint(Vector3.zero));
        Vector2 xUnit = RectTransformUtility.WorldToScreenPoint(null, root.transform.TransformPoint(Vector3.right));
        Vector2 yUnit = RectTransformUtility.WorldToScreenPoint(null, root.transform.TransformPoint(Vector3.up));
        float sx = xUnit.x - origin.x, sy = yUnit.y - origin.y;
        Assert.That(sx, Is.GreaterThan(0));
        Assert.That(sy, Is.GreaterThan(0));
        Assert.That(canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
        mask.padding = new Vector4(20, 10, 30, 15);
        Rect actual = RectMaskClipCapture.ScreenBounds(mask, canvas);
        Assert.That(actual.xMin, Is.EqualTo(unpadded.xMin + 20 * sx).Within(.01f));
        Assert.That(actual.yMin, Is.EqualTo(unpadded.yMin + 10 * sy).Within(.01f));
        Assert.That(actual.xMax, Is.EqualTo(unpadded.xMax - 30 * sx).Within(.01f));
        Assert.That(actual.yMax, Is.EqualTo(unpadded.yMax - 15 * sy).Within(.01f));
    }

    [Test]
    public void NestedMasksAreIndependentPaddedConstraints()
    {
        var outer = Mask(root.transform, 240, 120);
        var inner = Mask(outer.transform);
        outer.padding = new Vector4(10, 5, 10, 5);
        Canvas.ForceUpdateCanvases();
        Rect originalOuter = RectMaskClipCapture.ScreenBounds(outer, canvas);
        Rect originalInner = RectMaskClipCapture.ScreenBounds(inner, canvas);
        inner.padding = new Vector4(20, 5, 20, 5);
        Rect paddedInner = RectMaskClipCapture.ScreenBounds(inner, canvas);
        Assert.That(RectMaskClipCapture.ScreenBounds(outer, canvas), Is.EqualTo(originalOuter));
        Assert.That(paddedInner.xMin, Is.GreaterThan(originalInner.xMin));
        Assert.That(paddedInner.xMax, Is.LessThan(originalInner.xMax));
    }

    [Test]
    public void FullyPaddedAwayMaskCannotProduceSuccessfulCapture()
    {
        var mask = Mask(root.transform);
        Canvas.ForceUpdateCanvases();
        mask.padding = new Vector4(1000, 1000, 1000, 1000);
        Assert.Throws<InvalidOperationException>(() => RectMaskClipCapture.ScreenBounds(mask, canvas));
    }
}
