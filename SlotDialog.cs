namespace NFSU2ProfileMaster;

/// <summary>Elenco di voci con casella e, se richiesto, slot di destinazione per ciascuna.</summary>
public sealed class SlotDialog : Form
{
    private readonly List<(CheckBox Box, ComboBox? Target)> _rows = new();
    private bool _accepted;

    private SlotDialog(string title, string message, IReadOnlyList<(string Label, bool Checked)> rows, int targetCount, IReadOnlyList<int>? defaults)
    {
        const int W = 480;
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Bg;
        KeyPreview = true;

        var head = new Label
        {
            Text = title.ToUpperInvariant(), ForeColor = Theme.SelGreen, Font = Theme.Impact(16),
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 44, BackColor = Theme.PanelBg,
        };
        var msg = new Label
        {
            Text = message, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 34, BackColor = Theme.PanelBg,
        };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Bg, Padding = new Padding(8, 8, 8, 10) };
        var ok = Theme.MakeButton(Strings.Get("ok"), Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 13);
        var cancel = Theme.MakeButton(Strings.Get("cancel"), Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 13);
        ok.Dock = DockStyle.Left; cancel.Dock = DockStyle.Right;
        ok.Width = cancel.Width = (W - 16 - 8) / 2;
        ok.Click += (_, _) => { _accepted = true; Close(); };
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
        for (int i = 0; i < rows.Count; i++)
        {
            int y = 8 + i * 32;
            var cb = new CheckBox
            {
                Text = rows[i].Label, Checked = rows[i].Checked, AutoSize = false, Left = 12, Top = y, Height = 26,
                Width = targetCount > 0 ? 300 : 440, ForeColor = Theme.TxtWhite, BackColor = Theme.Bg,
                Font = Theme.Impact(10.5f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            };
            body.Controls.Add(cb);
            ComboBox? target = null;
            if (targetCount > 0)
            {
                body.Controls.Add(new Label
                {
                    Text = "->", ForeColor = Theme.TxtGrey, Font = Theme.Impact(11), AutoSize = false,
                    Left = 318, Top = y, Width = 24, Height = 26, TextAlign = ContentAlignment.MiddleCenter,
                });
                target = new ComboBox
                {
                    Left = 346, Top = y, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.PanelBg, ForeColor = Theme.TxtWhite, Font = Theme.Impact(10.5f),
                };
                for (int t = 0; t < targetCount; t++) target.Items.Add($"Slot {t + 1}");
                int def = defaults != null && i < defaults.Count ? defaults[i] : i;
                target.SelectedIndex = Math.Min(Math.Max(def, 0), targetCount - 1);
                body.Controls.Add(target);
            }
            _rows.Add((cb, target));
        }

        Controls.Add(body);
        Controls.Add(buttons);
        Controls.Add(msg);
        Controls.Add(head);
        int bodyH = Math.Min(rows.Count * 32 + 16, 352);
        ClientSize = new Size(W, head.Height + msg.Height + bodyH + buttons.Height);

        Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    /// <summary>Ritorna le voci spuntate (indice riga, slot destinazione da 0 oppure -1), o null se annullato.</summary>
    public static List<(int Row, int Target)>? Select(IWin32Window owner, string title, string message,
        IReadOnlyList<(string Label, bool Checked)> rows, int targetCount = 0, IReadOnlyList<int>? defaultTargets = null)
    {
        using var d = new SlotDialog(title, message, rows, targetCount, defaultTargets);
        d.ShowDialog(owner);
        if (!d._accepted) return null;
        var result = new List<(int Row, int Target)>();
        for (int i = 0; i < d._rows.Count; i++)
            if (d._rows[i].Box.Checked)
                result.Add((i, d._rows[i].Target?.SelectedIndex ?? -1));
        return result;
    }
}
