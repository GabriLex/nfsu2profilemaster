namespace NFSU2ProfileMaster;

/// <summary>Piccola finestra a tema per scegliere una voce da una lista.</summary>
public sealed class PickDialog : Form
{
    private readonly ListBox _list;
    private bool _accepted;

    private PickDialog(string title, string message, IReadOnlyList<string> items)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Bg;
        ClientSize = new Size(440, 330);
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
        ok.Width = cancel.Width = (440 - 16 - 8) / 2;
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => Close();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        _list = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.PanelBg, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24,
        };
        foreach (var i in items) _list.Items.Add(i);
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        _list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _list.Items[e.Index]?.ToString() ?? "", e.Font, Rectangle.Inflate(e.Bounds, -6, 0),
                sel ? Theme.SelGreen : Theme.TxtWhite, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
        _list.DoubleClick += (_, _) => Accept();

        Controls.Add(_list);
        Controls.Add(buttons);
        Controls.Add(msg);
        Controls.Add(head);

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
    }

    private void Accept()
    {
        if (_list.SelectedIndex < 0) return;
        _accepted = true;
        Close();
    }

    /// <summary>Ritorna l'indice scelto oppure -1 se annullato.</summary>
    public static int Choose(IWin32Window owner, string title, string message, IReadOnlyList<string> items)
    {
        using var d = new PickDialog(title, message, items);
        d.ShowDialog(owner);
        return d._accepted ? d._list.SelectedIndex : -1;
    }
}
