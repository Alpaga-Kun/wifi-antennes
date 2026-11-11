// Wi-Fi Antennes - aide au placement des antennes Wi-Fi
// Lit les bornes via l'API Wi-Fi native de Windows (wlanapi.dll) toutes les ~2,5 s.
// Compilation : voir build.ps1 (csc .NET Framework 4.x, aucune dependance).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Wi-Fi Antennes")]
[assembly: System.Reflection.AssemblyProduct("Wi-Fi Antennes")]
[assembly: System.Reflection.AssemblyDescription("Aide au placement des antennes Wi-Fi")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]
// Securite : les DLL natives (wlanapi.dll) ne sont chargees que depuis System32,
// jamais depuis le dossier de l'exe ou le dossier courant (anti DLL hijacking).
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace WifiAntennes
{
    // ------------------------------------------------------------------ Donnees
    class Bss
    {
        public string Ssid, Bssid;
        public int Rssi, FreqMhz;
        public string Band { get { return FreqMhz < 3000 ? "2,4 GHz" : (FreqMhz < 5925 ? "5 GHz" : "6 GHz"); } }
        public int Channel
        {
            get
            {
                if (FreqMhz < 3000) return (FreqMhz - 2407) / 5;
                if (FreqMhz < 5925) return (FreqMhz - 5000) / 5;
                return (FreqMhz - 5950) / 5;
            }
        }
    }

    class ScanResult
    {
        public List<Bss> Networks = new List<Bss>();
        public string ConnectedSsid, ConnectedBssid, Error;
        public DateTime Time = DateTime.Now;
    }

    class Mark
    {
        public DateTime Time; public double Val; public string Desc;
    }

    // ------------------------------------------------------------------ API Wi-Fi native
    static class Wlan
    {
        [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint ver, IntPtr res, out uint negVer, out IntPtr handle);
        [DllImport("wlanapi.dll")] static extern int WlanCloseHandle(IntPtr h, IntPtr res);
        [DllImport("wlanapi.dll")] static extern int WlanEnumInterfaces(IntPtr h, IntPtr res, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanScan(IntPtr h, ref Guid g, IntPtr ssid, IntPtr ie, IntPtr res);
        [DllImport("wlanapi.dll")] static extern int WlanGetNetworkBssList(IntPtr h, ref Guid g, IntPtr ssid, int bssType, bool secEnabled, IntPtr res, out IntPtr list);
        [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr h, ref Guid g, int opCode, IntPtr res, out int size, out IntPtr data, IntPtr opType);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

        static IntPtr handle = IntPtr.Zero;
        static Guid iface;
        static bool ready;
        public static string AdapterName = "";

        public static string Open()
        {
            uint v;
            int r = WlanOpenHandle(2, IntPtr.Zero, out v, out handle);
            if (r != 0) return "Impossible d'ouvrir le service Wi-Fi (code " + r + ").";
            IntPtr list;
            r = WlanEnumInterfaces(handle, IntPtr.Zero, out list);
            if (r != 0) return "Impossible de lister les cartes Wi-Fi (code " + r + ").";
            try
            {
                if (Marshal.ReadInt32(list, 0) == 0) return "Aucune carte Wi-Fi trouvée.";
                IntPtr p = new IntPtr(list.ToInt64() + 8);
                byte[] g = new byte[16];
                Marshal.Copy(p, g, 0, 16);
                iface = new Guid(g);
                AdapterName = Marshal.PtrToStringUni(new IntPtr(p.ToInt64() + 16));
                ready = true;
            }
            finally { WlanFreeMemory(list); }
            return null;
        }

        public static void Close() { if (handle != IntPtr.Zero) WlanCloseHandle(handle, IntPtr.Zero); }

        public static void TriggerScan() { if (ready) WlanScan(handle, ref iface, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); }

        static string ReadSsid(long addr)
        {
            int len = Marshal.ReadInt32(new IntPtr(addr));
            if (len < 0 || len > 32) len = 0;
            byte[] s = new byte[len];
            if (len > 0) Marshal.Copy(new IntPtr(addr + 4), s, 0, len);
            return Encoding.UTF8.GetString(s);
        }

        static string ReadMac(long addr)
        {
            byte[] mac = new byte[6];
            Marshal.Copy(new IntPtr(addr), mac, 0, 6);
            return BitConverter.ToString(mac).Replace('-', ':').ToLowerInvariant();
        }

        public static ScanResult Read()
        {
            var res = new ScanResult();
            if (!ready) { res.Error = "Carte Wi-Fi indisponible."; return res; }
            IntPtr list;
            int r = WlanGetNetworkBssList(handle, ref iface, IntPtr.Zero, 3, false, IntPtr.Zero, out list);
            if (r != 0)
            {
                res.Error = r == 5
                    ? "Accès refusé : autorise la localisation pour les applications de bureau (Paramètres > Confidentialité > Localisation)."
                    : "Lecture des bornes impossible (code " + r + ").";
                return res;
            }
            try
            {
                // Securite : on ne lit jamais au-dela de la taille annoncee par Windows
                int total = Marshal.ReadInt32(list, 0);
                int n = Marshal.ReadInt32(list, 4);
                if (n < 0 || 8 + (long)n * 360 > total) n = Math.Max(0, (total - 8) / 360);
                for (int i = 0; i < n; i++)
                {
                    long b = list.ToInt64() + 8 + (long)i * 360;   // sizeof(WLAN_BSS_ENTRY) = 360
                    var x = new Bss();
                    x.Ssid = ReadSsid(b);
                    x.Bssid = ReadMac(b + 40);
                    x.Rssi = Marshal.ReadInt32(new IntPtr(b + 56));
                    x.FreqMhz = Marshal.ReadInt32(new IntPtr(b + 92)) / 1000;
                    res.Networks.Add(x);
                }
            }
            finally { WlanFreeMemory(list); }

            int size; IntPtr data;
            if (WlanQueryInterface(handle, ref iface, 7, IntPtr.Zero, out size, out data, IntPtr.Zero) == 0)
            {
                try
                {
                    long a = data.ToInt64();
                    if (Marshal.ReadInt32(data) == 1)   // wlan_interface_state_connected
                    {
                        res.ConnectedSsid = ReadSsid(a + 520);
                        res.ConnectedBssid = ReadMac(a + 560);
                    }
                }
                finally { WlanFreeMemory(data); }
            }
            return res;
        }
    }

    // ------------------------------------------------------------------ Theme
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(20, 20, 24);
        public static readonly Color Card = Color.FromArgb(32, 32, 38);
        public static readonly Color Hover = Color.FromArgb(48, 48, 56);
        public static readonly Color Border = Color.FromArgb(52, 52, 60);
        public static readonly Color Track = Color.FromArgb(52, 52, 60);
        public static readonly Color Fg = Color.FromArgb(236, 236, 241);
        public static readonly Color Muted = Color.FromArgb(150, 150, 162);
        public static readonly Color Accent = Color.FromArgb(88, 166, 255);
        public static readonly Color Good = Color.FromArgb(46, 204, 113);
        public static readonly Color Bad = Color.FromArgb(231, 76, 60);
        public const double Min = -95, Max = -45;
        public static string IconFontName = "Segoe MDL2 Assets";

        public const string GWifi = "", GStar = "", GPin = "", GEdit = "",
            GRefresh = "", GVolume = "", GPinTop = "", GSave = "",
            GUp = "", GDown = "", GFlat = "",
            GTest = "", GPlay = "", GSkip = "", GAdd = "", GCheck = "", GCancel = "";

        public static void Init()
        {
            using (var fc = new InstalledFontCollection())
                foreach (var f in fc.Families)
                    if (f.Name == "Segoe Fluent Icons") { IconFontName = f.Name; break; }
        }

        public static Font Icon(float size) { return new Font(IconFontName, size, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font Ui(float size, FontStyle st = FontStyle.Regular) { return new Font("Segoe UI", size, st, GraphicsUnit.Point); }

        public static Color LevelColor(double d)
        {
            if (d >= -60) return Color.FromArgb(46, 204, 113);
            if (d >= -67) return Color.FromArgb(163, 217, 72);
            if (d >= -72) return Color.FromArgb(241, 196, 15);
            if (d >= -78) return Color.FromArgb(230, 126, 34);
            return Color.FromArgb(231, 76, 60);
        }

        public static string LevelName(double d)
        {
            if (d >= -60) return "Excellent";
            if (d >= -67) return "Bon";
            if (d >= -72) return "Moyen";
            if (d >= -78) return "Faible";
            return "Très faible";
        }

        public static string Fmt(double d) { return d.ToString("0.#"); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ------------------------------------------------------------------ Controles graphiques
    class Card : Panel
    {
        public string Title;
        public Card(string title)
        {
            Title = title;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            float k = g.DpiX / 96f;
            using (var path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 10 * k))
            using (var b = new SolidBrush(Theme.Card))
            using (var p = new Pen(Theme.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            if (!string.IsNullOrEmpty(Title))
                using (var f = Theme.Ui(8.5f, FontStyle.Bold))
                using (var tb = new SolidBrush(Theme.Muted))
                    g.DrawString(Title.ToUpperInvariant(), f, tb, 12 * k, 8 * k);
        }
    }

    class Canvas : Control
    {
        public Canvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }
    }

    class Dial : Canvas
    {
        public double? Value, Best, Ref;

        static float Ang(double d)
        {
            double t = (d - Theme.Min) / (Theme.Max - Theme.Min);
            if (t < 0) t = 0; if (t > 1) t = 1;
            return (float)(150 + 240 * t);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(BackColor);
            float k = g.DpiX / 96f;
            float D = Math.Min(Width - 30 * k, (Height - 12 * k) * 1.33f);
            float cx = Width / 2f, top = 6 * k;
            var outer = new RectangleF(cx - D / 2, top, D, D);
            float tw = 16 * k;

            // zones de couleur (anneau fin exterieur)
            double[] z = { -95, -78, -72, -67, -60, -45 };
            var zr = RectangleF.Inflate(outer, -3 * k, -3 * k);
            for (int i = 0; i < 5; i++)
            {
                float a0 = Ang(z[i]), a1 = Ang(z[i + 1]);
                using (var p = new Pen(Theme.LevelColor(z[i] + 0.1), 3 * k))
                    g.DrawArc(p, zr, a0 + 0.8f, a1 - a0 - 1.6f);
            }

            // piste + valeur
            var track = RectangleF.Inflate(outer, -(12 * k + tw / 2), -(12 * k + tw / 2));
            using (var p = new Pen(Theme.Track, tw))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawArc(p, track, 150, 240);
            }
            if (Value.HasValue)
            {
                float sweep = Ang(Value.Value) - 150;
                if (sweep < 1) sweep = 1;
                using (var p = new Pen(Theme.LevelColor(Value.Value), tw))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, track, 150, sweep);
                }
            }

            float tcx = track.X + track.Width / 2, tcy = track.Y + track.Height / 2, tr = track.Width / 2;
            Action<double, Color> tick = (d, col) =>
            {
                double a = Ang(d) * Math.PI / 180;
                float r1 = tr - tw / 2 - 3 * k, r2 = tr + tw / 2 + 3 * k;
                using (var p = new Pen(col, 3 * k))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, tcx + r1 * (float)Math.Cos(a), tcy + r1 * (float)Math.Sin(a),
                                  tcx + r2 * (float)Math.Cos(a), tcy + r2 * (float)Math.Sin(a));
                }
            };
            if (Ref.HasValue) tick(Ref.Value, Theme.Accent);
            if (Best.HasValue) tick(Best.Value, Color.White);

            // bornes de l'echelle
            using (var f = Theme.Ui(7.5f))
            using (var b = new SolidBrush(Theme.Muted))
            {
                double a0 = 150 * Math.PI / 180, a1 = 30 * Math.PI / 180;
                float rr = tr + 4 * k;
                g.DrawString("-95", f, b, tcx + rr * (float)Math.Cos(a0) - 8 * k, tcy + rr * (float)Math.Sin(a0) - 6 * k);
                g.DrawString("-45", f, b, tcx + rr * (float)Math.Cos(a1) - 12 * k, tcy + rr * (float)Math.Sin(a1) - 6 * k);
            }

            // textes centraux
            string v = Value.HasValue ? Theme.Fmt(Value.Value) : "—";
            Color vc = Value.HasValue ? Theme.LevelColor(Value.Value) : Theme.Muted;
            using (var f = Theme.Ui(30f, FontStyle.Bold))
            using (var b = new SolidBrush(vc))
            {
                var sz = g.MeasureString(v, f);
                g.DrawString(v, f, b, tcx - sz.Width / 2, tcy - sz.Height * 0.72f);
            }
            using (var f = Theme.Ui(9.5f))
            using (var b = new SolidBrush(Theme.Muted))
            {
                var sz = g.MeasureString("dBm", f);
                g.DrawString("dBm", f, b, tcx - sz.Width / 2, tcy + 6 * k);
            }
            string lvl = Value.HasValue ? Theme.LevelName(Value.Value) : "Recherche…";
            using (var f = Theme.Ui(13f, FontStyle.Bold))
            using (var b = new SolidBrush(vc))
            {
                var sz = g.MeasureString(lvl, f);
                g.DrawString(lvl, f, b, tcx - sz.Width / 2, tcy + 26 * k);
            }
        }
    }

    class Graph : Canvas
    {
        public List<double> Data = new List<double>();
        public double? Ref;
        public const int MaxPts = 72;   // ~3 min

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            float k = g.DpiX / 96f;
            double lo = -90, hi = -50;
            float L = 30 * k, R = Width - 6 * k, T = 6 * k, B = Height - 6 * k;
            Func<double, float> Y = d =>
            {
                double c = Math.Max(lo, Math.Min(hi, d));
                return (float)(B - (B - T) * (c - lo) / (hi - lo));
            };

            double[] z = { -90, -78, -72, -67, -60, -50 };
            for (int i = 0; i < 5; i++)
                using (var b = new SolidBrush(Color.FromArgb(20, Theme.LevelColor(z[i] + 0.1))))
                    g.FillRectangle(b, L, Y(z[i + 1]), R - L, Y(z[i]) - Y(z[i + 1]));

            using (var f = Theme.Ui(7.5f))
            using (var b = new SolidBrush(Theme.Muted))
                foreach (double t in new double[] { -78, -72, -67, -60 })
                {
                    float y = Y(t);
                    using (var p = new Pen(Color.FromArgb(90, Theme.LevelColor(t + 0.1))))
                    {
                        p.DashStyle = DashStyle.Dash;
                        g.DrawLine(p, L, y, R, y);
                    }
                    g.DrawString(t.ToString(), f, b, 0, y - 7 * k);
                }

            if (Ref.HasValue)
                using (var p = new Pen(Theme.Accent, 1.5f * k))
                {
                    p.DashStyle = DashStyle.Dot;
                    g.DrawLine(p, L, Y(Ref.Value), R, Y(Ref.Value));
                }

            if (Data.Count >= 2)
            {
                int start = Math.Max(0, Data.Count - MaxPts);
                float step = (R - L) / (MaxPts - 1);
                var pts = new List<PointF>();
                for (int i = start; i < Data.Count; i++) pts.Add(new PointF(L + (i - start) * step, Y(Data[i])));
                var area = new List<PointF>(pts);
                area.Add(new PointF(pts[pts.Count - 1].X, B));
                area.Add(new PointF(pts[0].X, B));
                using (var b = new SolidBrush(Color.FromArgb(26, 255, 255, 255))) g.FillPolygon(b, area.ToArray());
                using (var p = new Pen(Color.White, 2 * k)) { p.LineJoin = LineJoin.Round; g.DrawLines(p, pts.ToArray()); }
                var last = pts[pts.Count - 1];
                using (var b = new SolidBrush(Theme.LevelColor(Data[Data.Count - 1])))
                    g.FillEllipse(b, last.X - 5 * k, last.Y - 5 * k, 10 * k, 10 * k);
            }
            else
            {
                using (var f = Theme.Ui(10f))
                using (var b = new SolidBrush(Theme.Muted))
                {
                    string s = "En attente des premières mesures…";
                    var sz = g.MeasureString(s, f);
                    g.DrawString(s, f, b, (Width - sz.Width) / 2, (Height - sz.Height) / 2);
                }
            }
        }
    }

    class ApList : Canvas
    {
        public List<Bss> Items = new List<Bss>();
        public string ConnectedBssid;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            float k = g.DpiX / 96f;
            float rowH = 23 * k;
            if (Items.Count == 0)
            {
                using (var f = Theme.Ui(9.5f)) using (var b = new SolidBrush(Theme.Muted))
                    g.DrawString("Aucune borne captée pour ce réseau.", f, b, 4 * k, 4 * k);
                return;
            }
            using (var f = Theme.Ui(9.5f))
            using (var fb = Theme.Ui(9.5f, FontStyle.Bold))
            using (var fs = Theme.Ui(8f))
            using (var fg = new SolidBrush(Theme.Fg))
            using (var mu = new SolidBrush(Theme.Muted))
            using (var ac = new SolidBrush(Theme.Accent))
            {
                int n = Math.Min(Items.Count, (int)(Height / rowH));
                for (int i = 0; i < n; i++)
                {
                    var x = Items[i];
                    float y = i * rowH;
                    bool conn = ConnectedBssid != null && x.Bssid == ConnectedBssid;
                    Color c = Theme.LevelColor(x.Rssi);
                    if (conn)
                        using (var hb = new SolidBrush(Color.FromArgb(34, Theme.Accent)))
                        using (var path = Theme.Round(new RectangleF(0, y + 1 * k, Width - 1, rowH - 2 * k), 5 * k))
                            g.FillPath(hb, path);
                    using (var db = new SolidBrush(c)) g.FillEllipse(db, 6 * k, y + rowH / 2 - 4 * k, 8 * k, 8 * k);
                    g.DrawString(x.Band + " · canal " + x.Channel, f, fg, 20 * k, y + 3 * k);
                    if (conn) g.DrawString("connecté", fs, ac, 142 * k, y + 5 * k);
                    else g.DrawString("…" + x.Bssid.Substring(9), fs, mu, 142 * k, y + 5 * k);
                    float bx = 206 * k, bw = 56 * k, bh = 6 * k, by = y + rowH / 2 - bh / 2;
                    using (var tb = new SolidBrush(Theme.Track)) g.FillRectangle(tb, bx, by, bw, bh);
                    double t = Math.Max(0, Math.Min(1, (x.Rssi - Theme.Min) / (Theme.Max - Theme.Min)));
                    using (var cb = new SolidBrush(c)) g.FillRectangle(cb, bx, by, (float)(bw * t), bh);
                    string s = x.Rssi + " dBm";
                    var sz = g.MeasureString(s, fb);
                    using (var cb = new SolidBrush(c)) g.DrawString(s, fb, cb, Width - sz.Width - 2 * k, y + 3 * k);
                }
            }
        }
    }

    class IconButton : Control
    {
        public string Glyph;
        public bool Toggle, Checked, Primary;
        bool hover;
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        public IconButton(string glyph, string text)
        {
            Glyph = glyph; Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.Ui(9.5f);
            BackColor = Theme.Bg;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { if (Toggle) Checked = !Checked; Invalidate(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            float k = g.DpiX / 96f;
            bool on = Toggle && Checked;
            Color bg = Primary ? (hover ? Color.FromArgb(118, 186, 255) : Theme.Accent)
                               : (on ? Color.FromArgb(58, Theme.Accent) : (hover ? Theme.Hover : Theme.Card));
            Color bd = (on || Primary) ? Theme.Accent : Theme.Border;
            if (!Enabled) { bg = Theme.Card; bd = Theme.Border; }
            using (var path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 7 * k))
            using (var b = new SolidBrush(bg))
            using (var p = new Pen(bd))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            Color fgc = (on || Primary) ? Color.White : (Toggle ? Theme.Muted : Theme.Fg);
            if (!Enabled) fgc = Color.FromArgb(95, 95, 105);
            using (var fi = Theme.Icon(11f))
            using (var b = new SolidBrush((on && !Primary && Enabled) ? Theme.Accent : fgc))
            {
                var sz = g.MeasureString(Glyph, fi);
                g.DrawString(Glyph, fi, b, 10 * k, (Height - sz.Height) / 2 + 1 * k);
            }
            using (var b = new SolidBrush(fgc))
            {
                var sz = g.MeasureString(Text, Font);
                g.DrawString(Text, Font, b, 32 * k, (Height - sz.Height) / 2);
            }
        }
    }

    // ------------------------------------------------------------------ Boite de saisie
    static class Prompt
    {
        static Point SP(float k, int x, int y) { return new Point((int)Math.Round(x * k), (int)Math.Round(y * k)); }
        static Size SZ(float k, int w, int h) { return new Size((int)Math.Round(w * k), (int)Math.Round(h * k)); }
        public static string Ask(IWin32Window owner, string title, string message, string def)
        {
            using (var f = new Form())
            {
                float k; using (var g0 = f.CreateGraphics()) k = g0.DpiX / 96f;
                f.AutoScaleMode = AutoScaleMode.None;
                f.Text = title; f.BackColor = Theme.Bg; f.ForeColor = Theme.Fg; f.Font = Theme.Ui(10f);
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false;
                f.StartPosition = FormStartPosition.CenterParent; f.ShowInTaskbar = false; f.TopMost = true;
                f.ClientSize = SZ(k, 420, 136);
                var l = new Label { Text = message, Location = SP(k, 16, 14), Size = SZ(k, 390, 22) };
                var tb = new TextBox { Text = def, Location = SP(k, 18, 42), Size = SZ(k, 384, 26), BackColor = Theme.Card, ForeColor = Theme.Fg, BorderStyle = BorderStyle.FixedSingle };
                var ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, Location = SP(k, 206, 88), Size = SZ(k, 98, 32), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
                var ca = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Location = SP(k, 310, 88), Size = SZ(k, 92, 32), FlatStyle = FlatStyle.Flat, BackColor = Theme.Card, ForeColor = Theme.Fg };
                ok.FlatAppearance.BorderSize = 0; ca.FlatAppearance.BorderColor = Theme.Border;
                f.Controls.AddRange(new Control[] { l, tb, ok, ca });
                tb.MaxLength = 200;
                f.AcceptButton = ok; f.CancelButton = ca;
                f.Shown += (s, e) => { tb.SelectAll(); tb.Focus(); };
                return f.ShowDialog(owner) == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }
    }

    // ------------------------------------------------------------------ Fenetre principale
    class MainForm : Form
    {
        readonly string preferredSsid;
        float k = 1f;
        int S(int v) { return (int)Math.Round(v * k); }
        Point P(int x, int y) { return new Point(S(x), S(y)); }
        Size Z(int w, int h) { return new Size(S(w), S(h)); }

        // Le dernier reseau choisi est memorise dans %APPDATA%\WifiAntennes\reseau.txt
        static string SettingsFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WifiAntennes", "reseau.txt"); }
        }
        static string LoadSsid()
        {
            try { return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile, Encoding.UTF8).Trim() : null; }
            catch { return null; }
        }
        static void SaveSsid(string ssid)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllText(SettingsFile, ssid ?? "", Encoding.UTF8);
            }
            catch { }
        }

        Dial dial; Graph graph; ApList aps; Card cardAps;
        Label lblTrendIcon, lblTrend, lblBest, lblRef, lblStatus, lblMarksEmpty;
        ComboBox cbSsid; ListBox lbMarks;
        IconButton btnRef, btnMark, btnExport, btnReset, btnSound, btnTop, btnTest, btnTips;
        OrientationTest testForm;
        TipsForm tipsForm;

        // Diffuse chaque scan aux fenetres annexes (test d'orientation)
        public event Action<ScanResult> Scanned;
        public string SelectedSsid { get { return selectedSsid; } }

        readonly List<double> raw = new List<double>();
        readonly List<Mark> marks = new List<Mark>();
        double? current, best, refv;
        string selectedSsid, ssidSignature;
        bool updatingCombo;
        volatile bool running;
        Thread worker;

        public MainForm(string ssid)
        {
            preferredSsid = string.IsNullOrEmpty(ssid) ? LoadSsid() : ssid;
            Theme.Init();
            AutoScaleMode = AutoScaleMode.None;
            using (var g0 = CreateGraphics()) k = g0.DpiX / 96f;
            Text = "Wi-Fi Antennes";
            BackColor = Theme.Bg; ForeColor = Theme.Fg; Font = Theme.Ui(9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Z(940, 552);
            TopMost = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SuspendLayout();
            BuildUi();
            ResumeLayout(false);
            PerformLayout();
        }

        Label L(Control parent, string text, int x, int y, int w, int h, Font f, Color c)
        {
            var l = new Label { Text = text, Location = P(x, y), Size = Z(w, h), Font = f, ForeColor = c, BackColor = parent.BackColor };
            parent.Controls.Add(l);
            return l;
        }

        void BuildUi()
        {
            // En-tete
            L(this, Theme.GWifi, 18, 12, 40, 36, Theme.Icon(20f), Theme.Accent);
            L(this, "Wi-Fi Antennes", 56, 6, 236, 30, Theme.Ui(15f, FontStyle.Bold), Theme.Fg);
            L(this, "Bouge doucement, ~5 s par position", 58, 34, 240, 20, Theme.Ui(9f), Theme.Muted);
            L(this, "Réseau", 300, 21, 54, 22, Theme.Ui(9.5f), Theme.Muted);
            cbSsid = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Card, ForeColor = Theme.Fg, Font = Theme.Ui(10f),
                Location = P(356, 17), Size = Z(250, 28)
            };
            btnTips = new IconButton("", "Conseils") { Location = P(616, 14), Size = Z(126, 34) };
            btnTips.Click += (s, e) => OpenTips();
            Controls.Add(btnTips);
            btnTest = new IconButton(Theme.GTest, "Test d'orientation") { Location = P(752, 14), Size = Z(168, 34), Primary = true };
            btnTest.Click += (s, e) => OpenTest();
            Controls.Add(btnTest);
            cbSsid.SelectedIndexChanged += (s, e) =>
            {
                if (updatingCombo) return;
                selectedSsid = cbSsid.SelectedItem as string;
                SaveSsid(selectedSsid);
                ResetStats();
                if (testForm != null && !testForm.IsDisposed) testForm.ResetAll("Réseau changé : mesures du test effacées.");
            };
            Controls.Add(cbSsid);

            // Carte signal
            var cSig = new Card("Signal") { Location = P(20, 56), Size = Z(360, 270) };
            Controls.Add(cSig);
            dial = new Dial { Location = P(10, 26), Size = Z(340, 178) };
            cSig.Controls.Add(dial);
            lblTrendIcon = L(cSig, Theme.GFlat, 16, 206, 26, 26, Theme.Icon(13f), Theme.Muted);
            lblTrend = L(cSig, "Comparaison avec la référence…", 44, 207, 306, 24, Theme.Ui(10.5f, FontStyle.Bold), Theme.Muted);
            L(cSig, Theme.GStar, 16, 240, 22, 22, Theme.Icon(11f), Color.White);
            lblBest = L(cSig, "Record : —", 38, 239, 140, 22, Theme.Ui(10f, FontStyle.Bold), Theme.Fg);
            L(cSig, Theme.GPin, 186, 240, 22, 22, Theme.Icon(11f), Theme.Accent);
            lblRef = L(cSig, "Référence : —", 208, 239, 145, 22, Theme.Ui(10f), Theme.Muted);

            // Carte historique
            var cHist = new Card("Historique (~3 min)") { Location = P(400, 56), Size = Z(520, 270) };
            Controls.Add(cHist);
            graph = new Graph { Location = P(10, 28), Size = Z(500, 232) };
            cHist.Controls.Add(graph);

            // Carte bornes
            cardAps = new Card("Bornes captées") { Location = P(20, 334), Size = Z(360, 150) };
            Controls.Add(cardAps);
            aps = new ApList { Location = P(10, 30), Size = Z(340, 114) };
            cardAps.Controls.Add(aps);

            // Carte positions
            var cMarks = new Card("Positions notées") { Location = P(400, 334), Size = Z(520, 150) };
            Controls.Add(cMarks);
            lbMarks = new ListBox
            {
                Location = P(12, 30), Size = Z(496, 112), BorderStyle = BorderStyle.None,
                BackColor = Theme.Card, ForeColor = Theme.Fg, Font = Theme.Ui(9.5f),
                DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false
            };
            lbMarks.DrawItem += DrawMark;
            cMarks.Controls.Add(lbMarks);
            lblMarksEmpty = L(cMarks, "Place les antennes, attends quelques secondes, puis clique sur « Noter la position ».", 14, 34, 490, 40, Theme.Ui(9.5f), Theme.Muted);
            lblMarksEmpty.BringToFront();

            // Barre d'outils
            btnRef = Btn(Theme.GPin, "Nouvelle référence", 20, 160);
            btnMark = Btn(Theme.GEdit, "Noter la position", 188, 156);
            btnExport = Btn(Theme.GSave, "Exporter (CSV)", 352, 136);
            btnReset = Btn(Theme.GRefresh, "Remettre à zéro", 496, 140);
            btnSound = Btn(Theme.GVolume, "Son record", 660, 124); btnSound.Toggle = true; btnSound.Checked = true;
            btnTop = Btn(Theme.GPinTop, "Premier plan", 792, 128); btnTop.Toggle = true; btnTop.Checked = true;

            btnRef.Click += (s, e) => { if (current.HasValue) { refv = current; Refresh_(); } };
            btnMark.Click += (s, e) => AddMark();
            btnExport.Click += (s, e) => Export();
            btnReset.Click += (s, e) => ResetStats();
            btnTop.Click += (s, e) =>
            {
                TopMost = btnTop.Checked;
                if (testForm != null && !testForm.IsDisposed) testForm.TopMost = TopMost;
                if (tipsForm != null && !tipsForm.IsDisposed) tipsForm.TopMost = TopMost;
            };

            lblStatus = L(this, "Démarrage…", 20, 530, 900, 20, Theme.Ui(8.5f), Theme.Muted);
        }

        IconButton Btn(string glyph, string text, int x, int w)
        {
            var b = new IconButton(glyph, text) { Location = P(x, 492), Size = Z(w, 32) };
            Controls.Add(b);
            return b;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            lbMarks.ItemHeight = (int)Math.Round(24 * DeviceDpi / 96.0);
            string err = Wlan.Open();
            if (err != null) { lblStatus.Text = err; lblStatus.ForeColor = Theme.Bad; return; }
            running = true;
            worker = new Thread(Loop) { IsBackground = true };
            worker.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            running = false;
            if (worker != null && worker.IsAlive) worker.Join(3000);   // pas de fermeture du handle pendant une lecture
            Wlan.Close();
            base.OnFormClosing(e);
        }

        void Loop()
        {
            bool first = true;
            while (running)
            {
                if (!first)
                {
                    Wlan.TriggerScan();
                    for (int i = 0; i < 25 && running; i++) Thread.Sleep(100);
                }
                first = false;
                if (!running) break;
                ScanResult r;
                try { r = Wlan.Read(); }
                catch (Exception ex) { r = new ScanResult { Error = "Erreur : " + ex.Message }; }
                try { BeginInvoke(new Action(() => OnScan(r))); } catch { break; }
            }
        }

        void UpdateSsids(ScanResult r)
        {
            var ssids = r.Networks.Where(n => n.Ssid.Length > 0).Select(n => n.Ssid).Distinct().ToList();
            if (selectedSsid == null)
            {
                if (!string.IsNullOrEmpty(preferredSsid) && ssids.Contains(preferredSsid)) selectedSsid = preferredSsid;
                else if (!string.IsNullOrEmpty(r.ConnectedSsid) && ssids.Contains(r.ConnectedSsid)) selectedSsid = r.ConnectedSsid;
                else if (ssids.Count > 0)
                    selectedSsid = r.Networks.Where(n => n.Ssid.Length > 0).OrderByDescending(n => n.Rssi).First().Ssid;
            }
            if (selectedSsid != null && !ssids.Contains(selectedSsid)) ssids.Add(selectedSsid);
            ssids.Sort(StringComparer.CurrentCultureIgnoreCase);
            string sig = string.Join("\n", ssids);
            if (sig == ssidSignature || cbSsid.DroppedDown) return;
            ssidSignature = sig;
            updatingCombo = true;
            cbSsid.BeginUpdate();
            cbSsid.Items.Clear();
            foreach (var s in ssids) cbSsid.Items.Add(s);
            if (selectedSsid != null) cbSsid.SelectedItem = selectedSsid;
            cbSsid.EndUpdate();
            updatingCombo = false;
        }

        void OnScan(ScanResult r)
        {
            if (IsDisposed) return;
            if (r.Error != null) { lblStatus.Text = r.Error; lblStatus.ForeColor = Theme.Bad; return; }
            lblStatus.ForeColor = Theme.Muted;
            UpdateSsids(r);
            var handler = Scanned;
            if (handler != null) handler(r);

            var nets = r.Networks.Where(n => n.Ssid == selectedSsid).OrderByDescending(n => n.Rssi).ToList();
            aps.Items = nets;
            aps.ConnectedBssid = r.ConnectedBssid;
            aps.Invalidate();
            cardAps.Title = "Bornes captées (" + nets.Count + ")";
            cardAps.Invalidate();

            string conn = string.IsNullOrEmpty(r.ConnectedSsid) ? "aucun réseau" : r.ConnectedSsid;
            lblStatus.Text = string.Format("Carte : {0}    •    Connecté à : {1}    •    Mis à jour à {2:HH:mm:ss}    •    valeur = moyenne des 2 derniers scans",
                Wlan.AdapterName, conn, r.Time);

            if (nets.Count == 0)
            {
                raw.Clear();
                current = null;
                lblTrendIcon.Text = Theme.GFlat; lblTrendIcon.ForeColor = Theme.Muted;
                lblTrend.Text = "Réseau « " + (selectedSsid ?? "?") + " » introuvable"; lblTrend.ForeColor = Theme.Muted;
                Refresh_();
                return;
            }

            raw.Add(nets[0].Rssi);
            while (raw.Count > 2) raw.RemoveAt(0);
            double cur = Math.Round(raw.Average(), 1);
            current = cur;
            graph.Data.Add(cur);
            if (graph.Data.Count > 1000) graph.Data.RemoveAt(0);
            if (!refv.HasValue) refv = cur;
            if (!best.HasValue || cur > best.Value)
            {
                bool record = best.HasValue && cur >= best.Value + 1;
                best = cur;
                if (record && btnSound.Checked) SystemSounds.Asterisk.Play();
            }
            Refresh_();
        }

        void Refresh_()
        {
            dial.Value = current; dial.Best = best; dial.Ref = refv;
            graph.Ref = refv;
            lblBest.Text = "Record : " + (best.HasValue ? Theme.Fmt(best.Value) + " dBm" : "—");
            lblRef.Text = "Référence : " + (refv.HasValue ? Theme.Fmt(refv.Value) + " dBm" : "—");
            if (current.HasValue && refv.HasValue)
            {
                double diff = Math.Round(current.Value - refv.Value, 1);
                if (diff >= 3)
                {
                    lblTrendIcon.Text = Theme.GUp; lblTrendIcon.ForeColor = Theme.Good;
                    lblTrend.Text = "+" + Theme.Fmt(diff) + " dB : mieux qu'au départ"; lblTrend.ForeColor = Theme.Good;
                }
                else if (diff <= -3)
                {
                    lblTrendIcon.Text = Theme.GDown; lblTrendIcon.ForeColor = Theme.Bad;
                    lblTrend.Text = Theme.Fmt(diff) + " dB : moins bien qu'au départ"; lblTrend.ForeColor = Theme.Bad;
                }
                else
                {
                    lblTrendIcon.Text = Theme.GFlat; lblTrendIcon.ForeColor = Theme.Muted;
                    lblTrend.Text = "Stable (" + (diff > 0 ? "+" : "") + Theme.Fmt(diff) + " dB)"; lblTrend.ForeColor = Theme.Muted;
                }
            }
            dial.Invalidate(); graph.Invalidate();
        }

        void ResetStats()
        {
            raw.Clear(); graph.Data.Clear();
            current = null; best = null; refv = null;
            lblTrendIcon.Text = Theme.GFlat; lblTrendIcon.ForeColor = Theme.Muted;
            lblTrend.Text = "Nouvelle série de mesures…"; lblTrend.ForeColor = Theme.Muted;
            Refresh_();
        }

        void AddMark()
        {
            if (!current.HasValue)
            {
                MessageBox.Show(this, "Pas encore de mesure : attends quelques secondes.", "Wi-Fi Antennes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            double val = current.Value;
            string desc = Prompt.Ask(this, "Noter la position",
                "Décris la position (ex : antennes verticales, en haut du bureau) :", "Position " + (marks.Count + 1));
            if (desc == null) return;
            if (desc.Length == 0) desc = "Position " + (marks.Count + 1);
            var m = new Mark { Time = DateTime.Now, Val = val, Desc = desc };
            marks.Add(m);
            lbMarks.Items.Add(m);
            lbMarks.TopIndex = Math.Max(0, lbMarks.Items.Count - 1);
            lblMarksEmpty.Visible = false;
            lbMarks.Invalidate();
        }

        void DrawMark(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float k = g.DpiX / 96f;
            var m = (Mark)lbMarks.Items[e.Index];
            bool isBest = marks.Count > 0 && m == marks.OrderByDescending(x => x.Val).First();
            using (var bg = new SolidBrush(isBest ? Color.FromArgb(30, Theme.Good) : Theme.Card)) g.FillRectangle(bg, e.Bounds);
            Color c = Theme.LevelColor(m.Val);
            float cy = e.Bounds.Y + e.Bounds.Height / 2f;
            using (var b = new SolidBrush(c)) g.FillEllipse(b, e.Bounds.X + 4 * k, cy - 4 * k, 8 * k, 8 * k);
            using (var f = Theme.Ui(9.5f, FontStyle.Bold)) using (var b = new SolidBrush(c))
                g.DrawString(Theme.Fmt(m.Val) + " dBm", f, b, e.Bounds.X + 18 * k, e.Bounds.Y + 3 * k);
            using (var f = Theme.Ui(9.5f)) using (var b = new SolidBrush(Theme.Fg))
                g.DrawString(m.Desc, f, b, e.Bounds.X + 100 * k, e.Bounds.Y + 3 * k);
            using (var f = Theme.Ui(8f)) using (var b = new SolidBrush(Theme.Muted))
            {
                string t = m.Time.ToString("HH:mm") + "  ·  " + Theme.LevelName(m.Val);
                var sz = g.MeasureString(t, f);
                g.DrawString(t, f, b, e.Bounds.Right - sz.Width - (isBest ? 24 * k : 4 * k), e.Bounds.Y + 5 * k);
            }
            if (isBest)
                using (var f = Theme.Icon(10f)) using (var b = new SolidBrush(Theme.Good))
                    g.DrawString(Theme.GStar, f, b, e.Bounds.Right - 20 * k, e.Bounds.Y + 4 * k);
        }

        // Securite : un nom de reseau Wi-Fi est choisi par n'importe qui a portee.
        // Neutralise l'injection de formules a l'ouverture dans Excel (=, +, -, @) et echappe guillemets / retours a la ligne.
        internal static string CsvText(string s)
        {
            s = s ?? "";
            if (s.Length > 0 && "=+-@\t\r".IndexOf(s[0]) >= 0) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        internal void OpenTips()
        {
            if (tipsForm == null || tipsForm.IsDisposed) tipsForm = new TipsForm(this);
            tipsForm.TopMost = TopMost;
            if (!tipsForm.Visible) tipsForm.Show(this);
            tipsForm.BringToFront();
            tipsForm.Activate();
        }

        internal void OpenTest()
        {
            if (testForm == null || testForm.IsDisposed) testForm = new OrientationTest(this);
            testForm.TopMost = TopMost;   // sinon la fenetre « premier plan » principale la recouvre
            if (!testForm.Visible) testForm.Show(this);
            testForm.BringToFront();
            testForm.Activate();
        }

        void Export()
        {
            if (marks.Count == 0)
            {
                MessageBox.Show(this, "Aucune position notée pour l'instant.", "Wi-Fi Antennes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var d = new SaveFileDialog())
            {
                d.Filter = "Fichier CSV (*.csv)|*.csv";
                d.FileName = "positions-antennes-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("Heure;Réseau;Signal (dBm);Niveau;Description");
                foreach (var m in marks)
                    sb.AppendLine(string.Join(";", new[] { m.Time.ToString("yyyy-MM-dd HH:mm:ss"), CsvText(selectedSsid), Theme.Fmt(m.Val), CsvText(Theme.LevelName(m.Val)), CsvText(m.Desc) }));
                File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
            }
        }
    }

    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetDefaultDllDirectories(uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        [STAThread]
        static void Main(string[] args)
        {
            // Securite : toute DLL chargee ensuite ne peut venir que de System32 (LOAD_LIBRARY_SEARCH_SYSTEM32)
            try { SetDefaultDllDirectories(0x00000800); } catch { }

            // Une seule instance : si le programme tourne deja, on ramene sa fenetre au premier plan
            bool first;
            using (var mutex = new Mutex(true, "Local\\WifiAntennes.SingleInstance", out first))
            {
                if (!first)
                {
                    var me = System.Diagnostics.Process.GetCurrentProcess();
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(me.ProcessName))
                        if (p.Id != me.Id && p.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(p.MainWindowHandle, 9);   // SW_RESTORE
                            SetForegroundWindow(p.MainWindowHandle);
                            break;
                        }
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
            }
        }
    }
}
