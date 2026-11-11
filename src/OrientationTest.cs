// Wi-Fi Antennes - test d'orientation guide
// Fait passer les antennes par plusieurs orientations types, mesure chaque position sur
// plusieurs scans, puis classe les positions et indique si l'ecart est significatif.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

namespace WifiAntennes
{
    class Trial
    {
        public string Name;
        public float A1, A2;      // angle de chaque antenne en degres : 0 = verticale, + = penchee vers la droite
        public bool Custom;       // position libre decrite par l'utilisateur (pas de dessin)
        public readonly List<int> Samples = new List<int>();
        public readonly List<int> Recheck = new List<int>();

        public static double Mean(List<int> l) { return l.Count == 0 ? double.NaN : l.Average(); }
        public static double Sd(List<int> l)
        {
            if (l.Count < 2) return 0;
            double m = l.Average();
            return Math.Sqrt(l.Sum(v => (v - m) * (v - m)) / (l.Count - 1));
        }
    }

    // ------------------------------------------------------------------ Dessin : dos du PC + antennes
    class AntennaView : Canvas
    {
        public Trial Trial;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            if (Trial == null) return;
            float k = g.DpiX / 96f;
            float cx = Width / 2f;

            using (var f = Theme.Ui(7.5f)) using (var b = new SolidBrush(Theme.Muted))
                g.DrawString("Vu de derrière le PC", f, b, 2 * k, 0);

            // tour
            var body = new RectangleF(cx - 62 * k, 16 * k, 124 * k, Height - 22 * k);
            using (var path = Theme.Round(body, 8 * k))
            using (var b = new SolidBrush(Color.FromArgb(40, 40, 48)))
            using (var p = new Pen(Color.FromArgb(78, 78, 90), 1.5f * k))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            using (var p = new Pen(Color.FromArgb(70, 70, 82), 1.5f * k))
            {
                float fr = 22 * k, fy = body.Y + 36 * k;
                g.DrawEllipse(p, cx - fr, fy - fr, 2 * fr, 2 * fr);
                g.DrawEllipse(p, cx - fr * 0.45f, fy - fr * 0.45f, fr * 0.9f, fr * 0.9f);
                for (int i = 0; i < 4; i++)
                {
                    float y = body.Y + 72 * k + i * 9 * k;
                    g.DrawLine(p, cx - 40 * k, y, cx + 40 * k, y);
                }
            }

            // equerre de la carte Wi-Fi
            float by = body.Bottom - 34 * k;
            using (var b = new SolidBrush(Color.FromArgb(64, 64, 76)))
                g.FillRectangle(b, cx - 54 * k, by - 9 * k, 108 * k, 18 * k);
            using (var f = Theme.Ui(7f)) using (var b = new SolidBrush(Theme.Muted))
            {
                var sz = g.MeasureString("carte Wi-Fi", f);
                g.DrawString("carte Wi-Fi", f, b, cx - sz.Width / 2, by + 11 * k);
            }

            if (Trial.Custom)
            {
                var r = new RectangleF(body.X - 30 * k, body.Y + body.Height * 0.35f, body.Width + 60 * k, 52 * k);
                using (var path = Theme.Round(r, 8 * k))
                using (var b = new SolidBrush(Color.FromArgb(230, 32, 32, 38)))
                using (var p = new Pen(Theme.Accent))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
                using (var f = Theme.Ui(9.5f, FontStyle.Bold)) using (var b = new SolidBrush(Theme.Fg))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString("Position personnalisée", f, b, r, sf);
                return;
            }

            float L = Math.Min(Height * 0.62f, 112 * k);
            float[] xs = { cx - 24 * k, cx + 24 * k };
            float[] angs = { Trial.A1, Trial.A2 };
            for (int i = 0; i < 2; i++)
            {
                double a = angs[i] * Math.PI / 180;
                float x0 = xs[i], y0 = by;
                float x1 = x0 + L * (float)Math.Sin(a), y1 = y0 - L * (float)Math.Cos(a);
                using (var p = new Pen(Color.FromArgb(18, 18, 22), 14 * k))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, x0, y0, x1, y1);
                }
                using (var p = new Pen(Color.FromArgb(225, 225, 232), 10 * k))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, x0, y0, x1, y1);
                }
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillEllipse(b, x0 - 6 * k, y0 - 6 * k, 12 * k, 12 * k);
                using (var f = Theme.Ui(8.5f, FontStyle.Bold)) using (var b = new SolidBrush(Theme.Accent))
                    g.DrawString((i + 1).ToString(), f, b, x1 + (angs[i] >= 0 ? 7 * k : -16 * k), y1 - 9 * k);
            }
        }
    }

    class ProgressBarX : Canvas
    {
        public int Value, Max = 6;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            float k = g.DpiX / 96f, r = Height / 2f;
            using (var path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), r))
            using (var b = new SolidBrush(Theme.Track)) g.FillPath(b, path);
            float w = Max <= 0 ? 0 : (Width - 1) * Math.Min(1f, Value / (float)Max);
            if (w >= Height)
                using (var path = Theme.Round(new RectangleF(0, 0, w, Height - 1), r))
                using (var b = new SolidBrush(Value >= Max ? Theme.Good : Theme.Accent)) g.FillPath(b, path);
        }
    }

    class Ranking : Canvas
    {
        public List<Trial> Trials;
        public int Needed;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            float k = g.DpiX / 96f;
            var done = Trials.Where(t => t.Samples.Count >= Needed).OrderByDescending(t => Trial.Mean(t.Samples)).ToList();
            using (var f = Theme.Ui(9f))
            using (var fb = Theme.Ui(9f, FontStyle.Bold))
            using (var fs = Theme.Ui(7.5f))
            using (var fg = new SolidBrush(Theme.Fg))
            using (var mu = new SolidBrush(Theme.Muted))
            using (var good = new SolidBrush(Theme.Good))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (done.Count == 0)
                {
                    g.DrawString("Aucune position mesurée pour l'instant.", f, mu, new RectangleF(0, 2 * k, Width, 40 * k));
                    return;
                }
                float rowH = 34 * k;
                int max = Math.Max(1, (int)(Height / rowH));
                for (int i = 0; i < Math.Min(done.Count, max); i++)
                {
                    var t = done[i];
                    double m = Trial.Mean(t.Samples), sd = Trial.Sd(t.Samples);
                    float y = i * rowH;
                    Color c = Theme.LevelColor(m);
                    string val = Theme.Fmt(m) + " dBm";
                    var vsz = g.MeasureString(val, fb);
                    using (var cb = new SolidBrush(c)) g.DrawString(val, fb, cb, Width - vsz.Width, y);
                    g.DrawString((i + 1) + ". " + t.Name, i == 0 ? fb : f, i == 0 ? good : fg,
                        new RectangleF(0, y, Width - vsz.Width - 4 * k, 18 * k), sf);
                    float bw = Width - 44 * k, bh = 6 * k, byy = y + 21 * k;
                    using (var tb = new SolidBrush(Theme.Track)) g.FillRectangle(tb, 0, byy, bw, bh);
                    double p = Math.Max(0, Math.Min(1, (m + 90) / 40.0));
                    using (var cb = new SolidBrush(c)) g.FillRectangle(cb, 0, byy, (float)(bw * p), bh);
                    g.DrawString("±" + Theme.Fmt(sd), fs, mu, bw + 4 * k, y + 17 * k);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Fenetre du test
    class OrientationTest : Form
    {
        const int Needed = 6;     // scans par position (~15 s)
        readonly MainForm main;
        readonly List<Trial> trials = new List<Trial>();
        float k = 1f;
        int S(int v) { return (int)Math.Round(v * k); }
        Point P(int x, int y) { return new Point(S(x), S(y)); }
        Size Z(int w, int h) { return new Size(S(w), S(h)); }

        ListBox lbTrials; AntennaView view; ProgressBarX progress; Ranking ranking;
        Label lblName, lblInstr, lblLive, lblVerdict, lblStatus, lblSub;
        IconButton btnMeasure, btnSkip, btnAdd, btnRecheck, btnExport, btnRestart;

        int current;
        bool measuring, recheck, syncing;
        int skip;                 // scans ignores juste apres le clic (antennes encore en mouvement)
        Trial target;

        public OrientationTest(MainForm owner)
        {
            main = owner;
            AutoScaleMode = AutoScaleMode.None;
            using (var g0 = CreateGraphics()) k = g0.DpiX / 96f;
            Text = "Wi-Fi Antennes - Test d'orientation";
            BackColor = Theme.Bg; ForeColor = Theme.Fg; Font = Theme.Ui(9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ClientSize = Z(920, 540);
            try { Icon = owner.Icon; } catch { }

            trials.Add(new Trial { Name = "Deux verticales", A1 = 0, A2 = 0 });
            trials.Add(new Trial { Name = "Une verticale, une horizontale", A1 = 0, A2 = 90 });
            trials.Add(new Trial { Name = "En V à 45°", A1 = -45, A2 = 45 });
            trials.Add(new Trial { Name = "Parallèles à 45°", A1 = 45, A2 = 45 });
            trials.Add(new Trial { Name = "Deux horizontales", A1 = -90, A2 = 90 });

            SuspendLayout();
            BuildUi();
            ResumeLayout(false);

            main.Scanned += OnScan;
            FormClosed += (s, e) => main.Scanned -= OnScan;
            ShowTrial(0);
            UpdateVerdict();
            UpdateButtons();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var wa = Screen.FromControl(main).WorkingArea;
            int x = main.Left + (main.Width - Width) / 2, y = main.Top + (main.Height - Height) / 2;
            Location = new Point(Math.Max(wa.Left, Math.Min(x, wa.Right - Width)), Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height)));
        }

        Label L(Control parent, string text, int x, int y, int w, int h, Font f, Color c)
        {
            var l = new Label { Text = text, Location = P(x, y), Size = Z(w, h), Font = f, ForeColor = c, BackColor = parent.BackColor };
            parent.Controls.Add(l);
            return l;
        }

        IconButton Btn(Control parent, string glyph, string text, int x, int y, int w, int h)
        {
            var b = new IconButton(glyph, text) { Location = P(x, y), Size = Z(w, h) };
            parent.Controls.Add(b);
            return b;
        }

        void BuildUi()
        {
            L(this, Theme.GTest, 18, 12, 40, 36, Theme.Icon(20f), Theme.Accent);
            L(this, "Test d'orientation des antennes", 56, 6, 600, 30, Theme.Ui(15f, FontStyle.Bold), Theme.Fg);
            lblSub = L(this, "", 58, 34, 840, 20, Theme.Ui(9f), Theme.Muted);

            // Liste des positions
            var cList = new Card("Positions à tester") { Location = P(20, 62), Size = Z(290, 400) };
            Controls.Add(cList);
            lbTrials = new ListBox
            {
                Location = P(10, 30), Size = Z(270, 362), BorderStyle = BorderStyle.None,
                BackColor = Theme.Card, ForeColor = Theme.Fg, Font = Theme.Ui(9.5f),
                DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false, ItemHeight = S(46)
            };
            foreach (var t in trials) lbTrials.Items.Add(t);
            lbTrials.DrawItem += DrawTrial;
            lbTrials.SelectedIndexChanged += (s, e) =>
            {
                if (syncing || lbTrials.SelectedIndex < 0) return;
                if (measuring) { syncing = true; lbTrials.SelectedIndex = current; syncing = false; return; }
                ShowTrial(lbTrials.SelectedIndex);
            };
            cList.Controls.Add(lbTrials);

            // Position a mettre en place
            var cCur = new Card("Position à mettre en place") { Location = P(320, 62), Size = Z(320, 400) };
            Controls.Add(cCur);
            view = new AntennaView { Location = P(10, 28), Size = Z(300, 206) };
            cCur.Controls.Add(view);
            lblName = L(cCur, "", 14, 238, 292, 26, Theme.Ui(12f, FontStyle.Bold), Theme.Fg);
            lblInstr = L(cCur, "", 14, 264, 292, 44, Theme.Ui(9f), Theme.Muted);
            progress = new ProgressBarX { Location = P(14, 312), Size = Z(292, 10), Max = Needed };
            cCur.Controls.Add(progress);
            lblLive = L(cCur, "En attente du prochain scan…", 14, 326, 292, 20, Theme.Ui(9f), Theme.Muted);
            btnMeasure = Btn(cCur, Theme.GPlay, "Mesurer cette position", 14, 352, 196, 36);
            btnMeasure.Primary = true;
            btnSkip = Btn(cCur, Theme.GSkip, "Passer", 216, 352, 90, 36);
            btnMeasure.Click += (s, e) => StartMeasure(false);
            btnSkip.Click += (s, e) => { if (measuring) CancelMeasure(); else Next(); };

            // Classement
            var cRank = new Card("Classement") { Location = P(650, 62), Size = Z(250, 400) };
            Controls.Add(cRank);
            ranking = new Ranking { Location = P(12, 30), Size = Z(226, 172), Trials = trials, Needed = Needed };
            cRank.Controls.Add(ranking);
            lblVerdict = L(cRank, "", 12, 208, 228, 186, Theme.Ui(9f), Theme.Fg);

            // Barre d'outils
            btnAdd = Btn(this, Theme.GAdd, "Ajouter une position", 20, 472, 186, 34);
            btnRecheck = Btn(this, Theme.GCheck, "Revérifier le gagnant", 212, 472, 190, 34);
            btnExport = Btn(this, Theme.GSave, "Exporter (CSV)", 408, 472, 140, 34);
            btnRestart = Btn(this, Theme.GRefresh, "Recommencer", 554, 472, 140, 34);
            btnAdd.Click += (s, e) => AddCustom();
            btnRecheck.Click += (s, e) => StartMeasure(true);
            btnExport.Click += (s, e) => Export();
            btnRestart.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Effacer toutes les mesures du test ?", "Test d'orientation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ResetAll("Mesures effacées : recommence par la première position.");
            };

            lblStatus = L(this, "Place les antennes comme sur le dessin, lâche-les, puis clique sur « Mesurer cette position ».", 20, 514, 880, 20, Theme.Ui(8.5f), Theme.Muted);
        }

        // ---------------------------------------------------------------- Navigation
        void ShowTrial(int i)
        {
            if (i < 0 || i >= trials.Count) return;
            current = i;
            var t = trials[i];
            view.Trial = t; view.Invalidate();
            lblName.Text = (i + 1) + ". " + t.Name;
            lblInstr.Text = t.Custom
                ? "Mets le PC et les antennes dans la position décrite, lâche tout, puis lance la mesure."
                : "Oriente les antennes 1 et 2 comme sur le dessin, lâche-les, puis lance la mesure.";
            progress.Value = Math.Min(t.Samples.Count, Needed); progress.Invalidate();
            syncing = true; lbTrials.SelectedIndex = i; syncing = false;
            lbTrials.Invalidate();
            UpdateButtons();
        }

        void Next()
        {
            int n = trials.FindIndex(current + 1, t => t.Samples.Count < Needed);
            if (n < 0) n = trials.FindIndex(t => t.Samples.Count < Needed);
            if (n < 0) n = (current + 1) % trials.Count;
            ShowTrial(n);
        }

        Trial Winner()
        {
            return trials.Where(t => t.Samples.Count >= Needed).OrderByDescending(t => Trial.Mean(t.Samples)).FirstOrDefault();
        }

        // ---------------------------------------------------------------- Mesure
        void StartMeasure(bool isRecheck)
        {
            if (measuring) return;
            if (isRecheck)
            {
                var w = Winner();
                if (w == null) return;
                ShowTrial(trials.IndexOf(w));
                w.Recheck.Clear();
                lblStatus.Text = "Re-mesure du gagnant : remets les antennes dans cette position, puis ne les touche plus.";
            }
            else
            {
                trials[current].Samples.Clear();
                trials[current].Recheck.Clear();
                lblStatus.Text = "Mesure en cours : ne touche plus aux antennes (~15 s).";
            }
            target = trials[current];
            measuring = true; recheck = isRecheck; skip = 1;
            progress.Value = 0; progress.Invalidate();
            lblLive.Text = "Stabilisation…"; lblLive.ForeColor = Theme.Accent;
            lbTrials.Invalidate(); ranking.Invalidate();
            UpdateVerdict();
            UpdateButtons();
        }

        void CancelMeasure()
        {
            if (!measuring) return;
            measuring = false;
            if (recheck) target.Recheck.Clear(); else target.Samples.Clear();
            lblStatus.Text = "Mesure annulée.";
            lblLive.ForeColor = Theme.Muted;
            ShowTrial(current);
            UpdateVerdict();
        }

        void OnScan(ScanResult r)
        {
            if (IsDisposed) return;
            string ssid = main.SelectedSsid;
            lblSub.Text = "Réseau mesuré : " + (ssid ?? "?") + "    •    " + Needed + " scans par position (~15 s), le premier scan après ton clic est ignoré.";
            var nets = r.Networks.Where(n => n.Ssid == ssid).ToList();
            if (nets.Count == 0)
            {
                lblLive.Text = "Réseau introuvable pour l'instant…"; lblLive.ForeColor = Theme.Muted;
                return;
            }
            int best = nets.Max(n => n.Rssi);
            if (!measuring)
            {
                lblLive.Text = "Signal en direct : " + best + " dBm (" + Theme.LevelName(best) + ")";
                lblLive.ForeColor = Theme.Muted;
                return;
            }
            if (skip > 0) { skip--; lblLive.Text = "Stabilisation…"; return; }

            var list = recheck ? target.Recheck : target.Samples;
            list.Add(best);
            progress.Value = list.Count; progress.Invalidate();
            lblLive.Text = string.Format("Mesure {0}/{1} : {2} dBm", list.Count, Needed, best);
            lblLive.ForeColor = Theme.LevelColor(best);
            lbTrials.Invalidate();
            if (list.Count >= Needed) Finish();
        }

        void Finish()
        {
            measuring = false;
            SystemSounds.Asterisk.Play();
            ranking.Invalidate();
            UpdateVerdict();
            if (recheck)
            {
                lblStatus.Text = "Re-mesure terminée : regarde le verdict.";
                ShowTrial(current);
                return;
            }
            int n = trials.FindIndex(t => t.Samples.Count < Needed);
            if (n >= 0)
            {
                lblStatus.Text = "« " + trials[current].Name + " » mesurée. Suivante : « " + trials[n].Name + " ».";
                ShowTrial(n);
            }
            else
            {
                SystemSounds.Exclamation.Play();
                lblStatus.Text = "Toutes les positions sont mesurées : regarde le classement et le verdict.";
                ShowTrial(current);
            }
        }

        // ---------------------------------------------------------------- Verdict
        void UpdateVerdict()
        {
            var done = trials.Where(t => t.Samples.Count >= Needed).OrderByDescending(t => Trial.Mean(t.Samples)).ToList();
            if (done.Count == 0)
            {
                lblVerdict.Text = "Mesure les positions une par une : le classement se remplit au fur et à mesure.";
                lblVerdict.ForeColor = Theme.Muted;
                return;
            }
            if (done.Count == 1)
            {
                lblVerdict.Text = "Mesure au moins une deuxième position pour pouvoir comparer.";
                lblVerdict.ForeColor = Theme.Muted;
                return;
            }
            var a = done[0]; var b = done[1];
            double ma = Trial.Mean(a.Samples), mb = Trial.Mean(b.Samples), diff = ma - mb;
            double sa = Trial.Sd(a.Samples), sb2 = Trial.Sd(b.Samples);
            double se = Math.Sqrt(sa * sa / a.Samples.Count + sb2 * sb2 / b.Samples.Count);
            double spread = ma - Trial.Mean(done[done.Count - 1].Samples);
            var sb = new StringBuilder();
            if (diff >= 3 && diff >= 2 * se)
            {
                sb.Append("Gagnant net : « " + a.Name + " », +" + Theme.Fmt(diff) + " dB devant « " + b.Name + " ». Garde cette position.");
                lblVerdict.ForeColor = Theme.Good;
            }
            else if (diff >= 1.5)
            {
                sb.Append("Léger avantage pour « " + a.Name + " » (+" + Theme.Fmt(diff) + " dB). Clique sur « Revérifier le gagnant » pour confirmer.");
                lblVerdict.ForeColor = Color.FromArgb(241, 196, 15);
            }
            else
            {
                sb.Append("Pas de vraie différence entre « " + a.Name + " » et « " + b.Name + " » (" + Theme.Fmt(diff) + " dB, dans le bruit de mesure).");
                lblVerdict.ForeColor = Theme.Fg;
            }
            if (done.Count >= 3 && spread < 3)
                sb.Append("\n\nToutes les positions sont à moins de 3 dB : l'orientation change très peu ton signal.");
            if (ma < -72)
                sb.Append("\n\nMême au mieux, le signal reste « " + Theme.LevelName(ma).ToLowerInvariant() + " » : une antenne déportée sur câble ou l'Ethernet aideront plus que l'orientation.");
            if (a.Recheck.Count >= Needed)
            {
                double drift = Trial.Mean(a.Recheck) - ma;
                if (Math.Abs(drift) > 3)
                    sb.Append("\n\nAttention : à la re-mesure, le gagnant a varié de " + Theme.Fmt(drift) + " dB. Le signal fluctue, refais le test pour une conclusion fiable.");
                else
                    sb.Append("\n\nRe-mesure confirmée (écart de " + Theme.Fmt(drift) + " dB).");
            }
            lblVerdict.Text = sb.ToString();
        }

        void UpdateButtons()
        {
            btnMeasure.Enabled = !measuring;
            btnMeasure.Text = (!measuring && current < trials.Count && trials[current].Samples.Count >= Needed) ? "Re-mesurer" : "Mesurer cette position";
            btnSkip.Text = measuring ? "Annuler" : "Passer";
            btnSkip.Glyph = measuring ? Theme.GCancel : Theme.GSkip;
            btnAdd.Enabled = !measuring;
            btnRecheck.Enabled = !measuring && Winner() != null;
            btnExport.Enabled = trials.Any(t => t.Samples.Count >= Needed);
            btnRestart.Enabled = !measuring;
            foreach (var b in new[] { btnMeasure, btnSkip, btnAdd, btnRecheck, btnExport, btnRestart }) b.Invalidate();
        }

        public void ResetAll(string message)
        {
            measuring = false;
            foreach (var t in trials) { t.Samples.Clear(); t.Recheck.Clear(); }
            ranking.Invalidate();
            UpdateVerdict();
            ShowTrial(0);
            lblLive.ForeColor = Theme.Muted;
            lblStatus.Text = message;
        }

        // ---------------------------------------------------------------- Positions perso, export
        void AddCustom()
        {
            string desc = Prompt.Ask(this, "Ajouter une position",
                "Décris la position (ex : tour tournée d'un quart de tour) :", "Position perso " + (trials.Count(t => t.Custom) + 1));
            if (desc == null) return;
            if (desc.Length == 0) desc = "Position perso";
            var tr = new Trial { Name = desc, Custom = true };
            trials.Add(tr);
            lbTrials.Items.Add(tr);
            ShowTrial(trials.Count - 1);
        }

        void DrawTrial(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= trials.Count) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float kk = g.DpiX / 96f;
            var t = trials[e.Index];
            bool sel = e.Index == current;
            using (var bg = new SolidBrush(Theme.Card)) g.FillRectangle(bg, e.Bounds);
            var rr = new RectangleF(e.Bounds.X, e.Bounds.Y + 2 * kk, e.Bounds.Width - 1, e.Bounds.Height - 4 * kk);
            if (sel)
                using (var path = Theme.Round(rr, 6 * kk))
                using (var b = new SolidBrush(Color.FromArgb(40, Theme.Accent)))
                using (var p = new Pen(Color.FromArgb(120, Theme.Accent)))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
            var w = Winner();
            float right = e.Bounds.Right - 6 * kk;
            if (w == t)
            {
                using (var f = Theme.Icon(10f)) using (var b = new SolidBrush(Theme.Good))
                    g.DrawString(Theme.GStar, f, b, right - 16 * kk, e.Bounds.Y + 8 * kk);
                right -= 20 * kk;
            }
            using (var f = Theme.Ui(9.5f, FontStyle.Bold)) using (var b = new SolidBrush(Theme.Fg))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString((e.Index + 1) + ". " + t.Name, f, b, new RectangleF(e.Bounds.X + 8 * kk, e.Bounds.Y + 6 * kk, right - e.Bounds.X - 8 * kk, 20 * kk), sf);

            string status; Color sc;
            if (measuring && t == target)
            {
                int c = recheck ? t.Recheck.Count : t.Samples.Count;
                status = (recheck ? "Re-mesure… " : "Mesure en cours… ") + c + "/" + Needed;
                sc = Theme.Accent;
            }
            else if (t.Samples.Count >= Needed)
            {
                double m = Trial.Mean(t.Samples);
                status = Theme.Fmt(m) + " dBm  ±" + Theme.Fmt(Trial.Sd(t.Samples)) + "  ·  " + Theme.LevelName(m);
                sc = Theme.LevelColor(m);
            }
            else { status = "À tester"; sc = Theme.Muted; }
            using (var f = Theme.Ui(8.5f)) using (var b = new SolidBrush(sc))
                g.DrawString(status, f, b, e.Bounds.X + 8 * kk, e.Bounds.Y + 25 * kk);
        }

        void Export()
        {
            var done = trials.Where(t => t.Samples.Count >= Needed).OrderByDescending(t => Trial.Mean(t.Samples)).ToList();
            if (done.Count == 0) return;
            using (var d = new SaveFileDialog())
            {
                d.Filter = "Fichier CSV (*.csv)|*.csv";
                d.FileName = "test-orientation-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".csv";
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("Rang;Position;Réseau;Moyenne (dBm);Écart-type (dB);Mesures (dBm);Re-mesure (dBm)");
                int rank = 1;
                foreach (var t in done)
                    sb.AppendLine(string.Join(";", new[] {
                        (rank++).ToString(), MainForm.CsvText(t.Name), MainForm.CsvText(main.SelectedSsid),
                        Theme.Fmt(Trial.Mean(t.Samples)), Theme.Fmt(Trial.Sd(t.Samples)),
                        MainForm.CsvText(string.Join(" ", t.Samples)),
                        t.Recheck.Count >= Needed ? Theme.Fmt(Trial.Mean(t.Recheck)) : "" }));
                File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                lblStatus.Text = "Résultats exportés : " + Path.GetFileName(d.FileName);
            }
        }
    }
}
