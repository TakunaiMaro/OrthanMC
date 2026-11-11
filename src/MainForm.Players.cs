using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading; // for Timer disambiguation

namespace McPanel
{
    public partial class MainForm
    {
        private class PlayerSession
        {
            public string Name;
            public string Ip;
            public DateTime Joined = DateTime.Now;
        }

        private readonly Dictionary<string, PlayerSession> _playerCache = new Dictionary<string, PlayerSession>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void ShowPlayerJoin(string name)
        {
            try
            {
                NotifyIcon ni = new NotifyIcon();
                ni.Icon = SystemIcons.Information;
                ni.Visible = true;
                ni.BalloonTipTitle = "Minecraft";
                ni.BalloonTipText = name + " ha entrado al server ✅";
                ni.ShowBalloonTip(3000);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 4000;
                t.Tick += delegate { t.Stop(); try { ni.Visible = false; ni.Dispose(); } catch { } };
                t.Start();
            }
            catch { }
        }

        private bool IsOp(string name)
        {
            try
            {
                string p = _paths.OpsJson;
                if (!File.Exists(p)) return false;
                string txt = File.ReadAllText(p);
                return txt.IndexOf("\"name\"" + ":" + " \"" + name + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private bool IsBanned(string name)
        {
            try
            {
                string p = _paths.BansJson;
                if (!File.Exists(p)) return false;
                string txt = File.ReadAllText(p);
                return txt.IndexOf("\"name\"" + ":" + " \"" + name + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        private void ToggleOp(string name)
        {
            try
            {
                bool isOpNow = IsOp(name);
                if (_bridge.IsRunning)
                {
                    _bridge.Send(isOpNow ? ("deop " + name) : ("op " + name));
                    _toast.ShowMessage(isOpNow ? ("Deop a " + name) : ("Op a " + name), Palette.Blue, "check");
                    return;
                }
                string p = _paths.OpsJson;
                string content = File.Exists(p) ? File.ReadAllText(p) : "[]";
                if (isOpNow)
                {
                    content = Regex.Replace(content, @"\s*\{\s*[^\{\}]*""name""\s*:\s*""" + Regex.Escape(name) + @"""[^\{\}]*\}\s*,?", "", RegexOptions.IgnoreCase);
                    content = content.Replace("[,", "[").Replace(",]", "]").Replace(",,", ",").Trim();
                    if (content == "[" || content == "") content = "[]";
                    File.WriteAllText(p, content);
                }
                else
                {
                    if (content.Trim() == "[]") content = "[\n  {\n    \"uuid\": \"\",\n    \"name\": \"" + name + "\",\n    \"level\": 4,\n    \"bypassesPlayerLimit\": false\n  }\n]";
                    else
                    {
                        content = content.Trim();
                        if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1).TrimEnd() + ",\n  {\n    \"uuid\": \"\",\n    \"name\": \"" + name + "\",\n    \"level\": 4,\n    \"bypassesPlayerLimit\": false\n  }\n]";
                    }
                    File.WriteAllText(p, content);
                }
                if (_plyList != null) _plyList.Invalidate();
                _toast.ShowMessage(isOpNow ? ("Desop: " + name) : ("Op: " + name), Palette.Blue, "check");
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Error", ex.Message);
            }
        }
        private void Banplayer(string name)
        {
            try
            {
                bool IsBannedNow = IsBanned(name);
                if (_bridge.IsRunning)
                {
                    _bridge.Send(IsBannedNow ? ("unban " + name) : ("ban " + name));
                    _toast.ShowMessage(IsBannedNow ? ("Unban a " + name) : ("Ban a " + name), Palette.Red, "check");
                    return;
                }
                string p= _paths.BansJson;
                string content = File.Exists(p) ? File.ReadAllText(p) : "[]";
                if (IsBannedNow)
                {
                content = Regex.Replace(content, @"\s*\{\s*[^\{\}]*""name""\s*:\s*""" + Regex.Escape(name) + @"""[^\{\}]*\}\s*,?", "", RegexOptions.IgnoreCase);
                content = content.Replace("[,", "[").Replace(",]", "]").Replace(",,", ",").Trim();
                if (content == "[" || content == "") content = "[]";
                File.WriteAllText(p, content);
                }
                else
                {
                if (content.Trim() == "[]") content = "[\n  {\n    \"uuid\": \"\",\n    \"name\": \"" + name + "\",\n    \"level\": 4,\n    \"bypassesPlayerLimit\": false\n  }\n]";
                else
                {
                    content = content.Trim();
                    if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1).TrimEnd() + ",\n  {\n    \"uuid\": \"\",\n    \"name\": \"" + name + "\",\n    \"level\": 4,\n    \"bypassesPlayerLimit\": false\n  }\n]";
                }
                File.WriteAllText(p, content);
                }
            if (_plyList != null) _plyList.Invalidate();
            _toast.ShowMessage(IsBannedNow ? ("Unban: " + name) : ("Ban: " + name), Palette.Red, "check");
        
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Error", ex.Message);
            }   
        }

        private List<string> GetUnBan()
        {
            var options = new List<string>();
            string p = _paths.BansJson;
            string content = File.Exists(p) ? File.ReadAllText(p) : "[]";
            string trimmedContent = content.Trim();
            if (trimmedContent == "[]"|| trimmedContent == "") return options;
            foreach (Match match in Regex.Matches(trimmedContent, @"\{\s*""uuid""\s*:\s*""[^""]*""\s*,\s*""name""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase))
            {
                options.Add("unban " + match.Groups[1].Value);
            }
            return options;
        }

        private void PlyList_DrawItemEx(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            object item = _plyList.Items[e.Index];
            if (!(item is PlayerSession)) return;
            PlayerSession ps = (PlayerSession)item;
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color bg = e.State.HasFlag(DrawItemState.Selected) ? Palette.AccentDim : Palette.Card;
            using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);
            RectangleF gr = new RectangleF(e.Bounds.X + 10, e.Bounds.Y + 8, 16, 16);
            Draw.Glyph(g, "users", gr, e.State.HasFlag(DrawItemState.Selected) ? Palette.Accent : Palette.Muted, 1.6f);
            RectangleF nr = new RectangleF(e.Bounds.X + 36, e.Bounds.Y, 150, e.Bounds.Height);
            TextRenderer.DrawText(g, ps.Name, Fonts.UI(9.5f), Rectangle.Round(nr), Palette.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            TimeSpan ts = DateTime.Now - ps.Joined;
            string timeStr = string.Format("{0:D2}:{1:D2}", ts.TotalHours < 100 ? (int)ts.TotalHours : 99, ts.Minutes);
            RectangleF tr = new RectangleF(e.Bounds.X + 190, e.Bounds.Y, e.Bounds.Width - 320, e.Bounds.Height);
            TextRenderer.DrawText(g, "Tiempo en línea: " + timeStr, Fonts.UI(8.5f), Rectangle.Round(tr), Palette.Dim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            bool op = IsOp(ps.Name);
            string opText = op ? "DESOP" : "OP";
            Size ots = TextRenderer.MeasureText(opText, Fonts.UI(8.5f, FontStyle.Bold));
            RectangleF obr = new RectangleF(e.Bounds.X + e.Bounds.Width - ots.Width - 24, e.Bounds.Y + 7, ots.Width + 16, 18);
            Color obg = op ? Palette.RedDim : Palette.BlueDim;
            Color obc = op ? Palette.Red : Palette.Blue;
            Draw.FillRound(g, obr, 9f, obg);
            Draw.StrokeRound(g, obr, 9f, obc, 1f);
            TextRenderer.DrawText(g, opText, Fonts.UI(8.5f, FontStyle.Bold), Rectangle.Round(obr), obc, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private void PlyList_MouseUpEx(object sender, MouseEventArgs e)
        {
            if (_plyList == null) return;
            int idx = _plyList.IndexFromPoint(e.Location);
            if (idx < 0) return;
            object item = _plyList.Items[idx];
            if (!(item is PlayerSession)) return;
            PlayerSession ps = (PlayerSession)item;
            Rectangle r = _plyList.GetItemRectangle(idx);
            bool op = IsOp(ps.Name);
            string opText = op ? "DESOP" : "OP";
            Size ots = TextRenderer.MeasureText(opText, Fonts.UI(8.5f, FontStyle.Bold));
            Rectangle obr = new Rectangle(r.X + r.Width - ots.Width - 24, r.Y + 7, ots.Width + 16, 18);
            if (obr.Contains(e.Location))
            {
                ToggleOp(ps.Name);
                return;
            }
        }
    }
}
