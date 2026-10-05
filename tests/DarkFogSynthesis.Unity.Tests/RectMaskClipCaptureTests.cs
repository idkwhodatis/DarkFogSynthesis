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

    private Canvas ChildCanvas(Transform parent, bool overrideSorting)
    {
        var go = new GameObject("nested-canvas", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(parent, false);
        var nested = go.GetComponent<Canvas>();
        nested.overrideSorting = overrideSorting;
        return nested;
    }

    private Image Graphic(Transform parent, float x = 0f)
    {
        var go = new GameObject("observed-graphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(10, 10);
        rect.anchoredPosition = new Vector2(x, 0);
        return go.GetComponent<Image>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OuterRectMaskRespectsSortingBoundaryAndNativeClipping(bool overrideSorting)
    {
        var outer = Mask(root.transform, 100, 100);
        var nested = ChildCanvas(outer.transform, overrideSorting);
        var graphic = Graphic(nested.transform, 200); // Outside only the outer mask.
        Canvas.ForceUpdateCanvases();
        outer.PerformClipping();
        var clips = RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas);
        Assert.That(clips.Length, Is.EqualTo(overrideSorting ? 0 : 1));
        Assert.That(graphic.canvasRenderer.hasRectClipping, Is.EqualTo(!overrideSorting));
        if (!overrideSorting)
            Assert.That(clips[0], Is.EqualTo(RectMaskClipCapture.ScreenBounds(outer, canvas)));
        Assert.That(nested.overrideSorting, Is.EqualTo(overrideSorting));
    }

    [TestCase(false, 1f)]
    [TestCase(true, 1f)]
    [TestCase(false, 1.5f)]
    [TestCase(true, 1.5f)]
    public void InnerPaddedMaskRemainsBelowBoundary(bool overrideSorting, float scale)
    {
        canvas.scaleFactor = scale;
        var outer = Mask(root.transform, 240, 120);
        var nested = ChildCanvas(outer.transform, overrideSorting);
        var inner = Mask(nested.transform, 100, 80);
        inner.padding = new Vector4(5, 4, 6, 3);
        var graphic = Graphic(inner.transform);
        Canvas.ForceUpdateCanvases();
        inner.PerformClipping();
        var clips = RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas);
        Assert.That(clips.Length, Is.EqualTo(overrideSorting ? 1 : 2));
        CollectionAssert.Contains(clips, RectMaskClipCapture.ScreenBounds(inner, canvas));
        if (overrideSorting)
            CollectionAssert.DoesNotContain(clips, RectMaskClipCapture.ScreenBounds(outer, canvas));
        else
            CollectionAssert.Contains(clips, RectMaskClipCapture.ScreenBounds(outer, canvas));
        Assert.That(graphic.canvasRenderer.hasRectClipping, Is.True);
        Assert.That(inner.padding, Is.EqualTo(new Vector4(5, 4, 6, 3)));
    }

    [Test]
    public void IgnoredInvalidOuterMaskIsNeverProjected()
    {
        var outer = Mask(root.transform);
        outer.padding = new Vector4(1000, 1000, 1000, 1000);
        var nested = ChildCanvas(outer.transform, true);
        var graphic = Graphic(nested.transform);
        Canvas.ForceUpdateCanvases();
        Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas), Is.Empty);
        Assert.Throws<InvalidOperationException>(() => RectMaskClipCapture.ScreenBounds(outer, canvas));
    }

    [Test]
    public void SameObjectMaskDoesNotClipItsOwnGraphic()
    {
        var outer = Mask(root.transform, 240, 120);
        var graphic = Graphic(outer.transform);
        graphic.gameObject.AddComponent<RectMask2D>();
        Canvas.ForceUpdateCanvases();
        var clips = RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas);
        CollectionAssert.AreEqual(new[] { RectMaskClipCapture.ScreenBounds(outer, canvas) }, clips);
    }

    [Test]
    public void DisabledNearestMaskLeavesApplicableOuterMask()
    {
        var outer = Mask(root.transform, 240, 120);
        var inner = Mask(outer.transform);
        inner.enabled = false;
        var graphic = Graphic(inner.transform);
        Canvas.ForceUpdateCanvases();
        CollectionAssert.AreEqual(new[] { RectMaskClipCapture.ScreenBounds(outer, canvas) },
            RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas));
        Assert.That(inner.enabled, Is.False);
    }

    [Test]
    public void NonMaskableGraphicIgnoresBothMaskKinds()
    {
        var outer = Mask(root.transform);
        var stencil = Graphic(outer.transform);
        stencil.gameObject.AddComponent<UnityEngine.UI.Mask>();
        var graphic = Graphic(stencil.transform);
        graphic.maskable = false;
        Canvas.ForceUpdateCanvases();
        Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas), Is.Empty);
        Assert.That(graphic.maskable, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StencilMasksRespectSameSortingBoundary(bool overrideSorting)
    {
        var outer = Graphic(root.transform);
        var outerMask = outer.gameObject.AddComponent<UnityEngine.UI.Mask>();
        var nested = ChildCanvas(outer.transform, overrideSorting);
        var inner = Graphic(nested.transform);
        var innerMask = inner.gameObject.AddComponent<UnityEngine.UI.Mask>();
        var graphic = Graphic(inner.transform);
        Canvas.ForceUpdateCanvases();
        Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas).Length,
            Is.EqualTo(overrideSorting ? 1 : 2));
        Assert.That(outerMask.enabled && innerMask.enabled, Is.True);
    }

    [Test]
    public void GraphicOnSortingCanvasDoesNotInheritOuterMasks()
    {
        var outer = Mask(root.transform);
        var outerGraphic = outer.gameObject.AddComponent<Image>();
        outerGraphic.gameObject.AddComponent<UnityEngine.UI.Mask>();
        var nested = ChildCanvas(outer.transform, true);
        var graphic = nested.gameObject.AddComponent<Image>();
        Canvas.ForceUpdateCanvases();
        Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas), Is.Empty);
    }

    [Test]
    public void TogglingSortingAndReparentingDoesNotReuseMaskInventory()
    {
        var outer = Mask(root.transform);
        var nested = ChildCanvas(outer.transform, false);
        var graphic = Graphic(nested.transform);
        for (int i = 0; i < 3; i++)
        {
            nested.overrideSorting = false;
            Canvas.ForceUpdateCanvases();
            Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas).Length, Is.EqualTo(1));
            nested.overrideSorting = true;
            Canvas.ForceUpdateCanvases();
            Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas), Is.Empty);
        }
        graphic.transform.SetParent(root.transform, false);
        Canvas.ForceUpdateCanvases();
        Assert.That(RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas), Is.Empty);
    }

    [Test]
    public void LayoutOnlyOrInactiveGraphicRefusesCaptureWithoutAddingComponents()
    {
        var nested = ChildCanvas(root.transform, false);
        int count = nested.GetComponents<Component>().Length;
        Assert.Throws<InvalidOperationException>(() =>
            RectMaskClipCapture.ScreenClips(nested.GetComponent<RectTransform>(), canvas));
        Assert.That(nested.GetComponents<Component>().Length, Is.EqualTo(count));
        var graphic = Graphic(nested.transform);
        graphic.enabled = false;
        Assert.Throws<InvalidOperationException>(() => RectMaskClipCapture.ScreenClips(graphic.rectTransform, canvas));
        Assert.That(graphic.enabled, Is.False);
    }

}
