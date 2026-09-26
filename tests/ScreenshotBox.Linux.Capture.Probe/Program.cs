using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ScreenshotBox.Linux;
using ScreenshotBox.Linux.Native;
using ScreenshotBox.App.Native;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
using P = ScreenshotBox.App.Native.PixelPoint;
using R = ScreenshotBox.App.Native.PixelRect;

return AppBuilder.Configure<ProbeApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
public sealed class ProbeApp:Application
{
 public override void Initialize()=>Styles.Add(new FluentTheme());
 public override void OnFrameworkInitializationCompleted()
 {
  base.OnFrameworkInitializationCompleted();
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
  {
   life.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   Dispatcher.UIThread.Post(async()=>
   {
    try {await Probe.Run(life); life.Shutdown(0);}
    catch(Exception e){Console.Error.WriteLine(e);life.Shutdown(1);}
   });
  }
 }
}
public static class Probe
{
 private static readonly List<object> Checks=[];
 private static string Output="";
 private static void Check(string name,bool value)
 {
  Checks.Add(new{name,passed=value});Console.WriteLine(name+": "+value);
  if(!value)throw new Exception(name);
 }
 public static async Task Run(IClassicDesktopStyleApplicationLifetime life)
 {
  Output=Environment.GetEnvironmentVariable("SCREENSHOTBOX_CAPTURE_PROBE_OUTPUT")??"artifacts/linux-capture-validation";
  Directory.CreateDirectory(Output);
  try
  {
   var session=new CaptureSession(new R(0,0,640,420));
   session.Begin(new P(180,140));session.Update(new P(40,30));session.End();
   Check("Reverse drag keeps physical rectangle",session.Selection==new R(40,30,140,110));
   var original=session.Selection;
   foreach(var(handle,location)in SelectionGeometry.Handles(original))
   {
    var s=new CaptureSession(session.Bounds);s.Begin(new P(40,30));s.Update(new P(180,140));s.End();
    var p=handle switch {SelectionHandle.NorthWest=>new P(25,20),SelectionHandle.North=>new P(110,20),SelectionHandle.NorthEast=>new P(200,20),SelectionHandle.East=>new P(200,85),SelectionHandle.SouthEast=>new P(200,160),SelectionHandle.South=>new P(110,160),SelectionHandle.SouthWest=>new P(25,160),_=>new P(25,85)};
    s.Begin(location);s.Update(p);s.End();
    Check("Resize "+handle,s.Selection==SelectionGeometry.Resize(original,handle,p,session.Bounds));
   }
   session.Begin(new P(90,85));session.Update(new P(-100,-100));session.End();
   Check("Move clamps selection without changing size",session.Selection==new R(0,0,140,110));
   session.Begin(new P(140,55));session.Update(new P(5,55));session.End();
   Check("Resize can cross opposite edge",session.Selection==new R(0,0,5,110));
   using var originalBitmap=Pattern(640,420);
   var crop=new R(37,29,83,67);
   using(var image=SKBitmap.Decode(new AnnotationModel().RenderPng(originalBitmap,crop)))
   {
    Check("Crop has exact physical pixel dimensions",image.Width==83&&image.Height==67);
    var same=true;for(var y=0;y<67;y++)for(var x=0;x<83;x++)same&=image.GetPixel(x,y)==originalBitmap.GetPixel(x+37,y+29);
    Check("Crop pixels match source at every position",same);
   }
   var annotations=new AnnotationModel();var bounds=new R(0,0,640,420);
   annotations.Begin(AnnotationTool.Pen,new P(30,30),SKColors.Red,10,24,12);annotations.Update(new P(130,30));annotations.End();
   using(var png=SKBitmap.Decode(annotations.RenderPng(originalBitmap,bounds)))Check("Pen produces colored pixels with configured width",png.GetPixel(75,30).Red>240&&png.GetPixel(75,33).Red>240);
   annotations.Begin(AnnotationTool.Eraser,new P(75,30),SKColors.Black,4,24,12);annotations.End();
   Check("Eraser removes annotations rather than the image",annotations.Count==0);
   using(var png=SKBitmap.Decode(annotations.RenderPng(originalBitmap,bounds)))Check("Erased area preserves original screenshot",png.GetPixel(75,30)==originalBitmap.GetPixel(75,30));
   annotations.Undo();Check("Undo restores erased object",annotations.Count==1);
   foreach(var tool in new[]{AnnotationTool.Arrow,AnnotationTool.Rectangle,AnnotationTool.Mosaic})
   {
    var m=new AnnotationModel();m.Begin(tool,new P(150,140),SKColors.LimeGreen,6,24,12);m.Update(new P(220,180));m.End();
    using var png=SKBitmap.Decode(m.RenderPng(originalBitmap,bounds));var changed=0;
    for(var y=130;y<190;y++)for(var x=140;x<230;x++)if(png.GetPixel(x,y)!=originalBitmap.GetPixel(x,y))changed++;
    Check(tool+" changes real pixels",changed>30);m.Undo();Check(tool+" can be undone",m.Count==0);
   }
   using(var fixture=new MarkerWindow())
   {
    await Task.Delay(150);
    using var frame=X11Desktop.Capture();
    var origin=fixture.Origin;
    Console.WriteLine($"X11 frame={frame.Bitmap.Width}x{frame.Bitmap.Height}; marker origin={origin.X},{origin.Y}");
    Check("X11 captures actual drawable color",frame.Bitmap.GetPixel(origin.X+20,origin.Y+20)==new SKColor(0x37,0x6D,0xA8));
    var markerCrop=new R(origin.X+12,origin.Y+12,64,48);
    using var png=SKBitmap.Decode(new AnnotationModel().RenderPng(frame.Bitmap,markerCrop));
    Check("X11 region dimensions and location match captured pixels",png.Width==64&&png.Height==48&&png.GetPixel(0,0)==frame.Bitmap.GetPixel(markerCrop.Left,markerCrop.Top)&&png.GetPixel(63,47)==frame.Bitmap.GetPixel(markerCrop.Right-1,markerCrop.Bottom-1));
    // Owner covers the marker; CaptureAsync must hide it before taking the root image.
    var owner=new Window{Width=260,Height=180,Position=new Avalonia.PixelPoint(origin.X,origin.Y),WindowDecorations=WindowDecorations.None,Background=Brushes.Red};owner.Show();owner.Position=new Avalonia.PixelPoint(origin.X,origin.Y);await Task.Delay(200);
    using(var shown=X11Desktop.Capture())Check("Owner is visible before capture",shown.Bitmap.GetPixel(origin.X+20,origin.Y+20).Red>230&&shown.Bitmap.GetPixel(origin.X+20,origin.Y+20).Blue<20);
    var captureTask=CaptureWindow.CaptureAsync(owner);
    CaptureWindow? overlay=null;
    for(var i=0;i<40&&overlay is null;i++){await Task.Delay(50);overlay=life.Windows.OfType<CaptureWindow>().SingleOrDefault();}
    if(overlay is null)throw new Exception("CaptureAsync did not open an overlay");
    overlay.Session.Begin(new P(markerCrop.Left,markerCrop.Top));overlay.Session.Update(new P(markerCrop.Right,markerCrop.Bottom));overlay.Session.End();overlay.Refresh();
    overlay.Complete(CaptureAction.SaveAndCopy);var result=await captureTask;
    using(var saved=SKBitmap.Decode(result!.Png))Check("Owner is hidden before actual screenshot acquisition",saved.GetPixel(8,8)==new SKColor(0x37,0x6D,0xA8));
    Check("Successful capture leaves owner hidden",!owner.IsVisible);owner.Close();
   }
   using(var first=new GlobalHotkeyService(()=>{}))using(var second=new GlobalHotkeyService(()=>{}))
   {
    Check("X11 registers custom modifier shortcut",first.TrySet("Ctrl+Alt+Shift+F8",out _));
    Check("Second X11 client registers another shortcut",second.TrySet("Ctrl+Alt+Shift+F9",out _));
    Check("Occupied shortcut fails and preserves previous registration",!second.TrySet("Ctrl+Alt+Shift+F8",out var conflict)&&conflict.Length>0&&second.CurrentShortcut=="Ctrl+Alt+Shift+F9");
    Check("Reserved combination is rejected without replacing active shortcut",!second.TrySet("Super+L",out _)&&second.CurrentShortcut=="Ctrl+Alt+Shift+F9");
    Check("Invalid combination preserves previous shortcut",!second.TrySet("Ctrl+NoSuchKey",out _)&&second.CurrentShortcut=="Ctrl+Alt+Shift+F9");
    Check("Shortcut can be changed",first.TrySet("Alt+A",out _)&&first.CurrentShortcut=="Alt+A");
   }
   await Screenshots();
   Check("Capture tool preferences retain custom sizes and color",CaptureWindow.GetToolPreferences() is {ColorHex:"#3B82F6",PenWidth:8,EraserWidth:30,MosaicSize:14});
  }
  finally
  {
   await File.WriteAllTextAsync(Path.Combine(Output,"result.json"),JsonSerializer.Serialize(new{platform="Linux x64 / X11",display=Environment.GetEnvironmentVariable("DISPLAY"),checks=Checks,scope="X11 native capture and Avalonia component checks under WSL. Native Ubuntu desktops, Wayland and Windows global hotkeys are outside this test."},new JsonSerializerOptions{WriteIndented=true}));
  }
  Console.WriteLine("Passed "+Checks.Count+" capture checks.");
 }
 private static SKBitmap Pattern(int width,int height)
 {
  var bitmap=new SKBitmap(width,height);for(var y=0;y<height;y++)for(var x=0;x<width;x++)bitmap.SetPixel(x,y,new SKColor((byte)(x%256),(byte)(y%256),(byte)((x+y)%256)));return bitmap;
 }
 private static async Task Screenshots()
 {
  CaptureWindow.ConfigureTools("#3B82F6",8,30,14);
  using var bitmap=Demo();
  var overlay=new CaptureWindow(new DesktopFrame(bitmap.Copy()));overlay.Show();await Task.Delay(120);
  overlay.Session.Begin(new P(64,68));overlay.Session.Update(new P(880,540));overlay.Session.End();
  foreach(var tool in new[]{AnnotationTool.Pen,AnnotationTool.Arrow,AnnotationTool.Rectangle,AnnotationTool.Mosaic})
  {
   overlay.Session.Tool=tool;
   var a=tool switch{AnnotationTool.Pen=>new P(108,446),AnnotationTool.Arrow=>new P(700,430),AnnotationTool.Rectangle=>new P(110,210),_=>new P(650,180)};
   var b=tool switch{AnnotationTool.Pen=>new P(290,446),AnnotationTool.Arrow=>new P(550,400),AnnotationTool.Rectangle=>new P(570,340),_=>new P(800,240)};
   overlay.Session.Begin(a);overlay.Session.Update(b);overlay.Session.End();
  }
  overlay.Session.Tool=AnnotationTool.Select;overlay.Refresh();await Task.Delay(100);
  using(var render=new RenderTargetBitmap(new PixelSize(960,720))){render.Render(overlay);render.Save(Path.Combine(Output,"capture-annotated.png"), new PngBitmapEncoderOptions());}
  var toolbar=overlay.GetVisualDescendants().OfType<Border>().Single(x=>x.Child is StackPanel p&&p.Children.Count==2&&p.Children[0] is WrapPanel);
  var toolbarOrigin=toolbar.TranslatePoint(new Point(0,0),overlay)!.Value;
  Check("Capture toolbar remains within the visible window",toolbarOrigin.X>=0&&toolbarOrigin.Y>=0&&toolbarOrigin.X+toolbar.Bounds.Width<=overlay.Bounds.Width&&toolbarOrigin.Y+toolbar.Bounds.Height<=overlay.Bounds.Height);
  Check("Real overlay remains laid out at root pixel scale",overlay.Bounds.Width>0&&overlay.Session.Selection.Width==816);
  overlay.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});
  Check("Escape cancels without producing PNG",await overlay.Completion is null);overlay.DisposeFrame();
  var enter=new CaptureWindow(new DesktopFrame(bitmap.Copy()));enter.Show();await Task.Delay(50);
  enter.Session.Begin(new P(80,80));enter.Session.Update(new P(180,140));enter.Session.End();enter.Refresh();
  enter.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});
  var saved=await enter.Completion;
  Check("Enter returns SaveAndCopy with actual PNG",saved?.Action==CaptureAction.SaveAndCopy&&SKBitmap.Decode(saved.Png) is {Width:100,Height:60});enter.DisposeFrame();
  foreach(var action in new[]{CaptureAction.CopyOnly,CaptureAction.Export})
  {
   var w=new CaptureWindow(new DesktopFrame(bitmap.Copy()));w.Show();await Task.Delay(30);w.Session.Begin(new P(80,80));w.Session.Update(new P(180,140));w.Session.End();w.Complete(action);
   Check(action+" returns separate action and PNG",(await w.Completion)?.Action==action);w.DisposeFrame();
  }
  var empty=new CaptureWindow(new DesktopFrame(bitmap.Copy()));empty.Show();empty.Complete(CaptureAction.SaveAndCopy);Check("Empty selection does not complete a capture",!empty.Completion.IsCompleted);empty.Cancel();empty.DisposeFrame();
 }
 private static SKBitmap Demo()
 {
  var b=new SKBitmap(960,720);using var c=new SKCanvas(b);c.Clear(new SKColor(0xE8,0xEE,0xF3));
  using var p=new SKPaint{IsAntialias=true};void Rect(float x,float y,float w,float h,SKColor color){p.Color=color;c.DrawRect(x,y,w,h,p);}
  using var font=new SKFont(SKTypeface.FromFamilyName("DejaVu Sans"),16);
  void Text(string text,float x,float y,float size,SKColor color){font.Size=size;p.Color=color;c.DrawText(text,x,y,SKTextAlign.Left,font,p);}
  Rect(64,68,816,472,SKColors.White);Rect(64,68,816,88,new SKColor(0x16,0x35,0x50));Text("NORTHSIDE SUPPLY",92,104,22,SKColors.White);Text("Order receipt  /  Sample document",92,135,14,new SKColor(0xBB,0xD1,0xE2));
  Text("USB-C desk dock",108,193,24,new SKColor(0x16,0x35,0x50));Text("Order # NS-240927-013",108,223,16,SKColors.Black);Text("Purchased: 27 Sep 2026",108,251,16,SKColors.Black);
  Rect(650,178,154,84,new SKColor(0x15,0x80,0x3D));Text("WARRANTY",672,211,14,SKColors.White);Text("24 months",672,239,20,SKColors.White);
  Rect(110,285,460,48,new SKColor(0xF1,0xF5,0xF9));Text("1 x Dock  /  65W charger included",124,314,16,SKColors.Black);
  Rect(110,361,460,66,new SKColor(0xDB,0xEA,0xFE));Text("Total paid",126,388,15,new SKColor(0x1E,0x40,0xAF));Text("$ 79.90",432,407,26,new SKColor(0x1E,0x40,0xAF));
  Text("Keep this receipt for warranty support.",108,463,15,new SKColor(0x47,0x55,0x69));Text("Synthetic sample  /  No personal information",108,512,12,new SKColor(0x64,0x74,0x8B));return b;
 }
}
public sealed class MarkerWindow:IDisposable
{
 private readonly IntPtr _display;private readonly nuint _window;
 public MarkerWindow(){_display=XOpenDisplay(IntPtr.Zero);if(_display==IntPtr.Zero)throw new Exception("X11 unavailable");_window=XCreateSimpleWindow(_display,XDefaultRootWindow(_display),100,80,260,180,0,0,0x376DA8);XMapRaised(_display,_window);XSync(_display,0);}
 public P Origin{get{XTranslateCoordinates(_display,_window,XDefaultRootWindow(_display),0,0,out var x,out var y,out _);return new P(x,y);}}
 public void Dispose(){XDestroyWindow(_display,_window);XSync(_display,0);XCloseDisplay(_display);}
 [DllImport("libX11.so.6")]private static extern int XTranslateCoordinates(IntPtr display,nuint source,nuint destination,int sourceX,int sourceY,out int destinationX,out int destinationY,out nuint child);
 [DllImport("libX11.so.6")]private static extern IntPtr XOpenDisplay(IntPtr name);
 [DllImport("libX11.so.6")]private static extern nuint XDefaultRootWindow(IntPtr display);
 [DllImport("libX11.so.6")]private static extern nuint XCreateSimpleWindow(IntPtr display,nuint parent,int x,int y,uint width,uint height,uint border,nuint borderPixel,nuint backgroundPixel);
 [DllImport("libX11.so.6")]private static extern int XMapRaised(IntPtr display,nuint window);
 [DllImport("libX11.so.6")]private static extern int XDestroyWindow(IntPtr display,nuint window);
 [DllImport("libX11.so.6")]private static extern int XSync(IntPtr display,int discard);
 [DllImport("libX11.so.6")]private static extern int XCloseDisplay(IntPtr display);
}
