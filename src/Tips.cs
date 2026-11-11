// Wi-Fi Antennes - diagnostic en direct et conseils pour mieux capter
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace WifiAntennes
{
    class InfoItem
    {
        public int Level;          // 0 = ok, 1 = info, 2 = attention, 3 = probleme
        public string Glyph, Title, Detail;
        public InfoItem(int level, string title, string detail) { Level = level; Title = title; Detail = detail; }
        public InfoItem(string glyph, string title, string detail) { Level = -1; Glyph = glyph; Title = title; Detail = detail; }
    }

    // Liste d'elements icone + titre + texte, hauteur adaptee au contenu
    class InfoList : Canvas
    {
        public List<InfoItem> Items = new List<InfoItem>();

        static Color LevelColor(int l)
        {
            switch (l)
            {
                case 0: return Theme.Good;
                case 1: return Theme.Accent;
                case 2: return Color.FromArgb(241, 160, 40);
                case 3: return Theme.Bad;
                default: return Theme.Accent;
            }
        }
        static string LevelGlyph(int l)
        {
            switch (l)
            {
                case 0: return "";   // coche
                case 2: return "";   // attention
                case 3: return "";   // croix
                default: return "";  // info
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            float k = g.DpiX / 96f;
            float y = 0, tx = 34 * k, tw = Width - tx - 2 * k;
            using (var fi = Theme.Icon(11f))
            using (var ft = Theme.Ui(9.5f, FontStyle.Bold))
            using (var fd = Theme.Ui(8.5f))
            using (var fg = new SolidBrush(Theme.Fg))
            using (var mu = new SolidBrush(Theme.Muted))
            {
                foreach (var it in Items)
                {
                    if (y > Height) break;
                    Color c = LevelColor(it.Level);
                    string glyph = it.Level >= 0 ? LevelGlyph(it.Level) : it.Glyph;
                    using (var b = new SolidBrush(Color.FromArgb(40, c))) g.FillEllipse(b, 2 * k, y + 1 * k, 24 * k, 24 * k);
                    using (var b = new SolidBrush(c))
                    {
                        var gs = g.MeasureString(glyph, fi);
                        g.DrawString(glyph, fi, b, 14 * k - gs.Width / 2, y + 13 * k - gs.Height / 2 + 1 * k);
                    }
                    var ts = g.MeasureString(it.Title, ft, (int)tw);
                    g.DrawString(it.Title, ft, it.Level >= 2 ? new SolidBrush(c) : fg, new RectangleF(tx, y, tw, ts.Height));
                    var ds = g.MeasureString(it.Detail ?? "", fd, (int)tw);
                    g.DrawString(it.Detail ?? "", fd, mu, new RectangleF(tx, y + ts.Height, tw, ds.Height + 2));
                    y += Math.Max(28 * k, ts.Height + ds.Height) + 9 * k;
                }
            }
        }
    }

    class TipsForm : Form
    {
        readonly MainForm main;
        float k = 1f;
        int S(int v) { return (int)Math.Round(v * k); }
        Point P(int x, int y) { return new Point(S(x), S(y)); }
        Size Z(int w, int h) { return new Size(S(w), S(h)); }
        InfoList diag, tips;
        Label lblSub, lblStatus;

        public TipsForm(MainForm owner)
        {
            main = owner;
            AutoScaleMode = AutoScaleMode.None;
            using (var g0 = CreateGraphics()) k = g0.DpiX / 96f;
            Text = "Wi-Fi Antennes - Conseils";
            BackColor = Theme.Bg; ForeColor = Theme.Fg; Font = Theme.Ui(9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            ClientSize = Z(920, 560);
            try { Icon = owner.Icon; } catch { }
            SuspendLayout();
            BuildUi();
            ResumeLayout(false);
            main.Scanned += OnScan;
            FormClosed += (s, e) => main.Scanned -= OnScan;
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

        void BuildUi()
        {
            L(this, "", 18, 12, 40, 36, Theme.Icon(20f), Theme.Accent);
            L(this, "Conseils pour mieux capter", 56, 6, 600, 30, Theme.Ui(15f, FontStyle.Bold), Theme.Fg);
            lblSub = L(this, "Analyse en cours…", 58, 34, 840, 20, Theme.Ui(9f), Theme.Muted);

            var cDiag = new Card("Diagnostic en direct") { Location = P(20, 62), Size = Z(430, 436) };
            Controls.Add(cDiag);
            diag = new InfoList { Location = P(12, 32), Size = Z(408, 396) };
            diag.Items.Add(new InfoItem(1, "En attente du prochain scan…", "Le diagnostic se met à jour toutes les ~2,5 s."));
            cDiag.Controls.Add(diag);

            var cTips = new Card("Ce qui marche le mieux") { Location = P(460, 62), Size = Z(440, 436) };
            Controls.Add(cTips);
            var scroll = new Panel { Location = P(12, 32), Size = Z(422, 396), AutoScroll = true, BackColor = Theme.Card };
            cTips.Controls.Add(scroll);
            tips = new InfoList { Location = new Point(0, 0), Size = Z(398, 560) };
            tips.Items.AddRange(new[]
            {
                new InfoItem("", "Antennes en hauteur et dégagées", "Rien de métallique devant (boîtier, bureau, radiateur) ; décolle la tour du mur et sors-la de sous le bureau."),
                new InfoItem("", "Antenne déportée sur câble (~10-15 €)", "Un socle magnétique avec 1-2 m de câble (connecteur RP-SMA) se visse à la place des antennes : pose-le en hauteur, au meilleur endroit."),
                new InfoItem("", "Ou un adaptateur Wi-Fi USB + rallonge", "Placé en hauteur au bout d'une rallonge USB, il capte souvent bien mieux qu'une carte à l'arrière d'une tour."),
                new InfoItem("", "Supprime le brouillage", "Coupe le partage de connexion du téléphone et éloigne les appareils qui émettent sur le même canal que ta borne."),
                new InfoItem("", "Réglages avancés de la carte", "Connexions réseau → Wi-Fi → Propriétés → Configurer → Avancé : « Agressivité de l'itinérance / Roaming » au minimum, « Bande privilégiée » selon le diagnostic, largeur de canal 2,4 GHz sur 20 MHz."),
                new InfoItem("", "Pas d'économie d'énergie Wi-Fi", "Options d'alimentation → Paramètres de la carte sans fil → Performances maximales ; et dans le Gestionnaire de périphériques, décoche « Autoriser l'ordinateur à éteindre ce périphérique »."),
                new InfoItem("", "Pilote à jour", "Un pilote récent (Windows Update → options avancées → mises à jour facultatives, ou site du fabricant) corrige souvent les décrochages."),
                new InfoItem("", "Le câble reste imbattable", "Si une prise Ethernet existe, c'est la solution la plus stable : aucun réglage d'antenne n'en approchera."),
            });
            scroll.Controls.Add(tips);
            tips.MouseEnter += (s, e) => scroll.Focus();   // molette de la souris

            var b1 = new IconButton("", "Réglages Wi-Fi Windows") { Location = P(20, 508), Size = Z(196, 34) };
            var b2 = new IconButton("", "Connexions réseau (carte)") { Location = P(222, 508), Size = Z(206, 34) };
            var b3 = new IconButton(Theme.GTest, "Test d'orientation") { Location = P(434, 508), Size = Z(170, 34), Primary = true };
            b1.Click += (s, e) => Launch("ms-settings:network-wifi");
            b2.Click += (s, e) => Launch("ncpa.cpl");
            b3.Click += (s, e) => main.OpenTest();
            Controls.AddRange(new Control[] { b1, b2, b3 });
            lblStatus = L(this, "", 616, 516, 290, 20, Theme.Ui(8.5f), Theme.Muted);
        }

        void Launch(string target)
        {
            try { System.Diagnostics.Process.Start(target); }
            catch (Exception ex) { lblStatus.Text = "Ouverture impossible : " + ex.Message; }
        }

        static string Short(Bss b) { return b.Band + " · canal " + b.Channel; }
        static string Prefix(string bssid) { return bssid.Length >= 14 ? bssid.Substring(0, 14) : bssid; }

        void OnScan(ScanResult r)
        {
            if (IsDisposed) return;
            string ssid = main.SelectedSsid;
            lblSub.Text = "Réseau analysé : " + (ssid ?? "?") + "    •    mis à jour à " + r.Time.ToString("HH:mm:ss");
            var items = new List<InfoItem>();
            var mine = r.Networks.Where(n => n.Ssid == ssid).OrderByDescending(n => n.Rssi).ToList();
            if (mine.Count == 0)
            {
                items.Add(new InfoItem(3, "Réseau introuvable", "Aucune borne « " + ssid + " » captée lors de ce scan."));
                diag.Items = items; diag.Invalidate();
                return;
            }
            var best = mine[0];

            // 1. Niveau du signal
            int lv = best.Rssi >= -67 ? 0 : (best.Rssi >= -72 ? 2 : 3);
            items.Add(new InfoItem(lv, "Signal : " + best.Rssi + " dBm (" + Theme.LevelName(best.Rssi).ToLowerInvariant() + ")",
                "Meilleure borne en " + Short(best) + "." + (lv == 3
                    ? " Les réglages aident, mais une antenne déportée ou l'Ethernet régleront le fond du problème."
                    : (lv == 2 ? " Utilisable, mais chaque dB gagné compte." : " Bonne réception."))));

            // 2. Brouillage sur le meme canal (hors autres reseaux emis par la meme borne)
            var myRadios = new HashSet<string>(mine.Select(n => Prefix(n.Bssid)));
            var rivals = r.Networks.Where(n => n.Ssid != ssid && n.Band == best.Band && n.Channel == best.Channel
                    && n.Rssi >= best.Rssi - 10 && !myRadios.Contains(Prefix(n.Bssid)))
                .OrderByDescending(n => n.Rssi).ToList();
            if (rivals.Count > 0)
                items.Add(new InfoItem(2, "Brouillage sur le canal " + best.Channel,
                    string.Join(", ", rivals.Take(2).Select(n => "« " + (n.Ssid.Length > 0 ? n.Ssid : "réseau masqué") + " » (" + n.Rssi + " dBm)"))
                    + " émet sur le même canal que ta meilleure borne. Si c'est ton partage de connexion ou un de tes appareils, coupe-le ou change son canal."));
            else
                items.Add(new InfoItem(0, "Pas de brouillage fort sur le canal " + best.Channel, "Aucun autre réseau puissant ne partage ce canal."));

            // 3. Bande 2,4 / 5 GHz
            var b24 = mine.Where(n => n.FreqMhz < 3000).Select(n => (int?)n.Rssi).Max();
            var b5 = mine.Where(n => n.FreqMhz >= 3000).Select(n => (int?)n.Rssi).Max();
            if (b24.HasValue && b5.HasValue)
            {
                int d = b24.Value - b5.Value;
                if (d >= 8)
                    items.Add(new InfoItem(1, "Le 2,4 GHz porte mieux ici (+" + d + " dB)", "Règle « Bande privilégiée » sur 2,4 GHz : moins rapide, mais bien plus stable à travers les murs."));
                else if (b5.Value >= -67)
                    items.Add(new InfoItem(0, "Le 5 GHz est assez fort (" + b5.Value + " dBm)", "Garde le 5 GHz : il est plus rapide et moins encombré."));
                else
                    items.Add(new InfoItem(1, "2,4 GHz : " + b24.Value + " dBm · 5 GHz : " + b5.Value + " dBm", "Écart faible : préfère le 5 GHz si la connexion tient, sinon force le 2,4 GHz."));
            }
            else
                items.Add(new InfoItem(1, "Une seule bande captée (" + best.Band + ")", "L'autre bande n'arrive pas jusqu'ici."));

            // 4. Bornes de force similaire -> sauts d'itineraire
            int near = mine.Count(n => n.Bssid != best.Bssid && n.Rssi >= best.Rssi - 5);
            if (near >= 2)
                items.Add(new InfoItem(2, near + " autres bornes presque aussi fortes", "Le PC risque de sauter de l'une à l'autre et de décrocher : mets « Agressivité de l'itinérance » au minimum."));
            else if (near == 1)
                items.Add(new InfoItem(1, "Deux bornes de force proche", "Si tu as des micro-coupures, baisse l'agressivité de l'itinérance."));
            else
                items.Add(new InfoItem(0, "Une borne domine nettement", "Bon pour la stabilité : peu de risque de sauts entre bornes."));

            // 5. Borne a laquelle on est connecte
            if (r.ConnectedSsid == ssid)
            {
                var conn = mine.FirstOrDefault(n => n.Bssid == r.ConnectedBssid);
                if (conn != null && conn.Rssi <= best.Rssi - 8)
                    items.Add(new InfoItem(2, "Accroché à une borne plus faible", "Connecté à " + conn.Rssi + " dBm alors qu'une borne est à " + best.Rssi + " dBm : déconnecte puis reconnecte-toi pour basculer."));
                else
                    items.Add(new InfoItem(0, "Connecté à la bonne borne", "Ton PC utilise la meilleure borne disponible (ou presque)."));
            }
            else if (!string.IsNullOrEmpty(r.ConnectedSsid))
                items.Add(new InfoItem(1, "Connecté à « " + r.ConnectedSsid + " »", "Le diagnostic porte sur « " + ssid + " », pas sur le réseau utilisé en ce moment."));
            else
                items.Add(new InfoItem(1, "Pas connecté au Wi-Fi", "Le diagnostic reste valable : il se base sur les bornes captées."));

            // 6. Encombrement du canal
            int crowd = r.Networks.Count(n => n.Band == best.Band && n.Channel == best.Channel);
            if (best.FreqMhz < 3000 && crowd >= 8)
                items.Add(new InfoItem(2, "Canal " + best.Channel + " encombré (" + crowd + " émetteurs)", "Le 2,4 GHz est saturé ici : le 5 GHz ou le câble donneront de meilleurs débits."));

            diag.Items = items;
            diag.Invalidate();
        }
    }
}
