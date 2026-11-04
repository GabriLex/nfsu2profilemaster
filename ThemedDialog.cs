namespace NFSU2ProfileMaster;

public enum DialogKind { Info, Warn, Error, YesNo }

/// <summary>Popup a tema NFSU2 (sostituisce MessageBox / InputBox).</summary>
public sealed class ThemedDialog : Form
{
    private const int W = 400;
    private string? _input;
    private bool _accepted;
    private TextBox? _entry;

    private ThemedDialog(string title, string message, DialogKind kind, bool prompt, string initial)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Bg;
        ClientSize = new Size(W, 10);
        KeyPreview = true;

        Color accent = kind switch { DialogKind.Warn => Theme.Gold, DialogKind.Error => Theme.Danger, _ => Theme.SelGreen };

        var head = new Label
        {
            Text = title.ToUpperInvariant(), ForeColor = accent, Font = Theme.Impact(16),
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 46,
            BackColor = Theme.PanelBg,
        };
        var msg = new Label
        {
            Text = message, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            TextAlign = ContentAlignment.MiddleCenter, BackColor = Theme.PanelBg,
            Dock = DockStyle.Top, Padding = new Padding(16, 12, 16, 12),
            AutoSize = false,
        };
        var measured = TextRenderer.MeasureText(message, msg.Font, new Size(W - 32, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        msg.Height = Math.Max(48, measured.Height + 28);

        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Bg, Padding = new Padding(8, 8, 8, 10) };

        Button Primary(string t) =>
            Theme.MakeButton(t, Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 13);
        Button Secondary(string t) =>
            Theme.MakeButton(t, Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 13);

        if (kind == DialogKind.YesNo || prompt)
        {
            var yes = Primary(prompt ? Strings.Get("ok") : Strings.Get("yes"));
            var no = Secondary(prompt ? Strings.Get("cancel") : Strings.Get("no"));
            yes.Dock = DockStyle.Left; no.Dock = DockStyle.Right;
            yes.Width = no.Width = (W - 16 - 8) / 2;
            yes.Click += (_, _) => Accept();
            no.Click += (_, _) => Close();
            buttons.Controls.Add(yes); buttons.Controls.Add(no);
        }
        else
        {
            var ok = Primary(Strings.Get("ok"));
            ok.Dock = DockStyle.Fill;
            ok.Click += (_, _) => Accept();
            buttons.Controls.Add(ok);
        }

        Control? entryHost = null;
        if (prompt)
        {
            _entry = new TextBox
            {
                Text = initial, BackColor = Theme.PanelBg, ForeColor = Theme.SelGreen, Font = Theme.Impact(14),
                TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill,
            };
            entryHost = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8, 4, 8, 8), BackColor = Theme.Bg };
            entryHost.Controls.Add(_entry);
        }

        // ordine di inserimento: l'ultimo aggiunto con Dock=Top sta piu' in alto
        Controls.Add(buttons);
        if (entryHost != null) Controls.Add(entryHost);
        Controls.Add(msg);
        Controls.Add(head);
        ClientSize = new Size(W, head.Height + msg.Height + (entryHost?.Height ?? 0) + buttons.Height);

        Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Accept(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) Close();
        };
        Shown += (_, _) => { if (_entry != null) { _entry.Focus(); _entry.SelectAll(); } };
    }

    private void Accept()
    {
        _accepted = true;
        _input = _entry?.Text;
        Close();
    }

    public static void Show(IWin32Window owner, string title, string message, DialogKind kind)
    {
        using var d = new ThemedDialog(title, message, kind, false, "");
        d.ShowDialog(owner);
    }

    public static bool Confirm(IWin32Window owner, string title, string message)
    {
        using var d = new ThemedDialog(title, message, DialogKind.YesNo, false, "");
        d.ShowDialog(owner);
        return d._accepted;
    }

    public static string? Prompt(IWin32Window owner, string title, string message, string initial = "")
    {
        using var d = new ThemedDialog(title, message, DialogKind.Info, true, initial);
        d.ShowDialog(owner);
        return d._accepted ? d._input : null;
    }
}
