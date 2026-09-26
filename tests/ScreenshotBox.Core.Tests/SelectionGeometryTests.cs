using ScreenshotBox.App.Native;
using Xunit;

namespace ScreenshotBox.Core.Tests;

public sealed class SelectionGeometryTests
{
    private static readonly PixelRect Desktop = new(-1920, -1080, 5760, 3240);

    [Fact]
    public void ReverseDragProducesTheSameRectangleAsForwardDrag()
    {
        var topLeft = new PixelPoint(30, 40);
        var bottomRight = new PixelPoint(400, 300);
        Assert.Equal(new PixelRect(30, 40, 370, 260), PixelRect.FromPoints(bottomRight, topLeft));
        Assert.Equal(PixelRect.FromPoints(topLeft, bottomRight), PixelRect.FromPoints(bottomRight, topLeft));
    }

    [Fact]
    public void ASelectionCanCrossTheZeroOriginAndIncludeNegativeMonitors()
    {
        var result = PixelRect.FromPoints(new(-500, -200), new(200, 100));
        Assert.Equal(new PixelRect(-500, -200, 700, 300), result);
        Assert.Equal(result, result.Intersect(Desktop));
        Assert.True(result.Contains(new(-300, -100)));
        Assert.False(result.Contains(new(200, 100)));
    }

    [Theory]
    [InlineData(-10000, -10000, -1920, -1080)]
    [InlineData(10000, 10000, 3440, 1860)]
    public void MovingBeyondDesktopClampsPositionAndPreservesSize(int x, int y, int expectedX, int expectedY)
    {
        var original = new PixelRect(0, 0, 400, 300);
        var moved = SelectionGeometry.Move(original, new(200, 150), new(x, y), Desktop);
        Assert.Equal(new PixelRect(expectedX, expectedY, 400, 300), moved);
    }

    [Theory]
    [InlineData(SelectionHandle.West, 300, 0, 200, 100, 100, 80)]
    [InlineData(SelectionHandle.East, 20, 0, 20, 100, 80, 80)]
    [InlineData(SelectionHandle.North, 0, 300, 100, 180, 100, 120)]
    [InlineData(SelectionHandle.South, 0, 20, 100, 20, 100, 80)]
    public void AResizeHandleCanCrossItsOppositeAnchorWithoutNegativeSize(
        SelectionHandle handle, int x, int y, int left, int top, int width, int height)
    {
        var resized = SelectionGeometry.Resize(new(100, 100, 100, 80), handle, new(x, y), Desktop);
        Assert.Equal(new PixelRect(left, top, width, height), resized);
    }

    [Fact]
    public void IntersectionRetainsOnlyPixelsInsideBothRectangles()
    {
        var selection = new PixelRect(-300, -100, 500, 300);
        var monitor = new PixelRect(0, 0, 1920, 1080);
        Assert.Equal(new PixelRect(0, 0, 200, 200), selection.Intersect(monitor));
        Assert.True(selection.Intersect(new(1000, 1000, 100, 100)).IsEmpty);
    }

    [Fact]
    public void AllEightHandlesCanBeHitAndInteriorMovesTheSelection()
    {
        var selection = new PixelRect(-500, -300, 400, 200);
        var handles = SelectionGeometry.Handles(selection).ToList();
        Assert.Equal(8, handles.Count);
        Assert.Equal(8, handles.Select(x => x.Handle).Distinct().Count());
        foreach (var (handle, point) in handles)
        {
            Assert.Equal(handle, SelectionGeometry.HitTest(selection, point));
            Assert.Equal(handle, SelectionGeometry.HitTest(selection, new(point.X + 2, point.Y + 2)));
        }
        Assert.Equal(SelectionHandle.Move, SelectionGeometry.HitTest(selection, new(-300, -200)));
        Assert.Equal(SelectionHandle.None, SelectionGeometry.HitTest(selection, new(300, 200)));
    }
}
