using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using FastColoredTextBoxNS;

namespace NioxV2
{
    // =====================================================================
    //  THEME (black / gray / white)
    // =====================================================================
    internal static class AppInfo { public const string Version = "8.5"; }

    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 10, 10);
        public static readonly Color Panel = Color.FromArgb(18, 18, 18);
        public static readonly Color Card = Color.FromArgb(27, 27, 27);
        public static readonly Color Hover = Color.FromArgb(42, 42, 42);
        public static readonly Color CodeBg = Color.FromArgb(8, 8, 8);
        public static readonly Color Line = Color.FromArgb(78, 78, 78);       // gray border
        public static readonly Color LineSoft = Color.FromArgb(46, 46, 46);
        public static readonly Color Green = Color.FromArgb(74, 222, 128);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color DefaultAccent = Color.FromArgb(240, 240, 240);
        public static readonly Color DefaultText = Color.FromArgb(240, 240, 240);

        public static Color Accent = DefaultAccent;
        public static Color Text = DefaultText;
        public static Color Muted = Color.FromArgb(138, 138, 138);
        public static Color Dim = Color.FromArgb(88, 88, 88);

        public static event Action Changed;

        public static void Raise() { var h = Changed; if (h != null) h(); }
        public static void SetAccent(Color c) { Accent = c; Raise(); }
        public static void SetText(Color c)
        {
            Text = c;
            Muted = G.Mix(c, Bg, 0.45f);
            Dim = G.Mix(c, Bg, 0.72f);
            Raise();
        }
        public static void Reset() { Accent = DefaultAccent; SetText(DefaultText); }

        // translucent version of a surface color when a background image is active
        public static Color Surf(Color c)
        {
            return Backdrop.Has ? G.Alpha(c, Backdrop.PanelAlpha) : c;
        }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static Font Get(string fam, float size, FontStyle st)
        {
            string k = fam + "|" + size + "|" + (int)st;
            Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font(fam, size, st); fonts[k] = f; }
            return f;
        }
        public static Font UI(float s, FontStyle st = FontStyle.Regular) { return Get("Segoe UI", s, st); }
        public static Font Code(float s) { return Get("Consolas", s, FontStyle.Regular); }
    }

    internal static class G
    {
        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float rad, Color fill, Color? line = null)
        {
            using (var p = Round(r, rad))
            {
                if (fill.A > 0) using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (line.HasValue && line.Value.A > 0) using (var pen = new Pen(line.Value)) g.DrawPath(pen, p);
            }
        }

        public static Color Alpha(Color c, int a)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // vector "file" icon (used for scripts in tabs / explorer / start page)
        public static void FileGlyph(Graphics g, float x, float y, float s, Color c)
        {
            float fw = s * 0.74f, fh = s, fx = x + (s - fw) / 2f, fold = s * 0.30f;
            float w = Math.Max(1.7f, s / 10f);
            using (var p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(p, new[]
                {
                    new PointF(fx, y + 1), new PointF(fx + fw - fold, y + 1), new PointF(fx + fw, y + 1 + fold),
                    new PointF(fx + fw, y + fh - 1), new PointF(fx, y + fh - 1)
                });
                g.DrawLines(p, new[]
                {
                    new PointF(fx + fw - fold, y + 1), new PointF(fx + fw - fold, y + 1 + fold), new PointF(fx + fw, y + 1 + fold)
                });
                g.DrawLine(p, fx + fw * 0.24f, y + fh * 0.58f, fx + fw * 0.76f, y + fh * 0.58f);
                g.DrawLine(p, fx + fw * 0.24f, y + fh * 0.78f, fx + fw * 0.60f, y + fh * 0.78f);
            }
        }

        public static void Chevron(Graphics g, float cx, float cy, float s, Color c, bool down)
        {
            using (var p = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (down)
                    g.DrawLines(p, new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
                else
                    g.DrawLines(p, new[] { new PointF(cx - s / 2, cy - s), new PointF(cx + s / 2, cy), new PointF(cx - s / 2, cy + s) });
            }
        }

        public static void Check(Graphics g, float cx, float cy, float s, Color c, float w)
        {
            using (var p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(p, new[] { new PointF(cx - s, cy), new PointF(cx - s * 0.3f, cy + s * 0.7f), new PointF(cx + s, cy - s * 0.6f) });
        }
    }

    internal static class N
    {
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessage")]
        public static extern IntPtr SendMessageStr(IntPtr h, int m, IntPtr w, string l);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr h, string app, string idList);

        public static void DarkScroll(Control c)
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        }
    }

    // =====================================================================
    //  SAVED SETTINGS (settings.ini next to the .exe)
    // =====================================================================
    internal static class Prefs
    {
        static readonly string FilePath = Path.Combine(Application.StartupPath, "settings.ini");
        static readonly Dictionary<string, string> d = new Dictionary<string, string>();

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch { }
        }

        public static void Save()
        {
            try { File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "=" + kv.Value).ToArray()); }
            catch { }
        }

        public static string Str(string k, string def) { string v; return d.TryGetValue(k, out v) ? v : def; }
        public static int Int(string k, int def) { int r; return int.TryParse(Str(k, ""), out r) ? r : def; }
        public static bool Bool(string k, bool def) { string v = Str(k, ""); return v == "" ? def : v == "1"; }
        public static void Set(string k, string v) { d[k] = v; }
        public static void Set(string k, int v) { d[k] = v.ToString(); }
        public static void Set(string k, bool v) { d[k] = v ? "1" : "0"; }
    }

    // =====================================================================
    //  CUSTOM BACKGROUND IMAGE
    // =====================================================================
    internal static class Backdrop
    {
        static Bitmap prepared, cache;
        public static Control Anchor;
        public static int Dim = 55;        // 0..90 (% of black over the image)
        public static int PanelPct = 78;   // 30..100 (% opacity of panels)

        public static bool Has { get { return prepared != null; } }
        public static int PanelAlpha { get { return PanelPct * 255 / 100; } }

        public static bool Load(string file)
        {
            try
            {
                Bitmap bmp;
                using (var raw = Image.FromFile(file))
                {
                    float s = Math.Min(1f, 1920f / Math.Max(raw.Width, raw.Height));
                    int w = Math.Max(1, (int)(raw.Width * s)), h = Math.Max(1, (int)(raw.Height * s));
                    bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(raw, 0, 0, w, h);
                    }
                }
                if (prepared != null) prepared.Dispose();
                prepared = bmp;
                ResetCache();
                return true;
            }
            catch { return false; }
        }

        public static void Clear()
        {
            if (prepared != null) prepared.Dispose();
            prepared = null;
            ResetCache();
        }

        public static void ResetCache()
        {
            if (cache != null) { cache.Dispose(); cache = null; }
        }

        static void Build(Size sz)
        {
            ResetCache();
            if (prepared == null || sz.Width < 1 || sz.Height < 1) return;
            var bmp = new Bitmap(sz.Width, sz.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                float s = Math.Max((float)sz.Width / prepared.Width, (float)sz.Height / prepared.Height);
                float w = prepared.Width * s, h = prepared.Height * s;
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(prepared, (sz.Width - w) / 2f, (sz.Height - h) / 2f, w, h);
                using (var b = new SolidBrush(Color.FromArgb(Dim * 255 / 100, 0, 0, 0)))
                    g.FillRectangle(b, 0, 0, sz.Width, sz.Height);
            }
            cache = bmp;
        }

        // draws the part of the background that sits behind control c
        public static void Draw(Graphics g, Control c)
        {
            if (prepared == null || Anchor == null || !Anchor.IsHandleCreated || !c.IsHandleCreated) return;
            Size sz = Anchor.ClientSize;
            if (cache == null || cache.Size != sz) Build(sz);
            if (cache == null) return;
            Point p = Anchor.PointToClient(c.PointToScreen(Point.Empty));
            g.DrawImage(cache, new Rectangle(0, 0, c.Width, c.Height), p.X, p.Y, c.Width, c.Height, GraphicsUnit.Pixel);
        }
    }

    // =====================================================================
    //  ICONS (PNGs in the Icons folder next to the .exe)
    //  - works with black-on-white AND white-on-black icons (auto detected)
    //  - removes the little watermark in the corner
    //  - makes the strokes bolder and fills the icon box
    // =====================================================================
    internal static class Ico
    {
        static readonly string Dir = Path.Combine(Application.StartupPath, "Icons");
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        static readonly Dictionary<string, string> paths = new Dictionary<string, string>();

        static string Norm(string s)
        {
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().ToLowerInvariant();
        }

        static bool IsImg(string f)
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        static string Find(string key)
        {
            string p;
            if (paths.TryGetValue(key, out p)) return p;
            p = null;
            try
            {
                if (Directory.Exists(Dir))
                {
                    string k = Norm(key);
                    string[] all = Directory.GetFiles(Dir);
                    // 1) exact match (ignoring the word "icon" in the file name)
                    foreach (string f in all)
                    {
                        if (!IsImg(f)) continue;
                        string n = Norm(Path.GetFileNameWithoutExtension(f)).Replace("icon", "").Trim();
                        if (n == k) { p = f; break; }
                    }
                    // 2) partial match
                    if (p == null)
                        foreach (string f in all)
                        {
                            if (!IsImg(f)) continue;
                            if (Norm(Path.GetFileNameWithoutExtension(f)).Contains(k)) { p = f; break; }
                        }
                }
            }
            catch { }
            paths[key] = p;
            return p;
        }

        static byte[] Dilate(byte[] src, int W, int r)
        {
            var tmp = new byte[src.Length];
            var dst = new byte[src.Length];
            for (int y = 0; y < W; y++)
                for (int x = 0; x < W; x++)
                {
                    int x0 = Math.Max(0, x - r), x1 = Math.Min(W - 1, x + r);
                    byte m = 0;
                    for (int k = x0; k <= x1; k++) { byte v = src[y * W + k]; if (v > m) m = v; }
                    tmp[y * W + x] = m;
                }
            for (int y = 0; y < W; y++)
                for (int x = 0; x < W; x++)
                {
                    int y0 = Math.Max(0, y - r), y1 = Math.Min(W - 1, y + r);
                    byte m = 0;
                    for (int k = y0; k <= y1; k++) { byte v = tmp[k * W + x]; if (v > m) m = v; }
                    dst[y * W + x] = m;
                }
            return dst;
        }

        static bool LightOnDark(Image raw)
        {
            using (var small = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(raw, new Rectangle(0, 0, 32, 32));
                }
                long sum = 0;
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Color c = small.GetPixel(x, y);
                        sum += (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                    }
                return sum / 1024 < 128;
            }
        }

        // removes light pixels that are connected to the image border (outside of a black rounded square)
        static void ClearOutside(byte[] al, int W)
        {
            var q = new Queue<int>();
            for (int i = 0; i < W; i++)
            {
                Push(al, q, i, 0, W); Push(al, q, i, W - 1, W);
                Push(al, q, 0, i, W); Push(al, q, W - 1, i, W);
            }
            while (q.Count > 0)
            {
                int p = q.Dequeue();
                int x = p % W, y = p / W;
                if (x > 0) Push(al, q, x - 1, y, W);
                if (x < W - 1) Push(al, q, x + 1, y, W);
                if (y > 0) Push(al, q, x, y - 1, W);
                if (y < W - 1) Push(al, q, x, y + 1, W);
            }
        }

        static void Push(byte[] al, Queue<int> q, int x, int y, int W)
        {
            int p = y * W + x;
            if (al[p] == 0) return;
            al[p] = 0;
            q.Enqueue(p);
        }

        public static Bitmap Get(string key, Color tint, int size)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string ck = key + "|" + tint.ToArgb() + "|" + size;
            Bitmap cached;
            if (cache.TryGetValue(ck, out cached)) return cached;

            string path = Find(key);
            if (path == null) return null;

            try
            {
                const int W = 256;
                bool light;
                var work = new Bitmap(W, W, PixelFormat.Format32bppArgb);
                using (var raw = Image.FromFile(path))
                {
                    light = LightOnDark(raw);
                    using (var g = Graphics.FromImage(work))
                    {
                        g.Clear(light ? Color.Black : Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(raw, new Rectangle(0, 0, W, W));
                    }
                }

                var data = work.LockBits(new Rectangle(0, 0, W, W), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                byte[] buf = new byte[data.Stride * W];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                byte[] al = new byte[W * W];
                int wx = (int)(W * 0.70), wy = (int)(W * 0.91);   // watermark corner
                for (int y = 0; y < W; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * data.Stride + x * 4;
                        int b = buf[i], gg = buf[i + 1], r = buf[i + 2];
                        int mx = Math.Max(r, Math.Max(gg, b)), mn = Math.Min(r, Math.Min(gg, b));
                        int a = light ? mx : 255 - mn;
                        a = a < 45 ? 0 : Math.Min(255, (a - 45) * 255 / 140);
                        if (x >= wx && y >= wy) a = 0;
                        al[y * W + x] = (byte)a;
                    }
                if (light) ClearOutside(al, W);
                al = Dilate(al, W, light ? 2 : 4);   // bolder strokes

                int minX = W, minY = W, maxX = -1, maxY = -1;
                for (int y = 0; y < W; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * data.Stride + x * 4;
                        byte a = al[y * W + x];
                        buf[i] = tint.B; buf[i + 1] = tint.G; buf[i + 2] = tint.R; buf[i + 3] = a;
                        if (a > 96)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                Marshal.Copy(buf, 0, data.Scan0, buf.Length);
                work.UnlockBits(data);

                if (maxX < 0) { work.Dispose(); return null; }

                int cw = maxX - minX + 1, ch = maxY - minY + 1, side = Math.Max(cw, ch);
                var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float pad = size * 0.02f;
                    float scale = (size - 2 * pad) / side;
                    float dw = cw * scale, dh = ch * scale;
                    g.DrawImage(work, new RectangleF((size - dw) / 2f, (size - dh) / 2f, dw, dh),
                        new RectangleF(minX, minY, cw, ch), GraphicsUnit.Pixel);
                }
                work.Dispose();
                cache[ck] = result;
                return result;
            }
            catch { return null; }
        }
    }

    internal static class Brand
    {
        public static Bitmap Mark(Color c, int size)
        {
            var b = Ico.Get("logo", c, size);
            if (b != null) return b;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var f = new Font("Segoe UI", size * 0.55f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var br = new SolidBrush(c))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("N", f, br, new RectangleF(0, 0, size, size), sf);
            }
            return bmp;
        }
    }

    // =====================================================================
    //  .NET CHECK
    // =====================================================================
    internal static class DotNetCheck
    {
        const int MinRelease = 461808; // .NET Framework 4.7.2 (528040 = 4.8)
        public const string Url = "https://dotnet.microsoft.com/download/dotnet-framework";

        public static bool Ok()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("Release");
                    return v != null && Convert.ToInt32(v) >= MinRelease;
                }
            }
            catch { return true; }
        }
    }

    // =====================================================================
    //  ANIMATION
    // =====================================================================
    internal class Tween
    {
        readonly Timer t = new Timer { Interval = 15 };
        float target;
        public float Value;

        public Tween(Action onTick)
        {
            t.Tick += (s, e) =>
            {
                Value += (target - Value) * 0.3f;
                if (Math.Abs(target - Value) < 0.005f) { Value = target; t.Stop(); }
                onTick();
            };
        }
        public void To(float v) { target = v; if (!t.Enabled) t.Start(); }
        public void Set(float v) { Value = target = v; t.Stop(); }
    }

    // =====================================================================
    //  BASE CONTROLS (all transparent so the background image shows through)
    // =====================================================================
    internal class DB : Control
    {
        public DB()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
        }
    }

    internal class TPanel : Control
    {
        public TPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
        }
    }

    internal class RoundPanel : Control
    {
        public int Radius = 8;
        public Color Fill = Theme.Panel;
        public Color Line = Theme.LineSoft;
        public bool Solid;

        public RoundPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            G.Smooth(e.Graphics);
            Color f = Solid ? Fill : Theme.Surf(Fill);
            G.FillRound(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius, f, Line);
        }
    }

    internal class WindowPanel : RoundPanel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            using (var p = G.Round(new RectangleF(0, 0, Width, Height), Radius))
            {
                if (Backdrop.Has)
                {
                    g.SetClip(p);
                    Backdrop.Draw(g, this);
                    g.ResetClip();
                }
                else using (var b = new SolidBrush(Theme.Bg)) g.FillPath(b, p);
            }
        }
    }

    internal class Lbl : DB
    {
        public int Role;                 // 0 text, 1 muted, 2 dim, 3 accent
        public Color? Force;
        public bool AutoFit = true;
        public TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                       TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        string text = "";
        string iconKey;
        int iconSize = 22;

        // properties (not fields) so the label re-measures itself when the icon is set
        public string IconKey { get { return iconKey; } set { iconKey = value; DoFit(); Invalidate(); } }
        public int IconSize { get { return iconSize; } set { iconSize = value; DoFit(); Invalidate(); } }

        public Lbl() { Font = Theme.UI(9); }

        public override string Text
        {
            get { return text; }
            set { text = value ?? ""; DoFit(); Invalidate(); }
        }

        public override Font Font
        {
            get { return base.Font; }
            set { base.Font = value; DoFit(); }
        }

        void DoFit()
        {
            if (!AutoFit) return;
            var sz = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int w = sz.Width + 2, h = sz.Height;
            if (iconKey != null) { w += iconSize + 8; h = Math.Max(h, iconSize); }
            Size = new Size(w, h);
        }

        Color Fg()
        {
            if (Force.HasValue) return Force.Value;
            switch (Role)
            {
                case 1: return Theme.Muted;
                case 2: return Theme.Dim;
                case 3: return Theme.Accent;
                default: return Theme.Text;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            Color c = Fg();
            int x = 0;
            if (iconKey != null)
            {
                var ico = Ico.Get(iconKey, c, iconSize);
                if (ico != null) g.DrawImage(ico, 0, (Height - iconSize) / 2f, iconSize, iconSize);
                x = iconSize + 8;
            }
            TextRenderer.DrawText(g, text, Font, new Rectangle(x, 0, Math.Max(1, Width - x), Height), c, Flags);
        }
    }

    internal class Sep : DB
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var p = new Pen(Theme.LineSoft)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }

    internal class PillButton : DB
    {
        public string Caption = "", IconKey;
        public bool Primary, GlyphPlus, Outline;
        public Color? Base;
        public int Radius = 7, IconSize = 24;
        bool hover, down;
        readonly Tween tw;

        public PillButton()
        {
            Cursor = Cursors.Hand;
            Size = new Size(40, 38);
            tw = new Tween(() => Invalidate());
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; tw.To(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; tw.To(0); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            float t = tw.Value;
            Color main = Base ?? Theme.Accent;
            Color fill, fg;
            Color? line;
            if (Primary)
            {
                fill = G.Alpha(main, 38 + (int)(36 * t));
                line = G.Alpha(main, 110 + (int)(100 * t));
                fg = main;
            }
            else
            {
                fill = G.Alpha(Theme.Hover, (int)(235 * t));
                line = Outline ? (Color?)G.Mix(Theme.LineSoft, Theme.Line, t) : null;
                fg = hover ? main : Theme.Text;
            }

            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius, fill, line);
            float off = down ? 1f : 0f;

            if (GlyphPlus)
            {
                using (var p = new Pen(fg, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float cx = Width / 2f, cy = Height / 2f + off;
                    g.DrawLine(p, cx - 6.5f, cy, cx + 6.5f, cy);
                    g.DrawLine(p, cx, cy - 6.5f, cx, cy + 6.5f);
                }
                return;
            }

            int isz = Math.Min(IconSize, Height - 10);
            var ico = Ico.Get(IconKey, fg, isz);
            var font = Theme.UI(9.5f, Primary ? FontStyle.Bold : FontStyle.Regular);
            int capW = string.IsNullOrEmpty(Caption) ? 0 :
                TextRenderer.MeasureText(Caption, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            int total = (ico != null ? isz : 0) + (ico != null && capW > 0 ? 8 : 0) + capW;
            float x0 = (Width - total) / 2f;

            if (ico != null) g.DrawImage(ico, x0, (Height - isz) / 2f + off, isz, isz);
            if (capW > 0)
            {
                int tx = (int)(x0 + (ico != null ? isz + 8 : 0));
                TextRenderer.DrawText(g, Caption, font, new Rectangle(tx, (int)off, capW + 4, Height), fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }

    internal class WinBtn : DB
    {
        public int Kind; // 0 minimize, 1 maximize, 2 close
        readonly Tween tw;

        public WinBtn(int kind)
        {
            Kind = kind;
            Size = new Size(38, 30);
            Cursor = Cursors.Hand;
            tw = new Tween(() => Invalidate());
        }

        protected override void OnMouseEnter(EventArgs e) { tw.To(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { tw.To(0); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            float h = tw.Value;
            if (h > 0.01f)
            {
                Color c = Kind == 2 ? G.Alpha(Color.FromArgb(232, 64, 72), (int)(h * 235)) : G.Alpha(Theme.Hover, (int)(h * 255));
                G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8, c);
            }
            Color ic = (Kind == 2 && h > 0.5f) ? Color.White : G.Mix(Theme.Muted, Theme.Text, h);
            using (var p = new Pen(ic, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = Width / 2f, cy = Height / 2f;
                if (Kind == 0) g.DrawLine(p, cx - 5.5f, cy + 1, cx + 5.5f, cy + 1);
                else if (Kind == 1) { using (var path = G.Round(new RectangleF(cx - 5.5f, cy - 5.5f, 11, 11), 2.5f)) g.DrawPath(p, path); }
                else
                {
                    g.DrawLine(p, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(p, cx + 5, cy - 5, cx - 5, cy + 5);
                }
            }
        }
    }

    internal class HeaderBar : DB
    {
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            N.ReleaseCapture();
            N.SendMessage(FindForm().Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            var lg = Ico.Get("logo", Theme.Accent, 30);
            if (lg != null) g.DrawImage(lg, 2, (Height - 30) / 2f, 30, 30);
            else using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 2, (Height - 24) / 2f, 24, 24);

            var nf = Theme.UI(12, FontStyle.Bold);
            int nw = TextRenderer.MeasureText("Niox", nf, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, "Niox", nf, new Rectangle(40, 0, nw + 4, Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "v" + AppInfo.Version, Theme.UI(9), new Rectangle(40 + nw + 8, 1, 70, Height), Theme.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }

    internal class NavPill : DB
    {
        public string[] Items = new string[0];
        public string[] IconKeys = new string[0];
        int sel, hover = -1;
        readonly Tween pos;
        public event Action<int> Changed;

        public NavPill() { Cursor = Cursors.Hand; pos = new Tween(() => Invalidate()); }

        public int Selected { get { return sel; } set { sel = value; pos.To(value); Invalidate(); } }
        float ItemW { get { return Width / (float)Math.Max(1, Items.Length); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = (int)(e.X / ItemW);
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = (int)(e.X / ItemW);
            if (i >= 0 && i < Items.Length && i != sel)
            {
                Selected = i;
                if (Changed != null) Changed(i);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            float w = ItemW;
            if (hover >= 0 && hover != sel && hover < Items.Length)
                G.FillRound(g, new RectangleF(hover * w + 4, 5, w - 8, Height - 10), 7, G.Alpha(Theme.Hover, 200));
            G.FillRound(g, new RectangleF(pos.Value * w + 16, Height - 4, w - 32, 2), 1, Theme.Text);

            var font = Theme.UI(10, FontStyle.Bold);
            const int IS = 26;
            for (int i = 0; i < Items.Length; i++)
            {
                bool s = i == sel;
                Color c = s ? Theme.Accent : (i == hover ? Theme.Text : Theme.Muted);
                var ico = Ico.Get(i < IconKeys.Length ? IconKeys[i] : null, c, IS);
                int tw = TextRenderer.MeasureText(Items[i], font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int total = (ico != null ? IS + 8 : 0) + tw;
                float x0 = i * w + (w - total) / 2f;
                if (ico != null) { g.DrawImage(ico, x0, (Height - IS) / 2f, IS, IS); x0 += IS + 8; }
                TextRenderer.DrawText(g, Items[i], font, new Rectangle((int)x0, 0, tw + 4, Height), c,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }

    internal class SideMenu : DB
    {
        public string[] Items = new string[0];
        public string[] IconKeys = new string[0];
        public const int IH = 46, GAP = 4;
        int sel, hover = -1;
        readonly Tween pos;
        public event Action<int> Changed;

        public SideMenu() { Cursor = Cursors.Hand; pos = new Tween(() => Invalidate()); }

        public int Selected { get { return sel; } set { sel = value; pos.To(value); Invalidate(); } }

        int Hit(int y) { int i = y / (IH + GAP); return i >= 0 && i < Items.Length ? i : -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Hit(e.Y);
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = Hit(e.Y);
            if (i >= 0 && i != sel)
            {
                Selected = i;
                if (Changed != null) Changed(i);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            for (int i = 0; i < Items.Length; i++)
                if (i == hover && i != sel)
                    G.FillRound(g, new RectangleF(0, i * (IH + GAP), Width - 1, IH), 10, G.Alpha(Theme.Hover, 200));

            float y = pos.Value * (IH + GAP);
            G.FillRound(g, new RectangleF(0, y, Width - 1, IH), 10, G.Alpha(Theme.Accent, 28));
            G.FillRound(g, new RectangleF(0, y + 12, 3, IH - 24), 1.5f, Theme.Accent);

            const int IS = 26;
            for (int i = 0; i < Items.Length; i++)
            {
                bool s = i == sel;
                Color c = s ? Theme.Accent : (i == hover ? Theme.Text : Theme.Muted);
                float top = i * (IH + GAP);
                var ico = Ico.Get(i < IconKeys.Length ? IconKeys[i] : null, c, IS);
                if (ico != null) g.DrawImage(ico, 16, top + (IH - IS) / 2f, IS, IS);
                TextRenderer.DrawText(g, Items[i], Theme.UI(10.5f, s ? FontStyle.Bold : FontStyle.Regular),
                    new Rectangle(52, (int)top, Width - 58, IH), c,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }

    internal class Toggle : DB
    {
        bool on;
        readonly Tween tw;
        public event EventHandler CheckedChanged;

        public Toggle()
        {
            Size = new Size(42, 24);
            Cursor = Cursors.Hand;
            tw = new Tween(() => Invalidate());
        }

        public bool Checked { get { return on; } set { on = value; tw.Set(on ? 1 : 0); Invalidate(); } }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            on = !on;
            tw.To(on ? 1 : 0);
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            float t = tw.Value;
            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Height / 2f,
                G.Mix(Theme.Hover, Theme.Accent, t), G.Mix(Theme.Line, Theme.Accent, t));
            int d = Height - 8;
            float x = 4 + (Width - d - 8) * t;
            using (var b = new SolidBrush(G.Mix(Theme.Muted, Theme.Bg, t))) g.FillEllipse(b, x, 4, d, d);
        }
    }

    internal class Stepper : DB
    {
        public int Value = 11, Min = 8, Max = 24;
        public event EventHandler ValueChanged;

        public Stepper() { Size = new Size(116, 34); Cursor = Cursors.Hand; }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int v = Value;
            if (e.X < 36) v--; else if (e.X > Width - 36) v++;
            v = Math.Max(Min, Math.Min(Max, v));
            if (v != Value) { Value = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 10, Theme.Surf(Theme.Card), Theme.Line);
            using (var p = new Pen(Theme.Muted, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(p, 12, Height / 2f, 23, Height / 2f);
                float cx = Width - 18, cy = Height / 2f;
                g.DrawLine(p, cx - 5.5f, cy, cx + 5.5f, cy);
                g.DrawLine(p, cx, cy - 5.5f, cx, cy + 5.5f);
            }
            TextRenderer.DrawText(g, Value.ToString(), Theme.UI(10.5f, FontStyle.Bold), new Rectangle(36, 0, Width - 72, Height),
                Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal class SwatchBar : DB
    {
        public Color[] Colors;
        public int Index = -1;
        public bool AllowCustom = true;
        public Color CustomColor = Color.Gray;
        public event Action<Color> Picked;

        int Slots { get { return Colors.Length + (AllowCustom ? 1 : 0); } }

        public SwatchBar(Color[] cols)
        {
            Colors = cols;
            Size = new Size((cols.Length + 1) * 30, 30);
            Cursor = Cursors.Hand;
        }

        public void Select(Color c)
        {
            Index = -1;
            for (int i = 0; i < Colors.Length; i++)
                if (Colors[i].ToArgb() == c.ToArgb()) Index = i;
            if (Index < 0 && AllowCustom) { Index = Colors.Length; CustomColor = c; }
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = e.X / 30;
            if (i >= 0 && i < Colors.Length)
            {
                Index = i; Invalidate();
                if (Picked != null) Picked(Colors[i]);
            }
            else if (AllowCustom && i == Colors.Length)
            {
                Color? r = Picker.Pick(FindForm(), Index == Colors.Length ? CustomColor : Theme.Accent);
                if (r.HasValue)
                {
                    CustomColor = r.Value; Index = i; Invalidate();
                    if (Picked != null) Picked(CustomColor);
                }
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            for (int i = 0; i < Slots; i++)
            {
                int x = i * 30 + 4;
                if (i < Colors.Length)
                    using (var b = new SolidBrush(Colors[i])) g.FillEllipse(b, x + 2, 7, 16, 16);
                else if (Index == Colors.Length)
                    using (var b = new SolidBrush(CustomColor)) g.FillEllipse(b, x + 2, 7, 16, 16);
                else
                {
                    using (var p = new Pen(Theme.Muted, 1.2f) { DashStyle = DashStyle.Dot }) g.DrawEllipse(p, x + 2, 7, 16, 16);
                    using (var p = new Pen(Theme.Muted, 1.6f))
                    {
                        g.DrawLine(p, x + 10, 11, x + 10, 19);
                        g.DrawLine(p, x + 6, 15, x + 14, 15);
                    }
                }
                if (i == Index) using (var p = new Pen(Theme.Text, 1.6f)) g.DrawEllipse(p, x - 1, 4, 22, 22);
            }
        }
    }

    internal class SliderBar : DB
    {
        public int Min = 0, Max = 100;
        int val;
        bool drag;
        public event Action<int> Changed;

        public SliderBar() { Size = new Size(210, 26); Cursor = Cursors.Hand; }

        public int Value { get { return val; } set { val = Math.Max(Min, Math.Min(Max, value)); Invalidate(); } }

        int TrackW { get { return Width - 52; } }

        void SetFromX(int x)
        {
            double t = Math.Max(0, Math.Min(1, (x - 6) / (double)(TrackW - 12)));
            int v = Min + (int)Math.Round(t * (Max - Min));
            if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(v); }
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            float t = (val - Min) / (float)Math.Max(1, Max - Min);
            float cy = Height / 2f;
            float x0 = 6, x1 = TrackW - 6;
            G.FillRound(g, new RectangleF(x0, cy - 2.5f, x1 - x0, 5), 2.5f, Theme.Hover, Theme.LineSoft);
            float kx = x0 + (x1 - x0) * t;
            if (kx - x0 > 1) G.FillRound(g, new RectangleF(x0, cy - 2.5f, kx - x0, 5), 2.5f, Theme.Accent);
            using (var b = new SolidBrush(Theme.Text)) g.FillEllipse(b, kx - 7, cy - 7, 14, 14);
            using (var p = new Pen(Theme.Bg, 1.5f)) g.DrawEllipse(p, kx - 7, cy - 7, 14, 14);
            TextRenderer.DrawText(g, val + "%", Theme.UI(8.5f, FontStyle.Bold), new Rectangle(TrackW, 0, Width - TrackW, Height),
                Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal class SettingsCard : RoundPanel
    {
        readonly List<Control> rights = new List<Control>();
        readonly List<Sep> seps = new List<Sep>();
        int rows;

        public SettingsCard(string title)
        {
            Radius = 8; Fill = Theme.Panel; Line = Theme.LineSoft; Height = 44;
            Controls.Add(new Lbl { Text = title, Font = Theme.UI(8, FontStyle.Bold), Role = 1, Location = new Point(20, 16) });
        }

        public void AddRow(string title, string desc, Control ctl)
        {
            int y = 40 + rows * 58;
            if (rows > 0)
            {
                var sp = new Sep { Height = 1, Left = 20, Top = y, Width = Width - 40 };
                Controls.Add(sp); seps.Add(sp);
            }
            Controls.Add(new Lbl { Text = title, Font = Theme.UI(10), Role = 0, Location = new Point(20, y + 9) });
            Controls.Add(new Lbl { Text = desc, Font = Theme.UI(8.5f), Role = 1, Location = new Point(20, y + 31) });
            if (ctl != null)
            {
                ctl.Top = y + (58 - ctl.Height) / 2;
                ctl.Left = Width - ctl.Width - 20;
                Controls.Add(ctl); rights.Add(ctl);
            }
            rows++;
            Height = 44 + rows * 58 + 6;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (var c in rights) c.Left = Width - c.Width - 20;
            foreach (var s in seps) s.Width = Width - 40;
        }
    }

    // =====================================================================
    //  DARK CONTEXT MENUS
    // =====================================================================
    internal class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Line; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color SeparatorDark { get { return Theme.LineSoft; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
    }

    internal class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Color c = Theme.Text;
            if (e.Item.Tag is Color) c = (Color)e.Item.Tag;
            if (!e.Item.Enabled) c = Theme.Dim;
            e.TextColor = c;
            base.OnRenderItemText(e);
        }
    }

    // =====================================================================
    //  TABS (with + and x, right click, double click to rename)
    // =====================================================================
    internal class ScriptFile
    {
        public string Name, Path;
        public bool Modified, Loading;
        public Panel Host;
        public FastColoredTextBox Editor;
        public DocumentMap Map;
    }

    internal class TabBar : DB
    {
        public List<ScriptFile> Files = new List<ScriptFile>();
        public ScriptFile Current;
        public event Action<ScriptFile> SelectRequested, CloseRequested, RenameRequested;
        public event Action<ScriptFile, Point> ContextRequested;
        public event Action AddRequested;

        const int TW = 176, GAP = 6, PLUS = 36;
        int offset, hoverTab = -1;
        bool hoverClose, hoverPlus;

        public TabBar() { Cursor = Cursors.Hand; }

        int ViewW { get { return Width - PLUS - 8; } }
        Rectangle TabRect(int i) { return new Rectangle(i * (TW + GAP) - offset, 3, TW, Height - 6); }
        Rectangle CloseRect(Rectangle r) { return new Rectangle(r.Right - 34, r.Y, 30, r.Height); }
        Rectangle PlusRect()
        {
            int x = Math.Min(Files.Count * (TW + GAP) - offset, ViewW);
            return new Rectangle(x, (Height - 32) / 2, 32, 32);
        }

        int HitTab(Point p)
        {
            if (p.X >= ViewW) return -1;
            for (int i = 0; i < Files.Count; i++) if (TabRect(i).Contains(p)) return i;
            return -1;
        }

        public void SetCurrent(ScriptFile f)
        {
            Current = f;
            int i = Files.IndexOf(f);
            if (i >= 0 && Width > 0)
            {
                int l = i * (TW + GAP), r = l + TW;
                if (l - offset < 0) offset = l;
                else if (r - offset > ViewW) offset = r - ViewW;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = HitTab(e.Location);
            bool c = i >= 0 && CloseRect(TabRect(i)).Contains(e.Location);
            bool p = PlusRect().Contains(e.Location);
            if (i != hoverTab || c != hoverClose || p != hoverPlus) { hoverTab = i; hoverClose = c; hoverPlus = p; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverTab = -1; hoverClose = false; hoverPlus = false; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && PlusRect().Contains(e.Location))
            {
                if (AddRequested != null) AddRequested();
                return;
            }
            int i = HitTab(e.Location);
            if (i < 0) return;
            if (e.Button == MouseButtons.Right)
            {
                if (SelectRequested != null) SelectRequested(Files[i]);
                if (ContextRequested != null) ContextRequested(Files[i], PointToScreen(e.Location));
                return;
            }
            if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && CloseRect(TabRect(i)).Contains(e.Location)))
            {
                if (CloseRequested != null) CloseRequested(Files[i]);
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (SelectRequested != null) SelectRequested(Files[i]);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int i = HitTab(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0 && !CloseRect(TabRect(i)).Contains(e.Location))
                if (RenameRequested != null) RenameRequested(Files[i]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, Files.Count * (TW + GAP) - ViewW);
            offset = Math.Max(0, Math.Min(max, offset - e.Delta / 2));
            Invalidate();
            base.OnMouseWheel(e);
        }

        static void DrawX(Graphics g, float cx, float cy, float s, Color c)
        {
            using (var p = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(p, cx - s, cy - s, cx + s, cy + s);
                g.DrawLine(p, cx + s, cy - s, cx - s, cy + s);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            g.SetClip(new Rectangle(0, 0, Math.Max(1, ViewW), Height));

            for (int i = 0; i < Files.Count; i++)
            {
                var f = Files[i];
                var r = TabRect(i);
                if (r.Right < 0 || r.Left > Width) continue;
                bool s = f == Current, h = i == hoverTab;

                Color fill = s ? Theme.Surf(Theme.Card) : (h ? Theme.Surf(Theme.Panel) : Color.Transparent);
                G.FillRound(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 7, fill,
                    s ? G.Alpha(Theme.Accent, 120) : Theme.LineSoft);

                G.FileGlyph(g, r.X + 12, r.Y + (r.Height - 20) / 2f, 20, s ? Theme.Accent : Theme.Muted);

                var font = Theme.UI(10, s ? FontStyle.Bold : FontStyle.Regular);
                TextRenderer.DrawText(g, f.Name, font, new Rectangle(r.X + 40, r.Y, TW - 40 - 38, r.Height),
                    s ? Theme.Text : Theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

                if (s) G.FillRound(g, new RectangleF(r.X + 16, r.Bottom - 4, r.Width - 32, 2), 1, Theme.Accent);

                var cr = CloseRect(r);
                float cx = cr.X + cr.Width / 2f, cy = cr.Y + cr.Height / 2f;
                bool onClose = h && hoverClose;
                if (f.Modified && !onClose)
                {
                    using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, cx - 4f, cy - 4f, 8, 8);
                }
                else if (s || h)
                {
                    if (onClose) G.FillRound(g, new RectangleF(cx - 12, cy - 12, 24, 24), 7, Theme.Hover);
                    DrawX(g, cx, cy, 4.6f, onClose ? Theme.Red : Theme.Muted);
                }
            }

            g.ResetClip();
            var pr = PlusRect();
            if (hoverPlus) G.FillRound(g, new RectangleF(pr.X, pr.Y, pr.Width, pr.Height), 9, Theme.Surf(Theme.Card), G.Alpha(Theme.Accent, 110));
            using (var p = new Pen(hoverPlus ? Theme.Accent : Theme.Muted, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = pr.X + pr.Width / 2f, cy = pr.Y + pr.Height / 2f;
                g.DrawLine(p, cx - 6.5f, cy, cx + 6.5f, cy);
                g.DrawLine(p, cx, cy - 6.5f, cx, cy + 6.5f);
            }
        }
    }

    // =====================================================================
    //  EXPLORER (two sections: Scripts / Auto Execute, drag & drop between them)
    // =====================================================================
    internal class ScriptItem { public string Name, Path; public long Size; public bool Auto; }

    internal class ExplorerTree : DB
    {
        class Row { public int Kind, Section, Y, H; public ScriptItem Item; public string Text; }

        public readonly List<ScriptItem> Scripts = new List<ScriptItem>();
        public readonly List<ScriptItem> AutoItems = new List<ScriptItem>();
        public Func<ScriptItem, bool> IsOpen;
        public event Action<ScriptItem> ItemClick, DeleteRequested;
        public event Action<ScriptItem, Point> ContextRequested;
        public event Action<ScriptItem, bool> MoveRequested;

        const int HH = 38, IH = 40, EH = 34;
        readonly bool[] collapsed = new bool[2];
        int offset, hoverRow = -1, dropSection = -1;
        ScriptItem selItem, dragItem;
        Point dragStart, dragPos;
        bool dragging;

        public ExplorerTree() { SetStyle(ControlStyles.Selectable, true); TabStop = true; }

        public void SetItems(IEnumerable<ScriptItem> scripts, IEnumerable<ScriptItem> autos)
        {
            Scripts.Clear(); Scripts.AddRange(scripts);
            AutoItems.Clear(); AutoItems.AddRange(autos);
            hoverRow = -1;
            ClampOffset();
            Invalidate();
        }

        List<Row> Rows()
        {
            var r = new List<Row>();
            int y = 0;
            for (int s = 0; s < 2; s++)
            {
                List<ScriptItem> src = s == 0 ? Scripts : AutoItems;
                r.Add(new Row { Kind = 0, Section = s, Y = y, H = HH, Text = s == 0 ? "SCRIPTS" : "AUTO EXECUTE" });
                y += HH;
                if (collapsed[s]) continue;
                if (src.Count == 0)
                {
                    r.Add(new Row { Kind = 2, Section = s, Y = y, H = EH, Text = s == 0 ? "No saved scripts yet." : "Nothing runs on attach." });
                    y += EH;
                }
                foreach (var it in src)
                {
                    r.Add(new Row { Kind = 1, Section = s, Item = it, Y = y, H = IH });
                    y += IH;
                }
                y += 8;
            }
            return r;
        }

        static int Content(List<Row> rows)
        {
            return rows.Count == 0 ? 0 : rows[rows.Count - 1].Y + rows[rows.Count - 1].H + 6;
        }

        int RowAt(List<Row> rows, int y)
        {
            y += offset;
            for (int i = 0; i < rows.Count; i++)
                if (y >= rows[i].Y && y < rows[i].Y + rows[i].H) return i;
            return -1;
        }

        void ClampOffset()
        {
            int max = Math.Max(0, Content(Rows()) - Height);
            offset = Math.Max(0, Math.Min(max, offset));
        }

        protected override void OnResize(EventArgs e) { ClampOffset(); base.OnResize(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            offset -= e.Delta / 2;
            ClampOffset();
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var rows = Rows();
            if (dragItem != null && !dragging && (Math.Abs(e.X - dragStart.X) > 6 || Math.Abs(e.Y - dragStart.Y) > 6))
                dragging = true;

            if (dragging)
            {
                dragPos = e.Location;
                int di = RowAt(rows, e.Y);
                dropSection = di >= 0 ? rows[di].Section : -1;
                Cursor = Cursors.SizeAll;
                Invalidate();
                base.OnMouseMove(e);
                return;
            }

            int i = RowAt(rows, e.Y);
            bool hand = i >= 0 && rows[i].Kind != 2;
            Cursor = hand ? Cursors.Hand : Cursors.Default;
            if (i != hoverRow) { hoverRow = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (!dragging) { hoverRow = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var rows = Rows();
            int i = RowAt(rows, e.Y);
            if (i < 0) { base.OnMouseDown(e); return; }
            var row = rows[i];

            if (e.Button == MouseButtons.Right && row.Kind == 1)
            {
                selItem = row.Item;
                Invalidate();
                if (ContextRequested != null) ContextRequested(row.Item, PointToScreen(e.Location));
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (row.Kind == 0)
                {
                    collapsed[row.Section] = !collapsed[row.Section];
                    ClampOffset();
                    Invalidate();
                }
                else if (row.Kind == 1)
                {
                    selItem = row.Item;
                    dragItem = row.Item;
                    dragStart = e.Location;
                    dragging = false;
                    Invalidate();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && dragItem != null)
            {
                var item = dragItem;
                bool wasDrag = dragging;
                int target = dropSection;
                dragItem = null; dragging = false; dropSection = -1;
                Cursor = Cursors.Default;
                Invalidate();

                if (wasDrag)
                {
                    int from = item.Auto ? 1 : 0;
                    if (target >= 0 && target != from && MoveRequested != null) MoveRequested(item, target == 1);
                }
                else if (ItemClick != null) ItemClick(item);
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && selItem != null && DeleteRequested != null) DeleteRequested(selItem);
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            var rows = Rows();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int y = row.Y - offset;
                if (y + row.H < 0 || y > Height) continue;

                if (row.Kind == 0)
                {
                    var hr = new RectangleF(6, y + 2, Width - 12, row.H - 4);
                    bool drop = dragging && dropSection == row.Section && !(dragItem != null && (dragItem.Auto ? 1 : 0) == row.Section);
                    if (drop) G.FillRound(g, hr, 9, G.Alpha(Theme.Accent, 30), G.Alpha(Theme.Accent, 160));
                    else if (i == hoverRow) G.FillRound(g, hr, 9, G.Alpha(Theme.Hover, 200));

                    G.Chevron(g, 20, y + row.H / 2f, 4.2f, Theme.Muted, !collapsed[row.Section]);
                    var ico = Ico.Get(row.Section == 0 ? "pasta" : "autoexecute", Theme.Muted, 22);
                    if (ico != null) g.DrawImage(ico, 34, y + (row.H - 22) / 2f, 22, 22);
                    TextRenderer.DrawText(g, row.Text, Theme.UI(8.5f, FontStyle.Bold), new Rectangle(64, y, Width - 110, row.H), Theme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    int cnt = row.Section == 0 ? Scripts.Count : AutoItems.Count;
                    TextRenderer.DrawText(g, cnt.ToString(), Theme.UI(8.5f), new Rectangle(0, y, Width - 16, row.H), Theme.Dim,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
                else if (row.Kind == 2)
                {
                    TextRenderer.DrawText(g, row.Text, Theme.UI(9), new Rectangle(38, y, Width - 50, row.H), Theme.Dim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
                else
                {
                    var it = row.Item;
                    var r = new RectangleF(6, y + 1, Width - 12, row.H - 2);
                    bool h = i == hoverRow && !dragging;
                    bool open = IsOpen != null && IsOpen(it);
                    bool sel = selItem == it;
                    if (h || sel || open) G.FillRound(g, r, 9, h ? G.Alpha(Theme.Hover, 235) : Theme.Surf(Theme.Card));
                    if (dragging && dragItem == it) G.FillRound(g, r, 9, G.Alpha(Theme.Accent, 22), G.Alpha(Theme.Accent, 110));

                    G.FileGlyph(g, r.X + 14, r.Y + (r.Height - 20) / 2f, 20, open ? Theme.Accent : Theme.Muted);
                    TextRenderer.DrawText(g, it.Name, Theme.UI(10), new Rectangle((int)r.X + 44, (int)r.Y, (int)r.Width - 70, (int)r.Height), Theme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    if (open) using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r.Right - 18, r.Y + r.Height / 2f - 3.5f, 7, 7);
                }
            }

            int total = Content(rows);
            if (total > Height)
            {
                int th = Math.Max(30, Height * Height / total);
                int ty = (int)((Height - th) * (offset / (double)Math.Max(1, total - Height)));
                G.FillRound(g, new RectangleF(Width - 5, ty, 3, th), 1.5f, G.Alpha(Theme.Muted, 130));
            }

            if (dragging && dragItem != null)
            {
                var gr = new RectangleF(Math.Max(4, dragPos.X - 10), dragPos.Y - 16, Math.Min(Width - 8, 190), 30);
                G.FillRound(g, gr, 9, Color.FromArgb(235, Theme.Card), Theme.Accent);
                TextRenderer.DrawText(g, dragItem.Name, Theme.UI(9.5f), new Rectangle((int)gr.X + 10, (int)gr.Y, (int)gr.Width - 16, (int)gr.Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    // =====================================================================
    //  CONSOLE (3 tabs: Console / Terminal / Output)
    // =====================================================================
    internal class LogEntry { public DateTime T; public string Msg; public Color C; }

    internal class ConsoleView : DB
    {
        readonly List<LogEntry> lines = new List<LogEntry>();
        int top;
        bool follow = true;

        public int Count { get { return lines.Count; } }
        int LH { get { return Theme.Code(9.5f).Height + 4; } }
        int VisLines { get { return Math.Max(1, Height / LH); } }
        int MaxTop { get { return Math.Max(0, lines.Count - VisLines); } }

        public void Add(LogEntry e)
        {
            lines.Add(e);
            if (lines.Count > 500) { lines.RemoveAt(0); if (top > 0) top--; }
            if (follow) top = MaxTop;
            Invalidate();
        }

        public void Clear() { lines.Clear(); top = 0; follow = true; Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            top = Math.Max(0, Math.Min(MaxTop, top - Math.Sign(e.Delta) * 3));
            follow = top >= MaxTop;
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (follow) top = MaxTop; else top = Math.Min(top, MaxTop);
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            var f = Theme.Code(9.5f);
            int lh = LH;
            for (int i = 0; i < VisLines + 1; i++)
            {
                int idx = top + i;
                if (idx >= lines.Count) break;
                var l = lines[idx];
                int y = i * lh;
                TextRenderer.DrawText(g, "[" + l.T.ToString("HH:mm:ss") + "]", f, new Rectangle(0, y, 84, lh), Theme.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, l.Msg, f, new Rectangle(90, y, Math.Max(10, Width - 100), lh), l.C,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            if (lines.Count > VisLines)
            {
                int th = Math.Max(24, Height * VisLines / lines.Count);
                int ty = (int)((Height - th) * (top / (double)Math.Max(1, lines.Count - VisLines)));
                G.FillRound(g, new RectangleF(Width - 5, ty, 3, th), 1.5f, G.Alpha(Theme.Muted, 140));
            }
        }
    }

    internal class ConsoleTabs : DB
    {
        public readonly string[] Items = { "Console", "Terminal", "Output" };
        public readonly string[] IconKeys = { "consoleterminal", "terminal", "output" };
        public readonly bool[] Unread = new bool[3];
        int sel, hover = -1;
        public event Action<int> Changed;

        public ConsoleTabs()
        {
            Cursor = Cursors.Hand;
            Size = new Size(TotalWidth + 2, 42);
        }

        public int Selected { get { return sel; } set { sel = value; Invalidate(); } }

        int TotalWidth
        {
            get { var r = Rects(); return r[r.Length - 1].Right; }
        }

        Rectangle[] Rects()
        {
            var f = Theme.UI(10, FontStyle.Bold);
            var r = new Rectangle[Items.Length];
            int x = 0;
            for (int i = 0; i < Items.Length; i++)
            {
                int tw = TextRenderer.MeasureText(Items[i], f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int w = 14 + 24 + 8 + tw + 18;
                r[i] = new Rectangle(x, 5, w, 32);
                x += w + 4;
            }
            return r;
        }

        int Hit(Point p)
        {
            var r = Rects();
            for (int i = 0; i < r.Length; i++) if (r[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Hit(e.Location);
            if (i != hover) { hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = Hit(e.Location);
            if (i >= 0 && i != sel)
            {
                sel = i; Unread[i] = false; Invalidate();
                if (Changed != null) Changed(i);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            var rects = Rects();
            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                bool s = i == sel, h = i == hover;
                var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
                if (s) G.FillRound(g, rf, 7, G.Alpha(Theme.Accent, 30), G.Alpha(Theme.Accent, 100));
                else if (h) G.FillRound(g, rf, 7, G.Alpha(Theme.Hover, 220));

                Color c = s ? Theme.Accent : (h ? Theme.Text : Theme.Muted);
                var ico = Ico.Get(IconKeys[i], c, 24);
                if (ico != null) g.DrawImage(ico, r.X + 14, r.Y + (r.Height - 24) / 2f, 24, 24);
                TextRenderer.DrawText(g, Items[i], Theme.UI(10, FontStyle.Bold),
                    new Rectangle(r.X + 46, r.Y, r.Width - 50, r.Height), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                if (Unread[i] && !s)
                    using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r.Right - 12, r.Y + 6, 7, 7);
            }
        }
    }

    // =====================================================================
    //  CLIENT PICKER BUTTON (shows the PID like other executors)
    // =====================================================================
    internal class ClientPill : DB
    {
        public string Caption = "No client";
        public int Count;
        public bool AnyInjected;
        bool hover;

        public ClientPill() { Cursor = Cursors.Hand; Size = new Size(170, 36); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 7,
                hover ? G.Alpha(Theme.Hover, 235) : Theme.Surf(Theme.Card), hover ? Theme.Line : Theme.LineSoft);

            var ico = Ico.Get("injectnew", hover ? Theme.Accent : Theme.Muted, 24);
            if (ico != null) g.DrawImage(ico, 10, (Height - 24) / 2f, 24, 24);

            Color dot = AnyInjected ? Theme.Green : (Count > 0 ? Theme.Accent : Theme.Dim);
            using (var b = new SolidBrush(dot)) g.FillEllipse(b, 42, Height / 2f - 4, 8, 8);

            TextRenderer.DrawText(g, Caption, Theme.UI(9.5f, FontStyle.Bold), new Rectangle(58, 0, Width - 90, Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            G.Chevron(g, Width - 18, Height / 2f, 4.4f, Theme.Muted, true);
        }
    }

    // =====================================================================
    //  START PAGE
    // =====================================================================
    internal class StartView : DB
    {
        public List<string> Recent = new List<string>();
        public bool[] Steps = new bool[4];
        public string StatusText = "No Roblox client found.";
        public bool Attached;
        public string Tip = "";
        public event Action<int> ActionClicked;
        public event Action<string> RecentClicked;

        static readonly string[] ActLabels = { "New script", "Open file", "Attach to Roblox", "Explore scripts", "Settings" };
        static readonly string[] ActIcons = { "newscript", "pasta", "injectnew", "editortab", "confingnew" };
        static readonly string[] StepLabels = { "Attach to Roblox", "Run your first script", "Save a script", "Add a script to Auto Execute" };

        readonly Rectangle[] actR = new Rectangle[5];
        readonly List<Rectangle> recR = new List<Rectangle>();
        int hoverAct = -1, hoverRec = -1;

        public StartView() { }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int a = -1, r = -1;
            for (int i = 0; i < actR.Length; i++) if (actR[i].Contains(e.Location)) a = i;
            for (int i = 0; i < recR.Count; i++) if (recR[i].Contains(e.Location)) r = i;
            if (a != hoverAct || r != hoverRec)
            {
                hoverAct = a; hoverRec = r;
                Cursor = (a >= 0 || r >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverAct = -1; hoverRec = -1; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (hoverAct >= 0 && ActionClicked != null) ActionClicked(hoverAct);
                else if (hoverRec >= 0 && hoverRec < Recent.Count && RecentClicked != null) RecentClicked(Recent[hoverRec]);
            }
            base.OnMouseClick(e);
        }

        static void Cap(Graphics g, string text, int x, int y, int w, string right)
        {
            TextRenderer.DrawText(g, text, Theme.UI(8.5f, FontStyle.Bold), new Rectangle(x, y, w, 20), Theme.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (right != null)
                TextRenderer.DrawText(g, right, Theme.UI(8.5f, FontStyle.Bold), new Rectangle(x, y, w, 20), Theme.Dim,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 8, Theme.Surf(Theme.Panel), Theme.LineSoft);

            int pad = 32;
            TextRenderer.DrawText(g, "Start", Theme.UI(16, FontStyle.Bold), new Rectangle(pad, 22, 400, 30), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (var db = new SolidBrush(Attached ? Theme.Green : Theme.Dim)) g.FillEllipse(db, pad, 63, 8, 8);
            TextRenderer.DrawText(g, StatusText, Theme.UI(10), new Rectangle(pad + 16, 55, Math.Max(10, Width - pad - 16), 24),
                Attached ? Theme.Green : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int colW = (Width - pad * 3) / 2;
            int x1 = pad, x2 = pad * 2 + colW, top = 108;

            // ---- START
            Cap(g, "START", x1, top, colW, null);
            for (int i = 0; i < ActLabels.Length; i++)
            {
                var r = new Rectangle(x1 - 12, top + 28 + i * 44, colW + 24, 42);
                actR[i] = r;
                bool h = i == hoverAct;
                if (h) G.FillRound(g, r, 10, G.Alpha(Theme.Hover, 235));
                var ico = Ico.Get(ActIcons[i], h ? Theme.Accent : Theme.Muted, 26);
                if (ico != null) g.DrawImage(ico, x1, r.Y + 8, 26, 26);
                TextRenderer.DrawText(g, ActLabels[i], Theme.UI(11), new Rectangle(x1 + 40, r.Y, colW - 40, r.Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            // ---- RECENT
            int ry0 = top + 28 + ActLabels.Length * 44 + 14;
            Cap(g, "RECENT SCRIPTS", x1, ry0, colW, null);
            recR.Clear();
            int maxRows = Math.Max(0, (Height - 20 - (ry0 + 28)) / 40);
            if (Recent.Count == 0)
            {
                TextRenderer.DrawText(g, "No scripts yet. Save one with Ctrl+S.", Theme.UI(9.5f), new Rectangle(x1, ry0 + 28, colW, 28), Theme.Dim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                int n = Math.Min(Recent.Count, maxRows);
                for (int i = 0; i < n; i++)
                {
                    var r = new Rectangle(x1 - 12, ry0 + 28 + i * 40, colW + 24, 38);
                    recR.Add(r);
                    bool h = i == hoverRec;
                    if (h) G.FillRound(g, r, 10, G.Alpha(Theme.Hover, 235));
                    G.FileGlyph(g, x1 + 2, r.Y + 8, 22, h ? Theme.Accent : Theme.Muted);
                    TextRenderer.DrawText(g, Path.GetFileName(Recent[i]), Theme.UI(10.5f), new Rectangle(x1 + 40, r.Y, colW - 40, r.Height), Theme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }

            // ---- GETTING STARTED
            int done = 0;
            for (int i = 0; i < Steps.Length; i++) if (Steps[i]) done++;
            Cap(g, "GETTING STARTED", x2, top, colW, done + " OF " + Steps.Length);
            for (int i = 0; i < StepLabels.Length; i++)
            {
                int y = top + 28 + i * 44;
                var c = new RectangleF(x2, y + 8, 26, 26);
                if (i < StepLabels.Length - 1)
                    using (var p = new Pen(Theme.LineSoft, 2f)) g.DrawLine(p, c.X + 13, c.Bottom + 1, c.X + 13, c.Bottom + 17);
                if (Steps[i])
                {
                    using (var b = new SolidBrush(G.Alpha(Theme.Accent, 40))) g.FillEllipse(b, c);
                    using (var p = new Pen(Theme.Accent, 1.6f)) g.DrawEllipse(p, c);
                    G.Check(g, c.X + 13, c.Y + 12.5f, 5.5f, Theme.Accent, 2.2f);
                }
                else
                {
                    using (var p = new Pen(Theme.Line, 1.6f)) g.DrawEllipse(p, c);
                }
                TextRenderer.DrawText(g, StepLabels[i], Theme.UI(11), new Rectangle(x2 + 42, y, colW - 42, 42),
                    Steps[i] ? Theme.Text : Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            // ---- TIP
            int ty = top + 28 + StepLabels.Length * 44 + 14;
            Cap(g, "TIP OF THE DAY", x2, ty, colW, null);
            TextRenderer.DrawText(g, Tip, Theme.UI(10.5f), new Rectangle(x2, ty + 28, colW, 90), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }

    // =====================================================================
    //  STATUS BAR (bottom of the window)
    // =====================================================================
    internal class StatusBar : DB
    {
        public string Left = "", Right = "Not attached";
        public Color Dot = Theme.Dim;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            using (var p = new Pen(Theme.LineSoft)) g.DrawLine(p, 0, 0, Width, 0);
            var f = Theme.UI(8.5f);
            TextRenderer.DrawText(g, Left, f, new Rectangle(2, 1, Width / 2, Height - 1), Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            int tw = TextRenderer.MeasureText(Right, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, Right, f, new Rectangle(Width - tw - 4, 1, tw + 4, Height - 1), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (var b = new SolidBrush(Dot)) g.FillEllipse(b, Width - tw - 18, Height / 2f - 3, 7, 7);
        }
    }

    // =====================================================================
    //  TOAST NOTIFICATIONS
    // =====================================================================
    internal class ToastCard : DB
    {
        public int Type; // 0 success, 1 error, 2 info
        public string Title, Msg;
        public Color Tint;
        public int RestX, RestY;
        public event Action<ToastCard> Done;

        const double Life = 3800, Slide = 320;
        DateTime born;
        readonly Timer t = new Timer { Interval = 15 };

        public ToastCard(int type, string title, string msg, Color tint)
        {
            Type = type; Title = title; Msg = msg; Tint = tint;
            Size = new Size(346, 80);
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
            born = DateTime.UtcNow;
            t.Tick += (s, e) => Step();
        }

        public void Begin() { Left = RestX + Width + 40; born = DateTime.UtcNow; t.Start(); }

        static double Ease(double x) { x = Math.Max(0, Math.Min(1, x)); return 1 - Math.Pow(1 - x, 3); }

        void Step()
        {
            double age = (DateTime.UtcNow - born).TotalMilliseconds;
            double p;
            if (age < Slide) p = Ease(age / Slide);
            else if (age > Life - Slide) p = 1 - Ease((age - (Life - Slide)) / Slide);
            else p = 1;

            Left = RestX + (int)((1 - p) * (Width + 40));
            int d = RestY - Top;
            if (d != 0) Top += Math.Abs(d) <= 1 ? d : (int)(d * 0.3);
            Invalidate();

            if (age >= Life)
            {
                t.Stop();
                if (Done != null) Done(this);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (var p = G.Round(new RectangleF(0, 0, Width, Height), 13)) Region = new Region(p);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            born = DateTime.UtcNow.AddMilliseconds(-(Life - Slide));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) t.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            double age = Math.Min(Life, (DateTime.UtcNow - born).TotalMilliseconds);

            G.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12.5f, Theme.Card, G.Alpha(Tint, 130));
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, Width, 2), G.Alpha(Tint, 230), G.Alpha(Tint, 0), LinearGradientMode.Horizontal))
                g.FillRectangle(br, 0, 0, Width, 2);
            G.FillRound(g, new RectangleF(10, 18, 3, Height - 36), 1.5f, Tint);

            var ic = new RectangleF(24, Height / 2f - 16, 32, 32);
            using (var b = new SolidBrush(G.Alpha(Tint, 40))) g.FillEllipse(b, ic);
            using (var p = new Pen(Tint, 1.2f)) g.DrawEllipse(p, ic);

            float cx = ic.X + 16, cy = ic.Y + 16;
            using (var p = new Pen(Tint, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (Type == 0)
                    g.DrawLines(p, new[] { new PointF(cx - 6.5f, cy), new PointF(cx - 2, cy + 4.5f), new PointF(cx + 6.5f, cy - 5) });
                else if (Type == 1)
                {
                    g.DrawLine(p, cx - 5.5f, cy - 5.5f, cx + 5.5f, cy + 5.5f);
                    g.DrawLine(p, cx + 5.5f, cy - 5.5f, cx - 5.5f, cy + 5.5f);
                }
                else
                {
                    g.DrawLine(p, cx, cy - 1, cx, cy + 6.5f);
                    using (var b = new SolidBrush(Tint)) g.FillEllipse(b, cx - 2f, cy - 8.5f, 4, 4);
                }
            }

            TextRenderer.DrawText(g, Title, Theme.UI(10.5f, FontStyle.Bold), new Rectangle(68, 14, Width - 82, 22), Tint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Msg, Theme.UI(9), new Rectangle(68, 38, Width - 82, 22), Color.FromArgb(205, 205, 210),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            float pw = (Width - 28) * (float)(1 - age / Life);
            if (pw >= 2) G.FillRound(g, new RectangleF(14, Height - 9, pw, 3), 1.5f, G.Alpha(Tint, 170));
        }
    }

    // =====================================================================
    //  CONFIRM DIALOG
    // =====================================================================
    internal static class Confirm
    {
        public static bool Ask(Form owner, string title, string message, string yes, string no, bool danger)
        {
            DialogResult res;
            using (var f = new Form())
            {
                f.FormBorderStyle = FormBorderStyle.None;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(420, 218);
                f.BackColor = Theme.Bg;
                f.KeyPreview = true;
                f.Opacity = 0;
                using (var p = G.Round(new RectangleF(0, 0, 420, 218), 14)) f.Region = new Region(p);

                Color tint = danger ? Theme.Red : Theme.Accent;
                var panel = new RoundPanel { Radius = 14, Fill = Theme.Bg, Line = Theme.Line, Solid = true, Bounds = new Rectangle(0, 0, 420, 218) };
                panel.Paint += (s, e) =>
                {
                    G.Smooth(e.Graphics);
                    var c = new RectangleF(24, 28, 44, 44);
                    using (var b = new SolidBrush(G.Alpha(tint, 35))) e.Graphics.FillEllipse(b, c);
                    using (var pen = new Pen(tint, 1.4f)) e.Graphics.DrawEllipse(pen, c);
                    TextRenderer.DrawText(e.Graphics, "!", Theme.UI(17, FontStyle.Bold), Rectangle.Round(c), tint,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                };

                var lt = new Lbl { Text = title, Font = Theme.UI(12.5f, FontStyle.Bold), Location = new Point(84, 30) };
                var lm = new Lbl
                {
                    AutoFit = false,
                    Size = new Size(314, 70),
                    Location = new Point(84, 66),
                    Font = Theme.UI(9.5f),
                    Role = 1,
                    Text = message,
                    Flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding
                };
                var bNo = new PillButton
                {
                    Caption = no,
                    Outline = true,
                    Size = new Size(120, 40),
                    Location = new Point(420 - 24 - 120 * 2 - 10, 218 - 24 - 40)
                };
                var bYes = new PillButton
                {
                    Caption = yes,
                    Primary = true,
                    Base = tint,
                    Size = new Size(120, 40),
                    Location = new Point(420 - 24 - 120, 218 - 24 - 40)
                };
                bNo.Click += (s, e) => f.DialogResult = DialogResult.No;
                bYes.Click += (s, e) => f.DialogResult = DialogResult.Yes;

                panel.Controls.Add(lt);
                panel.Controls.Add(lm);
                panel.Controls.Add(bNo);
                panel.Controls.Add(bYes);
                f.Controls.Add(panel);

                f.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape) f.DialogResult = DialogResult.No;
                    else if (e.KeyCode == Keys.Enter) f.DialogResult = DialogResult.Yes;
                };

                var tm = new Timer { Interval = 15 };
                tm.Tick += (s, e) =>
                {
                    if (f.Opacity < 1) f.Opacity = Math.Min(1, f.Opacity + 0.16);
                    else tm.Stop();
                };
                f.Shown += (s, e) => tm.Start();
                res = f.ShowDialog(owner);
                tm.Stop();
                tm.Dispose();
            }
            return res == DialogResult.Yes;
        }
    }

    // =====================================================================
    //  TEXT PROMPT (used by "Edit name")
    // =====================================================================
    internal static class Prompt
    {
        public static string Ask(Form owner, string title, string label, string initial, string okText)
        {
            string result = null;
            using (var f = new Form())
            {
                f.FormBorderStyle = FormBorderStyle.None;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(440, 224);
                f.BackColor = Theme.Bg;
                f.KeyPreview = true;
                f.Opacity = 0;
                using (var p = G.Round(new RectangleF(0, 0, 440, 224), 14)) f.Region = new Region(p);

                var panel = new RoundPanel { Radius = 14, Fill = Theme.Bg, Line = Theme.Line, Solid = true, Bounds = new Rectangle(0, 0, 440, 224) };
                var lt = new Lbl { Text = title, Font = Theme.UI(12.5f, FontStyle.Bold), Location = new Point(28, 26) };
                var ll = new Lbl { Text = label, Font = Theme.UI(9.5f), Role = 1, Location = new Point(28, 58) };

                var box = new RoundPanel { Radius = 10, Fill = Theme.Card, Line = Theme.Line, Solid = true, Bounds = new Rectangle(28, 90, 384, 42) };
                var tb = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Card,
                    ForeColor = Theme.Text,
                    Font = Theme.UI(11),
                    Text = initial ?? "",
                    Left = 14,
                    Width = 356
                };
                tb.Top = (box.Height - tb.Height) / 2;
                box.Controls.Add(tb);

                var bNo = new PillButton
                {
                    Caption = "Cancel",
                    Outline = true,
                    Size = new Size(120, 40),
                    Location = new Point(440 - 28 - 120 * 2 - 10, 224 - 26 - 40)
                };
                var bYes = new PillButton
                {
                    Caption = okText,
                    Primary = true,
                    Size = new Size(120, 40),
                    Location = new Point(440 - 28 - 120, 224 - 26 - 40)
                };
                bNo.Click += (s, e) => f.DialogResult = DialogResult.Cancel;
                bYes.Click += (s, e) => f.DialogResult = DialogResult.OK;

                panel.Controls.Add(lt);
                panel.Controls.Add(ll);
                panel.Controls.Add(box);
                panel.Controls.Add(bNo);
                panel.Controls.Add(bYes);
                f.Controls.Add(panel);

                tb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.OK; }
                    else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.Cancel; }
                };

                var tm = new Timer { Interval = 15 };
                tm.Tick += (s, e) =>
                {
                    if (f.Opacity < 1) f.Opacity = Math.Min(1, f.Opacity + 0.16);
                    else tm.Stop();
                };
                f.Shown += (s, e) => { tm.Start(); tb.Focus(); tb.SelectAll(); };

                if (f.ShowDialog(owner) == DialogResult.OK) result = tb.Text.Trim();
                tm.Stop();
                tm.Dispose();
            }
            return string.IsNullOrEmpty(result) ? null : result;
        }
    }

    // =====================================================================
    //  CUSTOM COLOR PICKER (replaces the ugly Windows color dialog)
    // =====================================================================
    internal static class Hsv
    {
        public static void From(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
            v = mx;
            s = mx <= 0 ? 0 : d / mx;
            if (d <= 0) h = 0;
            else if (mx == r) h = 60 * (((g - b) / d) % 6);
            else if (mx == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }

        public static Color To(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }
    }

    internal class SVBox : DB
    {
        public double H, S, V = 1;
        public event Action Changed;
        Bitmap bmp;
        double bmpH = -1;
        Size bmpSz;
        bool drag;

        public SVBox() { Cursor = Cursors.Cross; }

        void Rebuild()
        {
            if (Width < 2 || Height < 2) return;
            if (bmp != null && bmpH == H && bmpSz == Size) return;
            if (bmp != null) bmp.Dispose();
            int w = Width, h = Height;
            bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            byte[] buf = new byte[data.Stride * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Hsv.To(H, x / (double)(w - 1), 1 - y / (double)(h - 1));
                    int i = y * data.Stride + x * 4;
                    buf[i] = c.B; buf[i + 1] = c.G; buf[i + 2] = c.R; buf[i + 3] = 255;
                }
            Marshal.Copy(buf, 0, data.Scan0, buf.Length);
            bmp.UnlockBits(data);
            bmpH = H; bmpSz = Size;
        }

        void SetFrom(int x, int y)
        {
            S = Math.Max(0, Math.Min(1, x / (double)Math.Max(1, Width - 1)));
            V = 1 - Math.Max(0, Math.Min(1, y / (double)Math.Max(1, Height - 1)));
            Invalidate();
            if (Changed != null) Changed();
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFrom(e.X, e.Y); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFrom(e.X, e.Y); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing && bmp != null) bmp.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            Rebuild();
            if (bmp == null) return;
            using (var p = G.Round(new RectangleF(0, 0, Width, Height), 10))
            {
                g.SetClip(p);
                g.DrawImage(bmp, 0, 0);
                g.ResetClip();
                using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, p);
            }
            float kx = (float)(S * (Width - 1)), ky = (float)((1 - V) * (Height - 1));
            using (var pen = new Pen(Color.Black, 3f)) g.DrawEllipse(pen, kx - 8, ky - 8, 16, 16);
            using (var pen = new Pen(Color.White, 2f)) g.DrawEllipse(pen, kx - 8, ky - 8, 16, 16);
        }
    }

    internal class HueBar : DB
    {
        public double H;
        public event Action Changed;
        bool drag;

        public HueBar() { Cursor = Cursors.Hand; }

        void SetFrom(int x)
        {
            H = Math.Max(0, Math.Min(1, x / (double)Math.Max(1, Width - 1))) * 359.99;
            Invalidate();
            if (Changed != null) Changed();
        }

        protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFrom(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFrom(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            var bar = new RectangleF(0, 3, Width, Height - 6);
            using (var p = G.Round(bar, 6))
            {
                g.SetClip(p);
                for (int x = 0; x < Width; x++)
                    using (var pen = new Pen(Hsv.To(x / (double)Math.Max(1, Width - 1) * 360, 1, 1)))
                        g.DrawLine(pen, x, 0, x, Height);
                g.ResetClip();
                using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, p);
            }
            float kx = (float)(H / 359.99 * (Width - 1));
            using (var b = new SolidBrush(Hsv.To(H, 1, 1))) g.FillEllipse(b, kx - 8, Height / 2f - 8, 16, 16);
            using (var pen = new Pen(Color.White, 2.2f)) g.DrawEllipse(pen, kx - 8, Height / 2f - 8, 16, 16);
        }
    }

    internal class ColorChip : DB
    {
        public Color C = Color.White;
        protected override void OnPaint(PaintEventArgs e)
        {
            G.Smooth(e.Graphics);
            G.FillRound(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 10, C, Theme.Line);
        }
    }

    internal static class Picker
    {
        public static Color? Pick(Form owner, Color initial)
        {
            Color? result = null;
            using (var f = new Form())
            {
                f.FormBorderStyle = FormBorderStyle.None;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(380, 494);
                f.BackColor = Theme.Bg;
                f.KeyPreview = true;
                f.Opacity = 0;
                using (var p = G.Round(new RectangleF(0, 0, 380, 494), 14)) f.Region = new Region(p);

                var panel = new RoundPanel { Radius = 14, Fill = Theme.Bg, Line = Theme.Line, Solid = true, Bounds = new Rectangle(0, 0, 380, 494) };
                var title = new Lbl { Text = "Pick a color", Font = Theme.UI(12.5f, FontStyle.Bold), Location = new Point(24, 22) };
                var sub = new Lbl { Text = "Drag on the square, or type a hex code.", Font = Theme.UI(9.5f), Role = 1, Location = new Point(24, 50) };
                var sv = new SVBox { Bounds = new Rectangle(24, 84, 332, 200) };
                var hue = new HueBar { Bounds = new Rectangle(24, 296, 332, 22) };
                var chip = new ColorChip { Bounds = new Rectangle(24, 332, 60, 42) };

                var hexBox = new RoundPanel { Radius = 10, Fill = Theme.Card, Line = Theme.Line, Solid = true, Bounds = new Rectangle(96, 332, 260, 42) };
                var hexTb = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Card,
                    ForeColor = Theme.Text,
                    Font = Theme.Code(11),
                    Left = 14,
                    Width = 232,
                    MaxLength = 7
                };
                hexTb.Top = (hexBox.Height - hexTb.Height) / 2;
                hexBox.Controls.Add(hexTb);

                var presets = new SwatchBar(new[]
                {
                    Color.FromArgb(255, 255, 255), Color.FromArgb(160, 160, 165), Color.FromArgb(96, 165, 250),
                    Color.FromArgb(45, 212, 191), Color.FromArgb(74, 222, 128), Color.FromArgb(250, 204, 21),
                    Color.FromArgb(251, 146, 60), Color.FromArgb(248, 113, 113), Color.FromArgb(244, 114, 182),
                    Color.FromArgb(167, 139, 250)
                })
                { AllowCustom = false, Location = new Point(20, 390) };
                presets.Width = presets.Colors.Length * 30;

                var bNo = new PillButton { Caption = "Cancel", Outline = true, Size = new Size(120, 40), Location = new Point(380 - 24 - 240 - 10, 494 - 24 - 40) };
                var bYes = new PillButton { Caption = "Apply", Primary = true, Size = new Size(120, 40), Location = new Point(380 - 24 - 120, 494 - 24 - 40) };

                Color cur = initial;
                bool busy = false;

                Action<Color, bool> setColor = null;
                setColor = (c, fromHex) =>
                {
                    busy = true;
                    double h, s, v;
                    Hsv.From(c, out h, out s, out v);
                    if (s > 0.001 && v > 0.001) { sv.H = h; hue.H = h; }
                    sv.S = s; sv.V = v;
                    chip.C = c; cur = c;
                    if (!fromHex) hexTb.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
                    sv.Invalidate(); hue.Invalidate(); chip.Invalidate();
                    busy = false;
                };

                sv.Changed += () =>
                {
                    if (busy) return;
                    cur = Hsv.To(sv.H, sv.S, sv.V);
                    chip.C = cur; chip.Invalidate();
                    busy = true;
                    hexTb.Text = "#" + cur.R.ToString("X2") + cur.G.ToString("X2") + cur.B.ToString("X2");
                    busy = false;
                };
                hue.Changed += () =>
                {
                    if (busy) return;
                    sv.H = hue.H;
                    sv.Invalidate();
                    cur = Hsv.To(sv.H, sv.S, sv.V);
                    chip.C = cur; chip.Invalidate();
                    busy = true;
                    hexTb.Text = "#" + cur.R.ToString("X2") + cur.G.ToString("X2") + cur.B.ToString("X2");
                    busy = false;
                };
                hexTb.TextChanged += (s, e) =>
                {
                    if (busy) return;
                    string t = hexTb.Text.Trim().TrimStart('#');
                    int hv;
                    if (t.Length == 6 && int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hv))
                        setColor(Color.FromArgb(255, (hv >> 16) & 255, (hv >> 8) & 255, hv & 255), true);
                };
                presets.Picked += c => setColor(c, false);

                bNo.Click += (s, e) => f.DialogResult = DialogResult.Cancel;
                bYes.Click += (s, e) => f.DialogResult = DialogResult.OK;

                panel.Controls.Add(title);
                panel.Controls.Add(sub);
                panel.Controls.Add(sv);
                panel.Controls.Add(hue);
                panel.Controls.Add(chip);
                panel.Controls.Add(hexBox);
                panel.Controls.Add(presets);
                panel.Controls.Add(bNo);
                panel.Controls.Add(bYes);
                f.Controls.Add(panel);

                f.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape) f.DialogResult = DialogResult.Cancel;
                };

                setColor(Color.FromArgb(255, initial.R, initial.G, initial.B), false);

                var tm = new Timer { Interval = 15 };
                tm.Tick += (s, e) =>
                {
                    if (f.Opacity < 1) f.Opacity = Math.Min(1, f.Opacity + 0.16);
                    else tm.Stop();
                };
                f.Shown += (s, e) => tm.Start();

                if (f.ShowDialog(owner) == DialogResult.OK) result = cur;
                tm.Stop();
                tm.Dispose();
            }
            return result;
        }
    }

    // =====================================================================
    //  SPLASH SCREEN (logo glows from bottom to top, then fades out)
    // =====================================================================
    internal class SplashForm : Form
    {
        public event Action<double> Fading;
        public event Action Completed;

        const double SweepMs = 1150, HoldMs = 350, FadeMs = 1100;
        const int Sweeps = 3, LogoSize = 150;

        readonly Timer t = new Timer { Interval = 16 };
        DateTime start;
        readonly Bitmap lit, dim;
        double p, glow, progress;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 380);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            TopMost = true;
            using (var path = G.Round(new RectangleF(0, 0, 520, 380), 16)) Region = new Region(path);
            lit = Brand.Mark(Color.White, LogoSize);
            dim = Brand.Mark(Color.FromArgb(62, 62, 62), LogoSize);
            t.Tick += (s, e) => Step();
        }

        public void Begin() { start = DateTime.UtcNow; t.Start(); }

        static double Ease(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }

        void Step()
        {
            double ms = (DateTime.UtcNow - start).TotalMilliseconds;
            double sweepEnd = Sweeps * SweepMs;
            if (ms < sweepEnd)
            {
                double c = (ms % SweepMs) / SweepMs;
                p = Ease(Math.Min(1, c / 0.8));
                glow = p;
                progress = ms / (sweepEnd + HoldMs);
            }
            else if (ms < sweepEnd + HoldMs)
            {
                p = 1; glow = 1;
                progress = ms / (sweepEnd + HoldMs);
            }
            else
            {
                p = 1; glow = 1; progress = 1;
                double f = (ms - sweepEnd - HoldMs) / FadeMs;
                if (f >= 1)
                {
                    t.Stop();
                    if (Fading != null) Fading(1);
                    Close();
                    if (Completed != null) Completed();
                    return;
                }
                double e = Ease(f);
                Opacity = 1 - e;
                if (Fading != null) Fading(e);
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) t.Dispose();
            base.Dispose(disposing);
        }

        void DrawLit(Graphics g, int x, int y, int size, double pr)
        {
            const int F = 34;
            double lineTop = y + (size + F) * (1 - pr) - F;
            double litStart = lineTop + F;

            int fs = (int)Math.Max(y, Math.Ceiling(litStart));
            if (fs < y + size)
                g.DrawImage(lit, new Rectangle(x, fs, size, y + size - fs), 0, fs - y, size, y + size - fs, GraphicsUnit.Pixel);

            using (var ia = new ImageAttributes())
            {
                for (int r = (int)lineTop; r < (int)litStart; r += 2)
                {
                    int ry = Math.Max(r, y);
                    int h = Math.Min(2, y + size - ry);
                    if (h <= 0 || r >= y + size) continue;
                    float a = (float)((r - lineTop) / F);
                    a = Math.Max(0f, Math.Min(1f, a));
                    var cm = new ColorMatrix();
                    cm.Matrix33 = a;
                    ia.SetColorMatrix(cm);
                    g.DrawImage(lit, new Rectangle(x, ry, size, h), 0, ry - y, size, h, GraphicsUnit.Pixel, ia);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; G.Smooth(g);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using (var path = G.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 16))
            using (var pen = new Pen(Theme.Line))
                g.DrawPath(pen, path);

            int x = (Width - LogoSize) / 2, y = 54;

            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(x - 70, y - 70, LogoSize + 140, LogoSize + 140);
                using (var pg = new PathGradientBrush(gp))
                {
                    pg.CenterColor = Color.FromArgb((int)(36 * glow), 255, 255, 255);
                    pg.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillPath(pg, gp);
                }
            }

            g.DrawImage(dim, x, y, LogoSize, LogoSize);
            DrawLit(g, x, y, LogoSize, p);

            TextRenderer.DrawText(g, "NIOX", Theme.UI(22, FontStyle.Bold), new Rectangle(0, 226, Width, 40), Color.FromArgb(240, 240, 240),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Loading...", Theme.UI(9.5f), new Rectangle(0, 268, Width, 22), Color.FromArgb(138, 138, 138),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int bw = 190, bx = (Width - bw) / 2, by = 320;
            G.FillRound(g, new RectangleF(bx, by, bw, 4), 2, Color.FromArgb(40, 40, 40));
            float fw = (float)(bw * Math.Max(0, Math.Min(1, progress)));
            if (fw > 2) G.FillRound(g, new RectangleF(bx, by, fw, 4), 2, Color.FromArgb(235, 235, 235));
        }
    }

    // =====================================================================
    //  LUA SYNTAX COLORS
    // =====================================================================
    internal static class LuaSyntax
    {
        static readonly Style SCall = new TextStyle(new SolidBrush(Color.FromArgb(122, 162, 255)), null, FontStyle.Regular);
        static readonly Style SBuiltin = new TextStyle(new SolidBrush(Color.FromArgb(86, 214, 230)), null, FontStyle.Regular);
        static readonly Style SKeyword = new TextStyle(new SolidBrush(Color.FromArgb(255, 121, 198)), null, FontStyle.Regular);
        static readonly Style SLiteral = new TextStyle(new SolidBrush(Color.FromArgb(255, 176, 102)), null, FontStyle.Regular);
        static readonly Style SString = new TextStyle(new SolidBrush(Color.FromArgb(152, 225, 150)), null, FontStyle.Regular);
        static readonly Style SComment = new TextStyle(new SolidBrush(Color.FromArgb(108, 112, 146)), null, FontStyle.Regular);

        static readonly Regex RxCall = new Regex(@"\b[A-Za-z_]\w*(?=\s*[\(\{""'])", RegexOptions.Compiled);

        static readonly Regex RxBuiltin = new Regex(
            @"\b(?:print|warn|error|assert|pcall|xpcall|require|loadstring|game|workspace|script|task|wait|spawn|delay|typeof|type|tostring|tonumber|" +
            @"ipairs|pairs|next|select|unpack|table|string|math|os|coroutine|Instance|Vector3|Vector2|CFrame|Color3|UDim2|UDim|Enum|BrickColor|" +
            @"setmetatable|getmetatable|rawget|rawset|tick|time|Drawing|getgenv|getrenv|getfenv|setfenv|readfile|writefile|isfile|makefolder|" +
            @"identifyexecutor|hookfunction|newcclosure|Players|RunService|HttpGet|HttpService)\b", RegexOptions.Compiled);

        static readonly Regex RxKeyword = new Regex(
            @"\b(?:and|break|do|else|elseif|end|for|function|goto|if|in|local|not|or|repeat|return|then|until|while|continue)\b",
            RegexOptions.Compiled);

        static readonly Regex RxLiteral = new Regex(
            @"\b(?:true|false|nil)\b|\b(?:0[xX][0-9a-fA-F]+|\d+\.?\d*(?:[eE][+-]?\d+)?)\b", RegexOptions.Compiled);

        static readonly Regex RxString = new Regex(
            @"""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])*'|\[(=*)\[[\s\S]*?\]\1\]", RegexOptions.Compiled);

        static readonly Regex RxComment = new Regex(
            @"^(?:[^""'\n]|""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])*')*?(?<range>--\[(=*)\[[\s\S]*?\]\1\]|--[^\n]*)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public static void Register(FastColoredTextBox tb)
        {
            tb.AddStyle(SCall);
            tb.AddStyle(SBuiltin);
            tb.AddStyle(SKeyword);
            tb.AddStyle(SLiteral);
            tb.AddStyle(SString);
            tb.AddStyle(SComment);
        }

        public static void Highlight(Range r)
        {
            r.ClearStyle(SCall, SBuiltin, SKeyword, SLiteral, SString, SComment);
            r.SetStyle(SCall, RxCall);
            r.SetStyle(SBuiltin, RxBuiltin);
            r.SetStyle(SKeyword, RxKeyword);
            r.SetStyle(SLiteral, RxLiteral);
            r.SetStyle(SString, RxString);
            r.SetStyle(SComment, RxComment);
        }
    }

    // lets the mouse wheel scroll the control under the cursor (console, explorer, tabs)
    internal class WheelFilter : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x20A) return false;
            long lp = m.LParam.ToInt64();
            var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
            var c = Control.FromHandle(N.WindowFromPoint(pt));
            if (c is ConsoleView || c is ExplorerTree || c is TabBar)
            {
                N.SendMessage(c.Handle, 0x20A, m.WParam, m.LParam);
                return true;
            }
            return false;
        }
    }

    // =====================================================================
    //  NIOXCORE.DLL WRAPPER (your own C++ DLL, replaces QuorumAPI)
    //  Put NioxCore.dll next to the .exe. Signatures must match the .cpp
    //  exactly: same order, same types, same __cdecl calling convention.
    // =====================================================================
    internal static class NioxCore
    {
        const string Dll = "NioxCore.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AttachAll();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Attach(uint pid);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool IsAttached();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool IsAttachedByPid(uint pid);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Detach();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DetachByPid(uint pid);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool Execute(uint pid, [MarshalAs(UnmanagedType.LPStr)] string script);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool ExecuteScriptAll([MarshalAs(UnmanagedType.LPStr)] string script);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AllowMultiInstance(bool allow);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool IsMultiInstanceAllowed();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool OpenRoblox();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool AutoUpdate();
    }

    // =====================================================================
    //  MAIN FORM
    // =====================================================================
    public partial class Form1 : Form
    {
        static readonly string ScriptsDir = Path.Combine(Application.StartupPath, "Scripts");
        static readonly string AutoDir = Path.Combine(ScriptsDir, "AutoExecute");

        WindowPanel window;
        RoundPanel explorer, editorHost, consolePanel, pill, settingsMenu;
        HeaderBar header;
        StatusBar sbar;
        NavPill nav;
        WinBtn bMin, bMax, bClose;
        TPanel pageStart, pageScripts, pageSettings;
        TPanel[] sPages;
        StartView start;
        SideMenu sMenu;
        TabBar tabBar;
        ExplorerTree tree;
        TextBox search;
        Lbl statusInfo, consoleCount;
        ClientPill clientPill;
        PillButton btnExec, btnInject, btnOpen, btnSave, btnClear, btnTerm, btnNewScript, btnConsoleClear;
        ConsoleView console, terminal, output;
        ConsoleTabs cTabs;
        SwatchBar swAccent, swText;
        SplashForm splash;

        readonly List<ScriptFile> files = new List<ScriptFile>();
        readonly List<ToastCard> toasts = new List<ToastCard>();
        readonly ToolTip tip = new ToolTip();
        readonly Timer saveTimer = new Timer { Interval = 500 };
        readonly Timer pollTimer = new Timer { Interval = 2000 };
        readonly Timer autoExecTimer = new Timer { Interval = 1800 };
        readonly WheelFilter wheel = new WheelFilter();
        ScriptFile current;

        // Roblox clients (multi instance)
        readonly List<int> pids = new List<int>();
        readonly HashSet<int> injectedPids = new HashSet<int>();
        readonly Dictionary<int, DateTime> firstSeen = new Dictionary<int, DateTime>();
        int selectedPid = 0;   // 0 = all clients
        DateTime lastTry = DateTime.MinValue;

        int untitled = 0;
        float fontSize = 11f;
        bool showLines = true, showMap = true, hiLine = true, wrap = false;
        bool confirmClose = true, notifOn = true, consoleOn = true;
        bool autoInject = false, runAuto = true;

        public Form1()
        {
            InitializeComponent();
            Controls.Clear();
            Directory.CreateDirectory(ScriptsDir);
            Directory.CreateDirectory(AutoDir);
            LoadPrefs();

            Text = "Niox";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(1000, 640);
            BackColor = Theme.Bg;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            Opacity = 0; // stays invisible until the splash screen finishes

            BuildWindow();
            BuildStartPage();
            BuildScriptsPage();
            BuildSettingsPage();

            Theme.Changed += ApplyTheme;
            SizeChanged += (s, e) => { ApplyRegion(); LayoutAll(); ArrangeToasts(); };
            KeyDown += OnShortcut;
            Activated += (s, e) => RefreshExplorer();
            Shown += (s, e) => BeginSplash();
            FormClosed += (s, e) =>
            {
                Application.RemoveMessageFilter(wheel);
                Prefs.Save();
                pollTimer.Stop();
                if (splash != null && !splash.IsDisposed) splash.Close();
            };

            saveTimer.Tick += (s, e) => { saveTimer.Stop(); Prefs.Save(); };
            pollTimer.Tick += (s, e) => PollTick();
            autoExecTimer.Tick += (s, e) => { autoExecTimer.Stop(); RunAutoExecute(); };
            pollTimer.Start();
            Application.AddMessageFilter(wheel);

            LayoutAll();
            ApplyRegion();
            ShowPage(0);
            ShowSettingsPage(0);
            NewTab();
            RefreshExplorer();
            RefreshStart();
            PollTick();

            Log("Niox started.", Theme.Accent);
            Log("Workspace: " + ScriptsDir, Theme.Muted);
            Term("Niox terminal ready.", Theme.Muted);
            Term("Executor starting...", Theme.Accent);
        }

        // ---------------------------------------------------- saved settings
        void LoadPrefs()
        {
            Prefs.Load();
            Theme.Accent = Color.FromArgb(Prefs.Int("accent", Theme.DefaultAccent.ToArgb()) | unchecked((int)0xFF000000));
            Theme.SetText(Color.FromArgb(Prefs.Int("text", Theme.DefaultText.ToArgb()) | unchecked((int)0xFF000000)));

            fontSize = Prefs.Int("font", 11);
            showLines = Prefs.Bool("lines", true);
            showMap = Prefs.Bool("map", true);
            hiLine = Prefs.Bool("hiline", true);
            wrap = Prefs.Bool("wrap", false);
            confirmClose = Prefs.Bool("confirmclose", true);
            notifOn = Prefs.Bool("notif", true);
            consoleOn = Prefs.Bool("console", true);
            autoInject = Prefs.Bool("autoinject", false);   // off by default
            runAuto = Prefs.Bool("runauto", true);

            Backdrop.Dim = Prefs.Int("dim", 55);
            Backdrop.PanelPct = Prefs.Int("panelpct", 78);
            string bg = Prefs.Str("bg", "");
            if (bg.Length > 0 && File.Exists(bg)) Backdrop.Load(bg);
        }

        void SaveSoon() { saveTimer.Stop(); saveTimer.Start(); }

        // ---------------------------------------------------- splash / startup checks
        void BeginSplash()
        {
            splash = new SplashForm();
            splash.Fading += p => { Opacity = Math.Min(1.0, p * 1.15); };
            splash.Completed += () =>
            {
                Opacity = 1;
                Activate();
                AfterLoad();
            };
            splash.Show();
            splash.Begin();
        }

        void AfterLoad()
        {
            Term("Executor ready.", Theme.Green);
            if (!DotNetCheck.Ok())
            {
                Log(".NET Framework 4.7.2 or newer was not found.", Theme.Red, true);
                if (Confirm.Ask(this, ".NET not found",
                    "Niox needs .NET Framework 4.7.2 or newer. Install it to avoid crashes. Open the download page now?",
                    "Download", "Later", false))
                {
                    try { Process.Start(DotNetCheck.Url); } catch { }
                }
            }
        }

        // ---------------------------------------------------- window
        void ApplyRegion()
        {
            if (window == null) return;
            if (WindowState == FormWindowState.Maximized) { Region = null; window.Radius = 0; return; }
            window.Radius = 12;
            using (var p = G.Round(new RectangleF(0, 0, Width, Height), 14)) Region = new Region(p);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (WindowState != FormWindowState.Normal) return;
            G.Smooth(e.Graphics);
            using (var p = G.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 14))
            using (var pen = new Pen(Theme.Line))
                e.Graphics.DrawPath(pen, p);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && WindowState == FormWindowState.Normal) // WM_NCHITTEST: resize from edges
            {
                long lp = m.LParam.ToInt64();
                int sx = (short)(lp & 0xFFFF), sy = (short)((lp >> 16) & 0xFFFF);
                var pt = PointToClient(new Point(sx, sy));
                const int g = 6;
                bool l = pt.X < g, r = pt.X >= Width - g, t = pt.Y < g, b = pt.Y >= Height - g;
                int code = 0;
                if (t && l) code = 13;
                else if (t && r) code = 14;
                else if (b && l) code = 16;
                else if (b && r) code = 17;
                else if (l) code = 10;
                else if (r) code = 11;
                else if (t) code = 12;
                else if (b) code = 15;
                if (code != 0) { m.Result = (IntPtr)code; return; }
            }
            base.WndProc(ref m);
        }

        void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else
            {
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
        }

        void LayoutAll()
        {
            if (window == null) return;
            window.SetBounds(6, 6, ClientSize.Width - 12, ClientSize.Height - 12);
        }

        void BuildWindow()
        {
            window = new WindowPanel { Radius = 12, Line = Color.Transparent };
            Backdrop.Anchor = window;
            Controls.Add(window);

            header = new HeaderBar();
            nav = new NavPill
            {
                Items = new[] { "Start", "Scripts", "Settings" },
                IconKeys = new[] { "homestart", "editortab", "confingnew" },
                Size = new Size(390, 44)
            };
            nav.Changed += i => ShowPage(i);

            bMin = new WinBtn(0); bMax = new WinBtn(1); bClose = new WinBtn(2);
            bMin.Click += (s, e) => WindowState = FormWindowState.Minimized;
            bMax.Click += (s, e) => ToggleMaximize();
            bClose.Click += (s, e) => Close();
            tip.SetToolTip(bMin, "Minimize");
            tip.SetToolTip(bMax, "Maximize");
            tip.SetToolTip(bClose, "Close");

            header.Controls.Add(nav);
            header.Controls.Add(bMin);
            header.Controls.Add(bMax);
            header.Controls.Add(bClose);
            header.Resize += (s, e) =>
            {
                nav.Location = new Point((header.Width - nav.Width) / 2, (header.Height - nav.Height) / 2);
                bClose.Location = new Point(header.Width - bClose.Width, (header.Height - bClose.Height) / 2);
                bMax.Location = new Point(bClose.Left - bMax.Width - 2, bClose.Top);
                bMin.Location = new Point(bMax.Left - bMin.Width - 2, bClose.Top);
            };

            window.Controls.Add(header);

            pageStart = new TPanel();
            pageScripts = new TPanel { Visible = false };
            pageSettings = new TPanel { Visible = false };
            window.Controls.Add(pageStart);
            window.Controls.Add(pageScripts);
            window.Controls.Add(pageSettings);
            sbar = new StatusBar();
            window.Controls.Add(sbar);

            window.Resize += (s, e) =>
            {
                int W = window.Width, H = window.Height;
                header.SetBounds(14, 8, W - 28, 52);
                var body = new Rectangle(14, 68, W - 28, H - 106);
                sbar.SetBounds(14, H - 32, W - 28, 24);
                pageStart.Bounds = body;
                pageScripts.Bounds = body;
                pageSettings.Bounds = body;
            };
        }

        void ShowPage(int i)
        {
            nav.Selected = i;
            if (sbar != null) { sbar.Left = i == 0 ? "Start" : (i == 1 ? "Scripts" : "Settings"); sbar.Invalidate(); }
            pageStart.Visible = i == 0;
            pageScripts.Visible = i == 1;
            pageSettings.Visible = i == 2;
            if (i == 0) RefreshStart();
            if (i == 1)
            {
                RefreshExplorer();
                if (current != null) current.Editor.Focus();
            }
        }

        // ---------------------------------------------------- START page
        void BuildStartPage()
        {
            start = new StartView { Dock = DockStyle.Fill };
            pageStart.Controls.Add(start);

            for (int i = 0; i < start.Steps.Length; i++) start.Steps[i] = Prefs.Bool("step" + i, false);

            string[] tips =
            {
                "Press Ctrl+S to save a script. It shows up in the Explorer right away.",
                "Drag a script onto AUTO EXECUTE and it runs every time you attach.",
                "Right click a tab (or double click it) to edit its name.",
                "Click the PID button to pick a Roblox client or kill it.",
                "You can set your own wallpaper in Settings > Background.",
                "Turn on Auto inject in Settings > Execution to attach without clicking."
            };
            start.Tip = tips[DateTime.Now.DayOfYear % tips.Length];

            start.ActionClicked += i =>
            {
                switch (i)
                {
                    case 0: NewTab(); ShowPage(1); break;
                    case 1: OpenDialog(); break;
                    case 2: InjectScript(false); break;
                    case 3: ShowPage(1); search.Focus(); break;
                    case 4: ShowPage(2); break;
                }
            };
            start.RecentClicked += p => OpenPath(p);
        }

        void RefreshStart()
        {
            if (start == null) return;
            start.Recent = LoadRecent().Where(p => File.Exists(p)).ToList();
            start.Invalidate();
        }

        void SetStep(int i)
        {
            if (start == null || start.Steps[i]) return;
            start.Steps[i] = true;
            Prefs.Set("step" + i, true);
            SaveSoon();
            start.Invalidate();
        }

        List<string> LoadRecent()
        {
            return Prefs.Str("recent", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        void AddRecent(string path)
        {
            var l = LoadRecent();
            l.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            l.Insert(0, path);
            if (l.Count > 8) l.RemoveRange(8, l.Count - 8);
            Prefs.Set("recent", string.Join("|", l.ToArray()));
            SaveSoon();
            RefreshStart();
        }

        void RemoveRecent(string path)
        {
            var l = LoadRecent();
            l.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            Prefs.Set("recent", string.Join("|", l.ToArray()));
            SaveSoon();
        }

        // ---------------------------------------------------- SCRIPTS page
        void BuildScriptsPage()
        {
            // ---- explorer
            explorer = new RoundPanel { Radius = 8, Fill = Theme.Panel, Line = Theme.LineSoft };
            var exTitle = new Lbl { Text = "Explorer", Font = Theme.UI(11, FontStyle.Bold), Location = new Point(16, 14) };
            btnNewScript = new PillButton { IconKey = "newscript", IconSize = 24, Size = new Size(36, 34), Outline = true };
            btnNewScript.Click += (s, e) => NewTab();
            tip.SetToolTip(btnNewScript, "New script (Ctrl+N)");

            var sbox = new RoundPanel { Radius = 10, Fill = Theme.Card, Line = Theme.LineSoft, Solid = true };
            search = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Theme.UI(10),
                Location = new Point(12, 9),
                Width = 170
            };
            search.HandleCreated += (s, e) => N.SendMessageStr(search.Handle, 0x1501, (IntPtr)1, "Filter scripts...");
            search.TextChanged += (s, e) => RefreshExplorer();
            sbox.Controls.Add(search);

            tree = new ExplorerTree { IsOpen = it => files.Any(f => f.Path == it.Path) };
            tree.ItemClick += it => OpenPath(it.Path);
            tree.DeleteRequested += it => DeleteScript(it);
            tree.ContextRequested += (it, pt) => ShowItemMenu(it, pt);
            tree.MoveRequested += (it, toAuto) => MoveItem(it, toAuto);

            explorer.Controls.Add(exTitle);
            explorer.Controls.Add(btnNewScript);
            explorer.Controls.Add(sbox);
            explorer.Controls.Add(tree);
            explorer.Resize += (s, e) =>
            {
                int w = explorer.Width, h = explorer.Height;
                btnNewScript.Location = new Point(w - 48, 10);
                sbox.SetBounds(12, 56, w - 24, 38);
                search.Width = sbox.Width - 24;
                search.Top = (sbox.Height - search.Height) / 2;
                tree.SetBounds(6, 104, w - 12, h - 112);
            };

            // ---- tabs
            tabBar = new TabBar { Files = files };
            tabBar.SelectRequested += f => SelectFile(f);
            tabBar.CloseRequested += f => RequestClose(f);
            tabBar.AddRequested += () => NewTab();
            tabBar.RenameRequested += f => RenameFile(f);
            tabBar.ContextRequested += (f, pt) => ShowTabMenu(f, pt);

            // ---- right side of the tab row: PID button, caret info, console toggle
            clientPill = new ClientPill();
            clientPill.Click += (s, e) => ShowClientMenu();
            tip.SetToolTip(clientPill, "Roblox clients (PID)");

            statusInfo = new Lbl
            {
                AutoFit = false,
                Size = new Size(100, 22),
                Role = 1,
                Font = Theme.UI(8.5f),
                Text = "Ln 1, Col 1",
                Flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            };
            btnTerm = new PillButton { IconKey = "consoleterminal", Size = new Size(40, 36), Outline = true };
            btnTerm.Click += (s, e) => { consoleOn = !consoleOn; Prefs.Set("console", consoleOn); SaveSoon(); LayoutScripts(); };
            tip.SetToolTip(btnTerm, "Show / hide console");

            // ---- editor
            editorHost = new RoundPanel { Radius = 8, Fill = Theme.CodeBg, Line = Theme.LineSoft };
            editorHost.Resize += (s, e) => { LayoutHosts(); PositionPill(); };

            // ---- floating buttons
            pill = new RoundPanel
            {
                Radius = 10,
                Fill = Color.FromArgb(242, Theme.Card),
                Line = Theme.Line,
                Solid = true,
                Size = new Size(392, 58)
            };
            btnExec = new PillButton { Caption = "Execute", IconKey = "execut", Primary = true, Size = new Size(132, 44), Location = new Point(7, 7) };
            btnInject = new PillButton { IconKey = "injectnew", Size = new Size(48, 44), Location = new Point(143, 7) };
            btnOpen = new PillButton { Caption = "Open", IconKey = "pasta", Size = new Size(94, 44), Location = new Point(195, 7) };
            btnSave = new PillButton { IconKey = "save", Size = new Size(48, 44), Location = new Point(293, 7) };
            btnClear = new PillButton { IconKey = "lixeira", Size = new Size(48, 44), Location = new Point(345, 7) };
            btnExec.Click += (s, e) => ExecuteScript();
            btnInject.Click += (s, e) => InjectScript(false);
            btnOpen.Click += (s, e) => OpenDialog();
            btnSave.Click += (s, e) => SaveFile(current);
            btnClear.Click += (s, e) => ClearEditor();
            tip.SetToolTip(btnExec, "Execute (F5)");
            tip.SetToolTip(btnInject, "Inject");
            tip.SetToolTip(btnOpen, "Open file (Ctrl+O)");
            tip.SetToolTip(btnSave, "Save (Ctrl+S)");
            tip.SetToolTip(btnClear, "Clear editor");
            pill.Controls.Add(btnExec);
            pill.Controls.Add(btnInject);
            pill.Controls.Add(btnOpen);
            pill.Controls.Add(btnSave);
            pill.Controls.Add(btnClear);
            editorHost.Controls.Add(pill);

            BuildConsole();

            pageScripts.Controls.Add(explorer);
            pageScripts.Controls.Add(tabBar);
            pageScripts.Controls.Add(clientPill);
            pageScripts.Controls.Add(statusInfo);
            pageScripts.Controls.Add(btnTerm);
            pageScripts.Controls.Add(editorHost);
            pageScripts.Controls.Add(consolePanel);
            pageScripts.Resize += (s, e) => LayoutScripts();
        }

        void LayoutScripts()
        {
            if (explorer == null) return;
            int W = pageScripts.Width, H = pageScripts.Height;
            if (W <= 0 || H <= 0) return;

            int exW = 250;
            explorer.SetBounds(0, 0, exW, H);
            int mx = exW + 10, mw = W - mx;

            btnTerm.Location = new Point(W - btnTerm.Width, 3);
            clientPill.Location = new Point(btnTerm.Left - 8 - clientPill.Width, 4);
            statusInfo.Location = new Point(clientPill.Left - 8 - statusInfo.Width, 11);
            tabBar.SetBounds(mx, 0, Math.Max(100, statusInfo.Left - mx - 10), 44);

            consolePanel.Visible = consoleOn;
            int ch = consoleOn ? 184 : 0;
            int eh = H - 52 - (ch > 0 ? ch + 10 : 0);
            editorHost.SetBounds(mx, 52, mw, Math.Max(80, eh));
            consolePanel.SetBounds(mx, H - ch, mw, ch);
            PositionPill();
        }

        void LayoutHosts()
        {
            foreach (var f in files)
                f.Host.SetBounds(6, 6, Math.Max(10, editorHost.Width - 12), Math.Max(10, editorHost.Height - 12));
        }

        void PositionPill()
        {
            if (pill == null) return;
            int right = 24 + (showMap ? 100 : 0);
            pill.Location = new Point(Math.Max(10, editorHost.Width - pill.Width - right), Math.Max(10, editorHost.Height - pill.Height - 18));
            pill.BringToFront();
        }

        // ---------------------------------------------------- menus
        ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkRenderer();
            m.BackColor = Theme.Card;
            m.ForeColor = Theme.Text;
            m.Font = Theme.UI(10);
            m.ShowImageMargin = true;
            m.ImageScalingSize = new Size(20, 20);
            return m;
        }

        ToolStripMenuItem MI(string text, string icon, Action act, Color? tint = null)
        {
            var it = new ToolStripMenuItem(text);
            it.Padding = new Padding(2, 7, 6, 7);
            Color c = tint ?? Theme.Text;
            it.Tag = c;
            if (icon != null) it.Image = Ico.Get(icon, c, 20);
            it.Click += (s, e) => act();
            return it;
        }

        void ShowMenu(ContextMenuStrip m, Point screen)
        {
            m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
            m.Show(screen);
        }

        void ShowTabMenu(ScriptFile f, Point screen)
        {
            var m = NewMenu();
            m.Items.Add(MI("Edit name", "editortab", () => RenameFile(f)));
            m.Items.Add(MI("Save", "save", () => SaveFile(f)));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(MI("Close tab", null, () => RequestClose(f), Theme.Red));
            ShowMenu(m, screen);
        }

        void ShowItemMenu(ScriptItem it, Point screen)
        {
            var m = NewMenu();
            m.Items.Add(MI("Open", "pasta", () => OpenPath(it.Path)));
            m.Items.Add(MI("Edit name", "editortab", () => RenameItem(it)));
            m.Items.Add(MI(it.Auto ? "Move to Scripts" : "Move to Auto Execute", "autoexecute", () => MoveItem(it, !it.Auto)));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(MI("Delete", "lixeira", () => DeleteScript(it), Theme.Red));
            ShowMenu(m, screen);
        }

        static Bitmap CheckBmp(bool on)
        {
            var b = new Bitmap(20, 20);
            if (on)
                using (var g = Graphics.FromImage(b))
                {
                    G.Smooth(g);
                    G.Check(g, 10, 10, 5.5f, Theme.Accent, 2.2f);
                }
            return b;
        }

        void ShowClientMenu()
        {
            var m = NewMenu();
            if (pids.Count == 0)
            {
                var none = new ToolStripMenuItem("No Roblox client found");
                none.Padding = new Padding(2, 7, 6, 7);
                none.Enabled = false;
                m.Items.Add(none);
            }
            else
            {
                var all = new ToolStripMenuItem("All clients");
                all.Padding = new Padding(2, 7, 6, 7);
                all.Image = CheckBmp(selectedPid == 0);
                all.Click += (s, e) => { selectedPid = 0; UpdateClientUi(); };
                m.Items.Add(all);

                foreach (int pid in pids)
                {
                    int p = pid;
                    var it = new ToolStripMenuItem("PID " + p + (injectedPids.Contains(p) ? "    attached" : ""));
                    it.Padding = new Padding(2, 7, 6, 7);
                    it.Image = CheckBmp(selectedPid == p);
                    it.Click += (s, e) => { selectedPid = p; UpdateClientUi(); };
                    m.Items.Add(it);
                }
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add(MI(selectedPid > 0 ? "Kill selected" : "Kill all clients", "killroblox", () => KillSelected(), Theme.Red));
            }
            m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
            m.Show(clientPill, new Point(0, clientPill.Height + 4));
        }

        // ---------------------------------------------------- Roblox clients (PID)
        static int[] GetRobloxPids()
        {
            var list = new List<int>();
            foreach (string n in new[] { "RobloxPlayerBeta", "Windows10Universal" })
            {
                Process[] ps = null;
                try { ps = Process.GetProcessesByName(n); } catch { }
                if (ps == null) continue;
                foreach (var p in ps)
                {
                    try { list.Add(p.Id); } catch { }
                    p.Dispose();
                }
            }
            list.Sort();
            return list.ToArray();
        }

        void PollTick()
        {
            int[] now = GetRobloxPids();
            bool changed = now.Length != pids.Count;

            foreach (int pid in now)
                if (!pids.Contains(pid))
                {
                    changed = true;
                    Term("Roblox client detected. PID " + pid, Theme.Accent);
                }
            foreach (int pid in pids.ToArray())
                if (Array.IndexOf(now, pid) < 0)
                {
                    changed = true;
                    Term("Roblox PID " + pid + " closed.", Theme.Muted);
                    injectedPids.Remove(pid);
                    firstSeen.Remove(pid);
                    if (selectedPid == pid) selectedPid = 0;
                }

            pids.Clear();
            pids.AddRange(now);
            if (changed) UpdateClientUi();

            // auto inject (off by default)
            if (!autoInject) return;
            foreach (int pid in pids)
            {
                if (injectedPids.Contains(pid)) continue;
                if (!firstSeen.ContainsKey(pid))
                {
                    firstSeen[pid] = DateTime.Now;
                    Log("Roblox detected (PID " + pid + "). Auto inject starting...", Theme.Accent);
                    Notify(2, "Roblox detected", "Auto inject will start in a moment.");
                    continue;
                }
                if ((DateTime.Now - firstSeen[pid]).TotalSeconds < 6) continue;
                if ((DateTime.Now - lastTry).TotalSeconds < 8) continue;
                lastTry = DateTime.Now;
                InjectScript(true);
                break;
            }
        }

        string TargetText()
        {
            if (selectedPid > 0) return "PID " + selectedPid;
            if (pids.Count == 1) return "PID " + pids[0];
            if (pids.Count > 1) return "all clients";
            return "Roblox";
        }

        void UpdateClientUi()
        {
            if (clientPill != null)
            {
                clientPill.Count = pids.Count;
                clientPill.AnyInjected = injectedPids.Count > 0;
                if (pids.Count == 0) clientPill.Caption = "No client";
                else if (selectedPid > 0) clientPill.Caption = "PID " + selectedPid;
                else if (pids.Count == 1) clientPill.Caption = "PID " + pids[0];
                else clientPill.Caption = pids.Count + " clients";
                clientPill.Invalidate();
            }
            if (start != null)
            {
                start.Attached = injectedPids.Count > 0;
                if (injectedPids.Count > 0)
                    start.StatusText = injectedPids.Count == 1 ? "1 client attached." : injectedPids.Count + " clients attached.";
                else if (pids.Count > 0) start.StatusText = "Roblox is open. Attach to start.";
                else start.StatusText = "No Roblox client found.";
                start.Invalidate();
            }
            if (sbar != null)
            {
                sbar.Dot = injectedPids.Count > 0 ? Theme.Green : (pids.Count > 0 ? Theme.Accent : Theme.Dim);
                sbar.Right = injectedPids.Count > 0 ? "Attached" : (pids.Count > 0 ? "Roblox detected" : "Not attached");
                sbar.Invalidate();
            }
        }

        void KillSelected()
        {
            var targets = selectedPid > 0 ? new List<int> { selectedPid } : new List<int>(pids);
            if (targets.Count == 0) return;
            string what = targets.Count == 1 ? "PID " + targets[0] : targets.Count + " Roblox clients";
            if (!Confirm.Ask(this, "Kill Roblox?", what + " will be closed right away. Unsaved progress in the game is lost.", "Kill", "Cancel", true)) return;
            foreach (int pid in targets)
            {
                try
                {
                    using (var p = Process.GetProcessById(pid)) p.Kill();
                    Term("Killed PID " + pid, Theme.Red);
                }
                catch (Exception ex) { Term("Could not kill PID " + pid + ": " + ex.Message, Theme.Red); }
            }
            Notify(2, "Roblox closed", what);
            selectedPid = 0;
            PollTick();
            UpdateClientUi();
        }

        // ---------------------------------------------------- explorer (files on disk)
        static List<string> ScriptFilesIn(string dir)
        {
            var l = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    string x = Path.GetExtension(f).ToLowerInvariant();
                    if (x == ".lua" || x == ".luau") l.Add(f);
                }
            }
            catch { }
            l.Sort(StringComparer.OrdinalIgnoreCase);
            return l;
        }

        List<ScriptItem> Collect(string dir, bool auto, string q)
        {
            var items = new List<ScriptItem>();
            foreach (string f in ScriptFilesIn(dir))
            {
                string n = Path.GetFileName(f);
                if (q.Length > 0 && !n.ToLowerInvariant().Contains(q)) continue;
                long size = 0;
                try { size = new FileInfo(f).Length; } catch { }
                items.Add(new ScriptItem { Name = n, Path = f, Size = size, Auto = auto });
            }
            return items;
        }

        void RefreshExplorer()
        {
            if (tree == null) return;
            string q = (search.Text ?? "").Trim().ToLowerInvariant();
            tree.SetItems(Collect(ScriptsDir, false, q), Collect(AutoDir, true, q));
        }

        static string UniquePath(string dir, string name)
        {
            string p = Path.Combine(dir, name);
            if (!File.Exists(p)) return p;
            string b = Path.GetFileNameWithoutExtension(name), x = Path.GetExtension(name);
            for (int i = 2; i < 1000; i++)
            {
                p = Path.Combine(dir, b + " (" + i + ")" + x);
                if (!File.Exists(p)) return p;
            }
            return p;
        }

        static string CleanName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "");
            s = s.Trim();
            if (s.Length == 0) return s;
            string ext = Path.GetExtension(s).ToLowerInvariant();
            if (ext != ".lua" && ext != ".luau") s += ".lua";
            return s;
        }

        void MoveItem(ScriptItem it, bool toAuto)
        {
            try
            {
                string dir = toAuto ? AutoDir : ScriptsDir;
                if (string.Equals(Path.GetDirectoryName(it.Path), dir, StringComparison.OrdinalIgnoreCase)) return;
                Directory.CreateDirectory(dir);
                string np = UniquePath(dir, it.Name);
                File.Move(it.Path, np);
                var f = files.FirstOrDefault(x => x.Path == it.Path);
                if (f != null) f.Path = np;
                RemoveRecent(it.Path);
                AddRecent(np);
                Log((toAuto ? "Added to Auto Execute: " : "Moved to Scripts: ") + it.Name, Theme.Accent);
                Notify(0, toAuto ? "Added to Auto Execute" : "Moved to Scripts", it.Name);
                if (toAuto) SetStep(3);
                RefreshExplorer();
            }
            catch (Exception ex) { Log("Move failed: " + ex.Message, Theme.Red, true); Notify(1, "Move failed", ex.Message); }
        }

        void RenameFile(ScriptFile f)
        {
            string nn = Prompt.Ask(this, "Edit name", "Type a new name for this script.", f.Name, "Rename");
            if (nn == null) return;
            nn = CleanName(nn);
            if (nn.Length == 0 || nn == f.Name) return;
            if (f.Path != null)
            {
                try
                {
                    string np = Path.Combine(Path.GetDirectoryName(f.Path), nn);
                    if (File.Exists(np)) { Notify(1, "Name already used", nn); return; }
                    File.Move(f.Path, np);
                    RemoveRecent(f.Path);
                    f.Path = np;
                    AddRecent(np);
                }
                catch (Exception ex) { Notify(1, "Rename failed", ex.Message); return; }
            }
            f.Name = nn;
            tabBar.Invalidate();
            RefreshExplorer();
            Log("Renamed to " + nn, Theme.Accent);
            Notify(0, "Renamed", nn);
        }

        void RenameItem(ScriptItem it)
        {
            var open = files.FirstOrDefault(x => x.Path == it.Path);
            if (open != null) { RenameFile(open); return; }

            string nn = Prompt.Ask(this, "Edit name", "Type a new name for this script.", it.Name, "Rename");
            if (nn == null) return;
            nn = CleanName(nn);
            if (nn.Length == 0 || nn == it.Name) return;
            try
            {
                string np = Path.Combine(Path.GetDirectoryName(it.Path), nn);
                if (File.Exists(np)) { Notify(1, "Name already used", nn); return; }
                File.Move(it.Path, np);
                RemoveRecent(it.Path);
                AddRecent(np);
                RefreshExplorer();
                Notify(0, "Renamed", nn);
            }
            catch (Exception ex) { Notify(1, "Rename failed", ex.Message); }
        }

        void DeleteScript(ScriptItem it)
        {
            if (!Confirm.Ask(this, "Delete script?", "\"" + it.Name + "\" will be deleted from disk. This cannot be undone.", "Delete", "Cancel", true)) return;
            try
            {
                File.Delete(it.Path);
                var f = files.FirstOrDefault(x => x.Path == it.Path);
                if (f != null) CloseTab(f);
                RemoveRecent(it.Path);
                Log("Script deleted: " + it.Name, Theme.Muted);
                Notify(2, "Script deleted", it.Name);
                RefreshExplorer();
                RefreshStart();
            }
            catch (Exception ex) { Log("Delete failed: " + ex.Message, Theme.Red, true); Notify(1, "Delete failed", ex.Message); }
        }

        // ---------------------------------------------------- console (Console / Terminal / Output)
        void BuildConsole()
        {
            consolePanel = new RoundPanel { Radius = 8, Fill = Theme.Panel, Line = Theme.LineSoft };

            cTabs = new ConsoleTabs { Location = new Point(10, 2) };
            cTabs.Changed += i => ShowConsoleTab(i);

            consoleCount = new Lbl { Text = "0 lines", Font = Theme.UI(8.5f), Role = 2 };
            btnConsoleClear = new PillButton { IconKey = "lixeira", Size = new Size(38, 34) };
            btnConsoleClear.Click += (s, e) => ClearConsole();
            tip.SetToolTip(btnConsoleClear, "Clear this tab");

            console = new ConsoleView();
            terminal = new ConsoleView { Visible = false };
            output = new ConsoleView { Visible = false };

            consolePanel.Controls.Add(cTabs);
            consolePanel.Controls.Add(consoleCount);
            consolePanel.Controls.Add(btnConsoleClear);
            consolePanel.Controls.Add(console);
            consolePanel.Controls.Add(terminal);
            consolePanel.Controls.Add(output);
            consolePanel.Resize += (s, e) => LayoutConsole();
        }

        void LayoutConsole()
        {
            if (consolePanel == null) return;
            int w = consolePanel.Width, h = consolePanel.Height;
            consoleCount.Location = new Point(cTabs.Right + 10, 14);
            btnConsoleClear.Location = new Point(w - 48, 4);
            var b = new Rectangle(16, 52, Math.Max(20, w - 32), Math.Max(20, h - 62));
            console.Bounds = b; terminal.Bounds = b; output.Bounds = b;
        }

        void ShowConsoleTab(int i)
        {
            cTabs.Selected = i;
            cTabs.Unread[i] = false;
            console.Visible = i == 0;
            terminal.Visible = i == 1;
            output.Visible = i == 2;
            UpdateCount();
            cTabs.Invalidate();
        }

        ConsoleView ActiveView()
        {
            return cTabs.Selected == 1 ? terminal : (cTabs.Selected == 2 ? output : console);
        }

        void UpdateCount()
        {
            int n = ActiveView().Count;
            consoleCount.Text = n + (n == 1 ? " line" : " lines");
        }

        void Mark(int tab)
        {
            if (cTabs.Selected != tab) { cTabs.Unread[tab] = true; cTabs.Invalidate(); }
            UpdateCount();
        }

        // Console = app log, Terminal = PID / executor / inject, Output = scripts sent + errors
        void Log(string msg, Color c, bool err = false)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Log(msg, c, err))); return; }
            if (console == null || console.IsDisposed) return;
            console.Add(new LogEntry { T = DateTime.Now, Msg = msg, C = c });
            Mark(0);
        }

        void Term(string msg, Color c)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Term(msg, c))); return; }
            if (terminal == null || terminal.IsDisposed) return;
            terminal.Add(new LogEntry { T = DateTime.Now, Msg = msg, C = c });
            Mark(1);
        }

        void Out(string msg, Color c)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Out(msg, c))); return; }
            if (output == null || output.IsDisposed) return;
            output.Add(new LogEntry { T = DateTime.Now, Msg = msg, C = c });
            Mark(2);
        }

        void ClearConsole()
        {
            ActiveView().Clear();
            if (cTabs.Selected == 0) Log("Console cleared.", Theme.Accent);
            UpdateCount();
        }

        // ---------------------------------------------------- tabs / files
        static void Rehighlight(FastColoredTextBox tb)
        {
            LuaSyntax.Highlight(tb.LinesCount > 3000 ? tb.VisibleRange : tb.Range);
        }

        void ApplyEditor(ScriptFile f)
        {
            var tb = f.Editor;
            if (fontSize <= 0) fontSize = 11;
            tb.Font = Theme.Code(fontSize);   // cached font: never Dispose() it
            tb.ForeColor = Theme.Text;
            tb.LineNumberColor = Theme.Dim;
            tb.ShowLineNumbers = showLines;
            tb.WordWrap = wrap;
            tb.CurrentLineColor = hiLine ? Color.FromArgb(22, 255, 255, 255) : Color.Transparent;
            tb.CaretColor = Theme.Accent;
            tb.SelectionColor = Color.FromArgb(70, Theme.Accent);
            if (f.Map != null)
            {
                f.Map.Visible = showMap;
                f.Map.ForeColor = Theme.Accent;
            }
        }

        ScriptFile CreateFile(string name, string path, string content)
        {
            var f = new ScriptFile { Name = name, Path = path, Loading = true };
            f.Host = new Panel { BackColor = Theme.CodeBg, Visible = false };

            var tb = new FastColoredTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.CodeBg,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                Language = Language.Custom,
                AcceptsTab = true,
                TabLength = 4,
                LineNumberColor = Theme.Dim,
                IndentBackColor = Theme.CodeBg,
                PaddingBackColor = Theme.CodeBg,
                ServiceLinesColor = Theme.CodeBg,
                DelayedTextChangedInterval = 120,
                LeftPadding = 6
            };
            LuaSyntax.Register(tb);

            tb.TextChanged += (s, e) =>
            {
                if (!f.Loading && !f.Modified) { f.Modified = true; tabBar.Invalidate(); }
            };
            tb.TextChangedDelayed += (s, e) => Rehighlight(tb);
            tb.VisibleRangeChangedDelayed += (s, e) => { if (tb.LinesCount > 3000) Rehighlight(tb); };
            tb.SelectionChangedDelayed += (s, e) => { if (f == current) UpdateCaretInfo(); };
            tb.HandleCreated += (s, e) => N.DarkScroll(tb);

            f.Editor = tb;
            f.Map = new DocumentMap { Dock = DockStyle.Right, Width = 100, BackColor = Theme.CodeBg, ForeColor = Theme.Accent };

            f.Host.Controls.Add(tb);
            f.Host.Controls.Add(f.Map);
            f.Map.Target = tb;

            ApplyEditor(f);
            tb.Text = content;
            tb.ClearUndo();
            Rehighlight(tb);
            f.Loading = false;
            f.Modified = false;

            files.Add(f);
            editorHost.Controls.Add(f.Host);
            LayoutHosts();
            pill.BringToFront();
            return f;
        }

        void SelectFile(ScriptFile f)
        {
            current = f;
            foreach (var x in files) x.Host.Visible = x == f;
            tabBar.SetCurrent(f);
            pill.BringToFront();
            f.Editor.Focus();
            UpdateCaretInfo();
            if (tree != null) tree.Invalidate();
        }

        void UpdateCaretInfo()
        {
            if (current == null) return;
            var p = current.Editor.Selection.Start;
            statusInfo.Text = "Ln " + (p.iLine + 1) + ", Col " + (p.iChar + 1);
        }

        void NewTab()
        {
            untitled++;
            string content = untitled == 1
                ? "-- Niox\n\nprint(\"Hello from Niox!\")\n"
                : "-- New Lua script\n\n";
            var f = CreateFile("Script" + untitled + ".lua", null, content);
            SelectFile(f);
            if (console != null && untitled > 1) Log("New tab created: " + f.Name, Theme.Accent);
        }

        void OpenPath(string path)
        {
            var ex = files.FirstOrDefault(d => d.Path == path);
            if (ex != null) { SelectFile(ex); ShowPage(1); return; }
            try
            {
                string content = File.ReadAllText(path).Replace("\r\n", "\n");
                var f = CreateFile(Path.GetFileName(path), path, content);
                SelectFile(f);
                ShowPage(1);
                AddRecent(path);
                Log("File opened: " + f.Name, Theme.Accent);
            }
            catch (Exception ex2) { Log("Open failed: " + ex2.Message, Theme.Red, true); Notify(1, "Open failed", ex2.Message); }
        }

        void OpenDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Open script";
                dlg.InitialDirectory = ScriptsDir;
                dlg.Filter = "Lua (*.lua;*.luau)|*.lua;*.luau|Text (*.txt)|*.txt|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenPath(dlg.FileName);
            }
        }

        // Ctrl+S: saves straight into the Scripts folder (no dialog) so it shows up in the Explorer
        bool SaveFile(ScriptFile f)
        {
            if (f == null) return false;
            try
            {
                string path = f.Path;
                if (path == null)
                {
                    string name = CleanName(f.Name);
                    if (name.Length == 0) name = "Script.lua";
                    path = UniquePath(ScriptsDir, name);
                }
                File.WriteAllText(path, f.Editor.Text, new UTF8Encoding(false));
                f.Path = path;
                f.Name = Path.GetFileName(path);
                f.Modified = false;
                tabBar.Invalidate();
                AddRecent(path);
                RefreshExplorer();
                SetStep(2);
                Log("File saved: " + path, Theme.Accent);
                Notify(0, "Saved", f.Name + " was saved to your scripts.");
                return true;
            }
            catch (Exception ex)
            {
                Log("Save failed: " + ex.Message, Theme.Red, true);
                Notify(1, "Save failed", ex.Message);
                return false;
            }
        }

        void RequestClose(ScriptFile f)
        {
            if (confirmClose)
            {
                string msg = f.Modified
                    ? "The tab \"" + f.Name + "\" has unsaved changes. Close it anyway?"
                    : "Are you sure you want to close the tab \"" + f.Name + "\"?";
                if (!Confirm.Ask(this, "Close tab?", msg, "Close", "Cancel", true)) return;
            }
            string name = f.Name;
            CloseTab(f);
        }

        void CloseTab(ScriptFile f)
        {
            int i = files.IndexOf(f);
            if (i < 0) return;
            files.Remove(f);
            editorHost.Controls.Remove(f.Host);
            f.Host.Dispose();
            if (files.Count == 0) NewTab();
            else SelectFile(files[Math.Min(i, files.Count - 1)]);
            tabBar.Invalidate();
            if (tree != null) tree.Invalidate();
        }

        void ClearEditor()
        {
            if (current == null) return;
            if (!Confirm.Ask(this, "Clear editor?", "All the text in \"" + current.Name + "\" will be erased.", "Clear", "Cancel", true)) return;
            current.Editor.Clear();
            Log("Editor cleared.", Theme.Muted);
        }

        // ---------------------------------------------------- executor
        // Single place that talks to the DLL. selectedPid 0 means "all clients".
        void SendToRoblox(string code)
        {
            bool ok = selectedPid > 0 ? NioxCore.Execute((uint)selectedPid, code) : NioxCore.ExecuteScriptAll(code);
            if (!ok) throw new Exception("NioxCore refused the script (returned false).");
        }

        void InjectScript(bool auto)
        {
            Term("Injecting into " + TargetText() + "...", Theme.Muted);
            try
            {
                bool ok;
                if (selectedPid > 0) ok = NioxCore.Attach((uint)selectedPid);
                else ok = NioxCore.AttachAll();

                if (!ok) throw new Exception("NioxCore refused the attach (returned false).");

                int[] now = GetRobloxPids();
                if (selectedPid > 0) injectedPids.Add(selectedPid);
                else foreach (int pid in now) injectedPids.Add(pid);

                string ptxt = selectedPid > 0 ? selectedPid.ToString()
                    : (now.Length > 0 ? string.Join(", ", now.Select(x => x.ToString()).ToArray()) : "unknown");
                Log(auto ? "Auto inject done." : "Inject requested!", Theme.Accent);
                Term("Injected. PID " + ptxt, Theme.Green);
                Notify(0, auto ? "Auto injected" : "Injected", "Connected to Roblox. PID " + ptxt);
                SetStep(0);
                UpdateClientUi();

                if (runAuto && ScriptFilesIn(AutoDir).Count > 0) { autoExecTimer.Stop(); autoExecTimer.Start(); }
            }
            catch (DllNotFoundException)
            {
                string msg = "NioxCore.dll was not found next to Niox.exe.";
                Log(msg, Theme.Red, true);
                Term(msg, Theme.Red);
                if (!auto) Notify(1, "NioxCore.dll missing", msg);
            }
            catch (EntryPointNotFoundException ex)
            {
                string msg = "A function is missing from NioxCore.dll: " + ex.Message;
                Log(msg, Theme.Red, true);
                Term(msg, Theme.Red);
                if (!auto) Notify(1, "NioxCore.dll outdated", msg);
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message, Theme.Red, true);
                Term("Inject failed: " + ex.Message, Theme.Red);
                if (!auto) Notify(1, "Inject failed", ex.Message);
            }
        }

        void RunAutoExecute()
        {
            var list = ScriptFilesIn(AutoDir);
            if (list.Count == 0) return;
            Term("Running " + list.Count + " Auto Execute script(s)...", Theme.Muted);
            foreach (string f in list)
            {
                string n = Path.GetFileName(f);
                try
                {
                    SendToRoblox(File.ReadAllText(f));
                    Out("Auto execute: " + n, Theme.Green);
                }
                catch (Exception ex) { Out("Auto execute failed (" + n + "): " + ex.Message, Theme.Red); }
            }
        }

        void ExecuteScript()
        {
            if (current == null) return;
            string code = current.Editor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                Notify(2, "Editor is empty", "Write a script before executing.");
                return;
            }
            try
            {
                SendToRoblox(code);
                Log("Executed: " + current.Name, Theme.Green);
                Out("Script sent: " + current.Name + " (" + code.Length + " chars)", Theme.Green);
                Term("Executed " + current.Name + " on " + TargetText(), Theme.Muted);
                Notify(0, "Executed", current.Name + " was sent successfully!");
                SetStep(1);
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message, Theme.Red, true);
                Out("Execution error: " + ex.Message, Theme.Red);
                Notify(1, "Execute failed", ex.Message);
            }
        }

        // ---------------------------------------------------- notifications
        void Notify(int type, string title, string msg)
        {
            if (!notifOn) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => Notify(type, title, msg))); return; }

            while (toasts.Count >= 4)
            {
                var old = toasts[0];
                toasts.RemoveAt(0);
                Controls.Remove(old);
                old.Dispose();
            }

            Color c = type == 0 ? Theme.Green : (type == 1 ? Theme.Red : Theme.Accent);
            var t = new ToastCard(type, title, msg, c);
            t.Done += x =>
            {
                toasts.Remove(x);
                Controls.Remove(x);
                x.Dispose();
                ArrangeToasts();
            };
            toasts.Add(t);
            ArrangeToasts();
            t.Top = t.RestY;
            Controls.Add(t);
            t.BringToFront();
            t.Begin();
        }

        void ArrangeToasts()
        {
            int y = ClientSize.Height - 24;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                y -= toasts[i].Height;
                toasts[i].RestY = y;
                toasts[i].RestX = ClientSize.Width - toasts[i].Width - 24;
                y -= 10;
            }
        }

        // ---------------------------------------------------- SETTINGS page
        Toggle Tg(bool v, Action<bool> on)
        {
            var t = new Toggle { Checked = v };
            t.CheckedChanged += (s, e) => { on(t.Checked); SaveSoon(); };
            return t;
        }

        TPanel MakeSettingsPage(string title, string sub)
        {
            var p = new TPanel { Visible = false };
            p.Controls.Add(new Lbl { Text = title, Font = Theme.UI(18, FontStyle.Bold), Location = new Point(2, 0) });
            p.Controls.Add(new Lbl { Text = sub, Font = Theme.UI(9.5f), Role = 1, Location = new Point(4, 38) });
            pageSettings.Controls.Add(p);
            return p;
        }

        SettingsCard AddCard(TPanel page, string title, int y)
        {
            var c = new SettingsCard(title) { Location = new Point(0, y), Width = 720 };
            page.Controls.Add(c);
            page.Resize += (s, e) => c.Width = Math.Max(320, Math.Min(860, page.ClientSize.Width - 10));
            return c;
        }

        void BuildSettingsPage()
        {
            settingsMenu = new RoundPanel { Radius = 8, Fill = Theme.Panel, Line = Theme.LineSoft };
            sMenu = new SideMenu
            {
                Items = new[] { "General", "Editor", "Execution", "Appearance", "Background", "About" },
                IconKeys = new[] { "confingnew", "editortab", "execut", "sol", "pasta", "logo" }
            };
            sMenu.Changed += i => ShowSettingsPage(i);
            var footer = new Lbl { Text = "Niox v" + AppInfo.Version, Font = Theme.UI(8.5f), Role = 2 };
            settingsMenu.Controls.Add(sMenu);
            settingsMenu.Controls.Add(footer);
            settingsMenu.Resize += (s, e) =>
            {
                sMenu.SetBounds(10, 12, settingsMenu.Width - 20, 6 * (SideMenu.IH + SideMenu.GAP));
                footer.Location = new Point(18, settingsMenu.Height - 30);
            };
            pageSettings.Controls.Add(settingsMenu);

            sPages = new TPanel[6];

            // ---- GENERAL
            var pg = MakeSettingsPage("General", "Window, alerts and console.");
            var c1 = AddCard(pg, "WINDOW", 72);
            c1.AddRow("Always on top", "Keeps Niox above other windows.", Tg(false, v => TopMost = v));
            c1.AddRow("Confirm before closing tabs", "Shows a warning before a tab is closed.", Tg(confirmClose, v => { confirmClose = v; Prefs.Set("confirmclose", v); }));
            var c2 = AddCard(pg, "ALERTS & CONSOLE", 72 + c1.Height + 14);
            c2.AddRow("Notifications", "Shows animated alerts in the corner of the window.", Tg(notifOn, v => { notifOn = v; Prefs.Set("notif", v); }));
            c2.AddRow("Show console", "Shows Console / Terminal / Output under the editor.", Tg(consoleOn, v => { consoleOn = v; Prefs.Set("console", v); LayoutScripts(); }));
            sPages[0] = pg;

            // ---- EDITOR
            var pe = MakeSettingsPage("Editor", "Text, formatting and code display.");
            var e1 = AddCard(pe, "TEXT", 72);
            var step = new Stepper { Value = (int)fontSize };
            step.ValueChanged += (s, ev) =>
            {
                fontSize = step.Value; Prefs.Set("font", step.Value); SaveSoon();
                foreach (var f in files) ApplyEditor(f);
            };
            e1.AddRow("Font size", "Size of the text in the code editor.", step);
            e1.AddRow("Word wrap", "Wraps long lines instead of scrolling sideways.", Tg(wrap, v => { wrap = v; Prefs.Set("wrap", v); foreach (var f in files) ApplyEditor(f); }));
            var e2 = AddCard(pe, "DISPLAY", 72 + e1.Height + 14);
            e2.AddRow("Line numbers", "Shows line numbers on the left side.", Tg(showLines, v => { showLines = v; Prefs.Set("lines", v); foreach (var f in files) ApplyEditor(f); }));
            e2.AddRow("Minimap", "Shows an overview of the script on the right.", Tg(showMap, v => { showMap = v; Prefs.Set("map", v); foreach (var f in files) ApplyEditor(f); PositionPill(); }));
            e2.AddRow("Highlight current line", "Lightens the line where the caret is.", Tg(hiLine, v => { hiLine = v; Prefs.Set("hiline", v); foreach (var f in files) ApplyEditor(f); }));
            sPages[1] = pe;

            // ---- EXECUTION
            var px = MakeSettingsPage("Execution", "Injection, clients and Auto Execute.");
            var x1 = AddCard(px, "INJECTION", 72);
            x1.AddRow("Auto inject", "Injects automatically when Roblox is detected. Off by default.",
                Tg(autoInject, v =>
                {
                    autoInject = v; Prefs.Set("autoinject", v);
                    firstSeen.Clear(); lastTry = DateTime.MinValue;
                    Log(v ? "Auto inject enabled." : "Auto inject disabled.", Theme.Muted);
                    Notify(2, "Auto inject", v ? "Enabled. Niox will inject when Roblox opens." : "Disabled.");
                }));
            var injNow = new PillButton { Caption = "Inject", IconKey = "injectnew", Primary = true, Size = new Size(114, 38) };
            injNow.Click += (s, ev) => InjectScript(false);
            x1.AddRow("Inject now", "Manually injects into the running Roblox client.", injNow);

            var x2 = AddCard(px, "AUTO EXECUTE", 72 + x1.Height + 14);
            x2.AddRow("Run Auto Execute scripts", "Runs the scripts in the AUTO EXECUTE folder after every inject.",
                Tg(runAuto, v => { runAuto = v; Prefs.Set("runauto", v); }));
            var openAuto = new PillButton { Caption = "Open", IconKey = "autoexecute", Outline = true, Size = new Size(104, 38) };
            openAuto.Click += (s, ev) =>
            {
                try { Directory.CreateDirectory(AutoDir); Process.Start(AutoDir); } catch { }
            };
            x2.AddRow("Auto Execute folder", "Opens the folder in Windows Explorer.", openAuto);
            sPages[2] = px;

            // ---- APPEARANCE
            var pa = MakeSettingsPage("Appearance", "Colors for text, icons and highlights.");
            var a1 = AddCard(pa, "COLORS", 72);
            swAccent = new SwatchBar(new[]
            {
                Color.FromArgb(240, 240, 240), Color.FromArgb(160, 160, 165), Color.FromArgb(96, 165, 250),
                Color.FromArgb(45, 212, 191), Color.FromArgb(74, 222, 128), Color.FromArgb(244, 114, 182),
                Color.FromArgb(251, 146, 60), Color.FromArgb(167, 139, 250)
            });
            swAccent.Select(Theme.Accent);
            swAccent.Picked += c => { Theme.SetAccent(c); Prefs.Set("accent", c.ToArgb()); SaveSoon(); };
            a1.AddRow("Accent & icon color", "Used for icons, active tabs, buttons and highlights. The last circle opens the color panel.", swAccent);

            swText = new SwatchBar(new[]
            {
                Color.FromArgb(240, 240, 240), Color.FromArgb(190, 190, 195), Color.FromArgb(190, 225, 255),
                Color.FromArgb(190, 255, 220), Color.FromArgb(255, 236, 200), Color.FromArgb(255, 205, 225)
            });
            swText.Select(Theme.Text);
            swText.Picked += c => { Theme.SetText(c); Prefs.Set("text", c.ToArgb()); SaveSoon(); };
            a1.AddRow("Text color", "Color of the letters across the whole app.", swText);

            var resetBtn = new PillButton { Caption = "Reset colors", Outline = true, Size = new Size(124, 36) };
            resetBtn.Click += (s, ev) =>
            {
                Theme.Reset();
                Prefs.Set("accent", Theme.DefaultAccent.ToArgb());
                Prefs.Set("text", Theme.DefaultText.ToArgb());
                swAccent.Select(Theme.Accent); swText.Select(Theme.Text);
                SaveSoon();
            };
            a1.AddRow("Default colors", "Goes back to white on black.", resetBtn);

            var a2 = AddCard(pa, "NOTIFICATIONS", 72 + a1.Height + 14);
            var test = new PillButton { Caption = "Test", Primary = true, Size = new Size(92, 36) };
            test.Click += (s, ev) =>
            {
                Notify(0, "All good!", "Success notification.");
                Notify(2, "Information", "Info notification.");
                Notify(1, "Error", "Error notification.");
            };
            a2.AddRow("Test notifications", "Shows the three alert types.", test);
            sPages[3] = pa;

            // ---- BACKGROUND
            var pb = MakeSettingsPage("Background", "Use your own image as the window background.");
            var b1 = AddCard(pb, "IMAGE", 72);
            var bgBox = new TPanel { Size = new Size(214, 38) };
            var bChoose = new PillButton { Caption = "Choose image", Outline = true, Size = new Size(132, 38), Location = new Point(0, 0) };
            var bRemove = new PillButton { Caption = "Remove", Outline = true, Size = new Size(76, 38), Location = new Point(138, 0) };
            bChoose.Click += (s, ev) => ChooseBackground();
            bRemove.Click += (s, ev) =>
            {
                Backdrop.Clear(); Prefs.Set("bg", ""); RefreshBackdrop();
                Notify(2, "Background removed", "Back to the default black.");
            };
            bgBox.Controls.Add(bChoose);
            bgBox.Controls.Add(bRemove);
            b1.AddRow("Custom background", "PNG, JPG, BMP or GIF from your computer.", bgBox);

            var b2 = AddCard(pb, "TRANSPARENCY", 72 + b1.Height + 14);
            var sDim = new SliderBar { Min = 0, Max = 90, Value = Backdrop.Dim };
            sDim.Changed += v => { Backdrop.Dim = v; Prefs.Set("dim", v); RefreshBackdrop(); };
            b2.AddRow("Image dimming", "Darkens the image so text stays readable.", sDim);
            var sPanel = new SliderBar { Min = 30, Max = 100, Value = Backdrop.PanelPct };
            sPanel.Changed += v => { Backdrop.PanelPct = v; Prefs.Set("panelpct", v); RefreshBackdrop(); };
            b2.AddRow("Panel opacity", "Lower it to see more of your image (needs a background).", sPanel);
            sPages[4] = pb;

            // ---- ABOUT
            var ps = MakeSettingsPage("About", "Information about Niox.");
            var s1 = AddCard(ps, "NIOX", 72);
            s1.AddRow("Version", "Niox Editor", new Lbl { Text = AppInfo.Version, Font = Theme.UI(10, FontStyle.Bold), Role = 3 });
            s1.AddRow("Language", "Code editor with Lua syntax colors.", new Lbl { Text = "Lua", Font = Theme.UI(10, FontStyle.Bold), Role = 3 });
            s1.AddRow("Shortcuts", "Ctrl+S save · Ctrl+N new tab · Ctrl+O open · Ctrl+W close · F5 execute.", null);
            sPages[5] = ps;

            pageSettings.Resize += (s, ev) =>
            {
                int W = pageSettings.Width, H = pageSettings.Height;
                settingsMenu.SetBounds(0, 0, 224, H);
                foreach (var p in sPages) p.SetBounds(236, 0, Math.Max(100, W - 236), H);
            };
        }

        void ShowSettingsPage(int i)
        {
            if (sPages == null) return;
            sMenu.Selected = i;
            for (int k = 0; k < sPages.Length; k++) sPages[k].Visible = k == i;
        }

        // ---------------------------------------------------- background / theme
        void ChooseBackground()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose a background image";
                dlg.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (Backdrop.Load(dlg.FileName))
                {
                    Prefs.Set("bg", dlg.FileName);
                    RefreshBackdrop();
                    Notify(0, "Background updated", Path.GetFileName(dlg.FileName));
                }
                else Notify(1, "Could not load image", "Try a different file.");
            }
        }

        void RefreshBackdrop()
        {
            Backdrop.ResetCache();
            InvalidateTree(this);
            SaveSoon();
        }

        void ApplyTheme()
        {
            foreach (var f in files) ApplyEditor(f);
            search.ForeColor = Theme.Text;
            InvalidateTree(this);
        }

        void InvalidateTree(Control c)
        {
            c.Invalidate();
            foreach (Control k in c.Controls) InvalidateTree(k);
        }

        void OnShortcut(object s, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S) { SaveFile(current); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.N) { NewTab(); ShowPage(1); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.O) { OpenDialog(); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.W) { if (current != null) RequestClose(current); e.SuppressKeyPress = true; e.Handled = true; }
            else if (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.Enter)) { ExecuteScript(); e.SuppressKeyPress = true; e.Handled = true; }
        }
    }
}
