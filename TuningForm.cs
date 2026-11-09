// Tuning delle parti performance di uno slot. Funzione basata su NFSU2-Savegame-Editor di hackch (MIT).
namespace NFSU2ProfileMaster;

public sealed class TuningForm : Form
{
    private readonly byte[] _data;
    private readonly int _slot;
    private readonly Action _changed;
    private readonly List<(CheckBox Box, int Index)> _partBoxes = new();
    private readonly List<(CheckBox Box, int Index)> _uniqueBoxes = new();
    private bool _loading;

    public TuningForm(byte[] data, int slot, string carName, Action changed)
    {
        _data = data; _slot = slot; _changed = changed;

        Text = string.Format(Strings.Get("tn_title"), slot + 1);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 640);

        var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.PanelBg };
        header.Controls.Add(new Label
        {
            Text = string.Format(Strings.Get("tn_title"), slot + 1) + "  ·  hackch", ForeColor = Theme.SelGreen, Font = Theme.Impact(15),
            Left = 14, Top = 6, Width = 430, Height = 28, AutoSize = false,
        });
        header.Controls.Add(new Label
        {
            Text = carName, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            Left = 14, Top = 34, Width = 430, Height = 22, AutoSize = false,
        });

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.Bg, Padding = new Padding(10, 8, 10, 10) };
        var max = Theme.MakeButton(Strings.Get("tn_max"), Theme.Gold, Theme.BtnNorm, Theme.Gold, Theme.BtnHov, Theme.Gold, 13);
        var close = Theme.MakeButton(Strings.Get("tn_close"), Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 13);
        max.Dock = DockStyle.Left; close.Dock = DockStyle.Right;
        max.Width = close.Width = 215;
        max.Click += (_, _) => { CareerSave.MaxOut(_data, _slot); _changed(); LoadFromData(); };
        close.Click += (_, _) => Close();
        footer.Controls.Add(max);
        footer.Controls.Add(close);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, BackColor = Theme.Bg, Padding = new Padding(12, 6, 0, 6),
        };

        flow.Controls.Add(NoteLabel(Strings.Get("tn_note")));
        foreach (var cat in PerfData.TuningCategories)
        {
            flow.Controls.Add(CategoryHeader(cat.Label));
            foreach (int idx in cat.Indices)
            {
                var part = PerfData.Parts[idx];
                string text = part.Label + (part.Level is { } lv ? $"   (L{lv})" : "");
                var cb = MakeCheck(text, part.Active);
                if (part.Conflicts.Length > 0)
                    new ToolTip().SetToolTip(cb, "Conflicts: " + string.Join(", ", part.Conflicts.Select(c => PerfData.Parts[c].Label)));
                int captured = idx;
                cb.CheckedChanged += (_, _) =>
                {
                    if (_loading) return;
                    CareerSave.SetPerfFlag(_data, _slot, captured, cb.Checked);
                    _changed();
                };
                _partBoxes.Add((cb, idx));
                flow.Controls.Add(cb);
            }
        }

        flow.Controls.Add(CategoryHeader(Strings.Get("tn_unique")));
        foreach (var u in PerfData.UniqueParts)
        {
            var cb = MakeCheck("Unique " + u.Label, true);
            int captured = u.Index;
            cb.CheckedChanged += (_, _) =>
            {
                if (_loading) return;
                CareerSave.SetUniqueFlag(_data, _slot, captured, cb.Checked);
                _changed();
            };
            _uniqueBoxes.Add((cb, u.Index));
            flow.Controls.Add(cb);
        }

        Controls.Add(flow);
        Controls.Add(footer);
        Controls.Add(header);
        LoadFromData();
    }

    private void LoadFromData()
    {
        _loading = true;
        try
        {
            foreach (var (box, idx) in _partBoxes) box.Checked = CareerSave.GetPerfFlag(_data, _slot, idx);
            foreach (var (box, idx) in _uniqueBoxes) box.Checked = CareerSave.GetUniqueValue(_data, _slot, idx) != 0;
        }
        finally { _loading = false; }
    }

    private static CheckBox MakeCheck(string text, bool enabled) => new()
    {
        Text = text, Enabled = enabled, AutoSize = false, Width = 400, Height = 24,
        ForeColor = enabled ? Theme.TxtWhite : Theme.TxtGrey, BackColor = Theme.Bg,
        Font = Theme.Impact(10.5f), FlatStyle = FlatStyle.Flat, Margin = new Padding(8, 1, 0, 1),
        Cursor = enabled ? Cursors.Hand : Cursors.Default,
    };

    private static Label CategoryHeader(string text) => new()
    {
        Text = text.ToUpperInvariant(), ForeColor = Theme.SelGreen, Font = Theme.Impact(12),
        AutoSize = false, Width = 400, Height = 26, Margin = new Padding(0, 10, 0, 2),
        TextAlign = ContentAlignment.BottomLeft,
    };

    private static Label NoteLabel(string text) => new()
    {
        Text = text, ForeColor = Theme.TxtGrey, Font = Theme.Mono(8),
        AutoSize = false, Width = 400, Height = 22, Margin = new Padding(0, 0, 0, 0),
    };
}
