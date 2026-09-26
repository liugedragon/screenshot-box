using System;
using System.Collections.Generic;

namespace ScreenshotBox.App.Native;

// All values are physical desktop pixels, including negative coordinates on secondary displays.
public readonly record struct PixelPoint(int X, int Y);
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => checked(Left + Width);
    public int Bottom => checked(Top + Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public bool Contains(PixelPoint p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
    public static PixelRect FromPoints(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    public PixelRect Intersect(PixelRect other)
    {
        int left = Math.Max(Left, other.Left), top = Math.Max(Top, other.Top);
        return new(left, top, Math.Max(0, Math.Min(Right, other.Right) - left),
            Math.Max(0, Math.Min(Bottom, other.Bottom) - top));
    }
}

public enum SelectionHandle { None, Move, NorthWest, North, NorthEast, East, SouthEast, South, SouthWest, West }

public static class SelectionGeometry
{
    public static IEnumerable<(SelectionHandle Handle, PixelPoint Point)> Handles(PixelRect r)
    {
        int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
        yield return (SelectionHandle.NorthWest, new(r.Left, r.Top));
        yield return (SelectionHandle.North, new(cx, r.Top));
        yield return (SelectionHandle.NorthEast, new(r.Right, r.Top));
        yield return (SelectionHandle.East, new(r.Right, cy));
        yield return (SelectionHandle.SouthEast, new(r.Right, r.Bottom));
        yield return (SelectionHandle.South, new(cx, r.Bottom));
        yield return (SelectionHandle.SouthWest, new(r.Left, r.Bottom));
        yield return (SelectionHandle.West, new(r.Left, cy));
    }

    public static SelectionHandle HitTest(PixelRect r, PixelPoint point, int radius = 8)
    {
        if (r.IsEmpty) return SelectionHandle.None;
        foreach (var (handle, location) in Handles(r))
            if (Math.Abs(point.X - location.X) <= radius && Math.Abs(point.Y - location.Y) <= radius)
                return handle;
        return r.Contains(point) ? SelectionHandle.Move : SelectionHandle.None;
    }

    public static PixelPoint Clamp(PixelPoint p, PixelRect bounds) =>
        new(Math.Clamp(p.X, bounds.Left, bounds.Right), Math.Clamp(p.Y, bounds.Top, bounds.Bottom));

    public static PixelRect Move(PixelRect original, PixelPoint start, PixelPoint current, PixelRect bounds)
    {
        int x = Math.Clamp(original.Left + current.X - start.X, bounds.Left, bounds.Right - original.Width);
        int y = Math.Clamp(original.Top + current.Y - start.Y, bounds.Top, bounds.Bottom - original.Height);
        return new(x, y, original.Width, original.Height);
    }

    public static PixelRect Resize(PixelRect original, SelectionHandle handle, PixelPoint point, PixelRect bounds)
    {
        point = Clamp(point, bounds);
        int left = original.Left, right = original.Right, top = original.Top, bottom = original.Bottom;
        if (handle is SelectionHandle.NorthWest or SelectionHandle.West or SelectionHandle.SouthWest) left = point.X;
        if (handle is SelectionHandle.NorthEast or SelectionHandle.East or SelectionHandle.SouthEast) right = point.X;
        if (handle is SelectionHandle.NorthWest or SelectionHandle.North or SelectionHandle.NorthEast) top = point.Y;
        if (handle is SelectionHandle.SouthWest or SelectionHandle.South or SelectionHandle.SouthEast) bottom = point.Y;
        return PixelRect.FromPoints(new(left, top), new(right, bottom));
    }
}
