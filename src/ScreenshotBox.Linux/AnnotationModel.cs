using SkiaSharp;
using ScreenshotBox.App.Native;

namespace ScreenshotBox.Linux;

public enum AnnotationTool { Select, Pen, Arrow, Rectangle, Mosaic, Eraser }
public sealed record CaptureToolPreferences(string ColorHex, int PenWidth, int EraserWidth, int MosaicSize);

public sealed class AnnotationModel
{
    private sealed record Mark(AnnotationTool Tool, SKColor Color, float Width, int MosaicSize, List<SKPoint> Points)
    {
        public Mark Copy() => new(Tool, Color, Width, MosaicSize, [.. Points]);
    }
    private readonly List<Mark> _marks = [];
    private readonly Stack<Mark[]> _history = new();
    private Mark? _current;
    private bool _erasing;
    private float _eraserRadius;
    public int Count => _marks.Count;
    public bool CanUndo => _history.Count != 0;

    public void Begin(AnnotationTool tool, PixelPoint point, SKColor color, int penWidth, int eraserWidth, int mosaicSize)
    {
        if (tool == AnnotationTool.Select) return;
        _history.Push(_marks.Select(x => x.Copy()).ToArray());
        _current = null;
        _erasing = tool == AnnotationTool.Eraser;
        _eraserRadius = Math.Clamp(eraserWidth, 4, 96) / 2f;
        if (_erasing) { Erase(new(point.X, point.Y)); return; }
        _current = new Mark(tool, color, Math.Clamp(penWidth, 1, 48), Math.Clamp(mosaicSize, 4, 48), [new(point.X, point.Y)]);
        _marks.Add(_current);
    }
    public void Update(PixelPoint point)
    {
        var p = new SKPoint(point.X, point.Y);
        if (_erasing) { Erase(p); return; }
        if (_current is null) return;
        if (_current.Tool == AnnotationTool.Pen)
        {
            if (Distance(p, _current.Points[^1]) >= .75f) _current.Points.Add(p);
        }
        else if (_current.Points.Count == 1) _current.Points.Add(p);
        else _current.Points[1] = p;
    }
    public void End() { _current = null; _erasing = false; }
    public void Undo()
    {
        End();
        if (_history.TryPop(out var marks)) { _marks.Clear(); _marks.AddRange(marks); }
    }
    public void Clear() { End(); _marks.Clear(); _history.Clear(); }

    private void Erase(SKPoint point) => _marks.RemoveAll(mark => Hit(mark, point, _eraserRadius));
    private static bool Hit(Mark mark, SKPoint point, float radius)
    {
        var points = mark.Points;
        radius += mark.Width / 2;
        if (points.Count == 1) return Distance(point, points[0]) <= radius;
        if (mark.Tool is AnnotationTool.Rectangle or AnnotationTool.Mosaic)
        {
            var r = Rect(points[0], points[^1]);
            if (mark.Tool == AnnotationTool.Mosaic) return r.Contains(point.X, point.Y);
            return SegmentDistance(point, new(r.Left, r.Top), new(r.Right, r.Top)) <= radius ||
                SegmentDistance(point, new(r.Right, r.Top), new(r.Right, r.Bottom)) <= radius ||
                SegmentDistance(point, new(r.Right, r.Bottom), new(r.Left, r.Bottom)) <= radius ||
                SegmentDistance(point, new(r.Left, r.Bottom), new(r.Left, r.Top)) <= radius;
        }
        for (var i = 1; i < points.Count; i++) if (SegmentDistance(point, points[i - 1], points[i]) <= radius) return true;
        if (mark.Tool == AnnotationTool.Arrow)
        {
            var (left, right) = ArrowHead(points[0], points[^1], mark.Width);
            return SegmentDistance(point, left, points[^1]) <= radius || SegmentDistance(point, right, points[^1]) <= radius;
        }
        return false;
    }
    private static float Distance(SKPoint a, SKPoint b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static float SegmentDistance(SKPoint p, SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        if (dx * dx + dy * dy < .001f) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Distance(p, new(a.X + t * dx, a.Y + t * dy));
    }
    private static SKRect Rect(SKPoint a, SKPoint b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    private static (SKPoint Left, SKPoint Right) ArrowHead(SKPoint a, SKPoint b, float width)
    {
        var angle = MathF.Atan2(b.Y - a.Y, b.X - a.X); var length = Math.Max(12, width * 4);
        return (new(b.X - length * MathF.Cos(angle - .55f), b.Y - length * MathF.Sin(angle - .55f)),
            new(b.X - length * MathF.Cos(angle + .55f), b.Y - length * MathF.Sin(angle + .55f)));
    }

    public byte[] RenderPng(SKBitmap original, PixelRect selection)
    {
        var bounds = new PixelRect(0, 0, original.Width, original.Height);
        if (selection.IsEmpty || selection.Intersect(bounds) != selection) throw new ArgumentOutOfRangeException(nameof(selection));
        using var result = new SKBitmap(selection.Width, selection.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.White);
            // Integer crop with equal source/destination dimensions: no DPI resampling.
            canvas.DrawBitmap(original, new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom), new SKRect(0, 0, selection.Width, selection.Height));
            canvas.Translate(-selection.Left, -selection.Top);
            foreach (var mark in _marks)
            {
                using var paint = new SKPaint { Color = mark.Color, StrokeWidth = mark.Width, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
                var a = mark.Points[0]; var b = mark.Points[^1];
                switch (mark.Tool)
                {
                    case AnnotationTool.Pen:
                        if (mark.Points.Count == 1) { paint.Style = SKPaintStyle.Fill; canvas.DrawCircle(a, mark.Width / 2, paint); }
                        else { using var path = new SKPath(); path.MoveTo(a); foreach (var p in mark.Points.Skip(1)) path.LineTo(p); canvas.DrawPath(path, paint); }
                        break;
                    case AnnotationTool.Arrow:
                        canvas.DrawLine(a, b, paint); var (left, right) = ArrowHead(a, b, mark.Width);
                        canvas.DrawLine(left, b, paint); canvas.DrawLine(right, b, paint);
                        break;
                    case AnnotationTool.Rectangle: canvas.DrawRect(Rect(a, b), paint); break;
                    case AnnotationTool.Mosaic: DrawMosaic(canvas, original, Rect(a, b), mark.MosaicSize); break;
                }
            }
        }
        using var image = SKImage.FromBitmap(result);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
    private static void DrawMosaic(SKCanvas canvas, SKBitmap original, SKRect rect, int size)
    {
        var left = Math.Max(0, (int)Math.Floor(rect.Left)); var top = Math.Max(0, (int)Math.Floor(rect.Top));
        var right = Math.Min(original.Width, (int)Math.Ceiling(rect.Right)); var bottom = Math.Min(original.Height, (int)Math.Ceiling(rect.Bottom));
        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        for (var y = top; y < bottom; y += size)
        for (var x = left; x < right; x += size)
        {
            var endX = Math.Min(x + size, right); var endY = Math.Min(y + size, bottom);
            long r = 0, g = 0, b = 0; var count = 0;
            for (var iy = y; iy < endY; iy++)
            for (var ix = x; ix < endX; ix++) { var c = original.GetPixel(ix, iy); r += c.Red; g += c.Green; b += c.Blue; count++; }
            paint.Color = new SKColor((byte)(r / count), (byte)(g / count), (byte)(b / count));
            canvas.DrawRect(x, y, endX - x, endY - y, paint);
        }
    }
}

public sealed class CaptureSession
{
    public PixelRect Bounds { get; }
    public PixelRect Selection { get; private set; }
    public AnnotationModel Annotations { get; } = new();
    public AnnotationTool Tool { get; set; }
    public CaptureToolPreferences Preferences { get; set; } = new("#EF4444", 4, 24, 12);
    private PixelPoint _start;
    private PixelRect _original;
    private SelectionHandle _handle;
    private bool _dragging, _drawing;
    public bool IsDragging => _dragging;
    public CaptureSession(PixelRect bounds) => Bounds = bounds;
    public void Begin(PixelPoint point, int handleRadius = 8)
    {
        point = SelectionGeometry.Clamp(point, Bounds);
        _start = point; _original = Selection; _dragging = true;
        _drawing = Tool != AnnotationTool.Select && !Selection.IsEmpty && Selection.Contains(point);
        if (_drawing)
        {
            var p = Preferences;
            Annotations.Begin(Tool, point, SKColor.Parse(p.ColorHex), p.PenWidth, p.EraserWidth, p.MosaicSize);
        }
        else
        {
            _handle = SelectionGeometry.HitTest(Selection, point, handleRadius);
            if (_handle == SelectionHandle.None) { Selection = default; Annotations.Clear(); }
        }
    }
    public void Update(PixelPoint point)
    {
        if (!_dragging) return;
        point = SelectionGeometry.Clamp(point, Bounds);
        if (_drawing)
        {
            point = new(Math.Clamp(point.X, Selection.Left, Selection.Right - 1), Math.Clamp(point.Y, Selection.Top, Selection.Bottom - 1));
            Annotations.Update(point);
        }
        else Selection = _handle switch
        {
            SelectionHandle.None => PixelRect.FromPoints(_start, point),
            SelectionHandle.Move => SelectionGeometry.Move(_original, _start, point, Bounds),
            _ => SelectionGeometry.Resize(_original, _handle, point, Bounds)
        };
    }
    public void End() { _dragging = false; _drawing = false; Annotations.End(); }
    public void Redraw() { End(); Selection = default; Tool = AnnotationTool.Select; Annotations.Clear(); }
}
