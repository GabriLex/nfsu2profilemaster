// Garage editor: profilo, slot auto, tuning. Funzioni basate su NFSU2-Savegame-Editor di hackch (MIT).
using System.Text.Json;
using static NFSU2ProfileMaster.CareerSave;

namespace NFSU2ProfileMaster;

public sealed class GarageForm : Form
{
    private readonly SaveDocument _doc;
    private bool _dirty;

    private readonly TextBox _name = new(), _money = new();
    private readonly Label _checks = new(), _status = new(), _slotTitle = new(), _slotState = new(), _docTitle = new();
    private readonly ListBox _slots = new();
    private readonly ComboBox _donors = new();
    private readonly Button _btnLock, _btnParts, _btnTuning, _btnReplace, _btnSave;

    private byte[] D => _doc.Data;

    public GarageForm(SaveDocument doc)
    {
        _doc = doc;
        Text = Strings.Get("g_title") + " - hackch";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(780, 560);

        // ── intestazione ──
        var header = new Panel { Left = 0, Top = 0, Width = 780, Height = 64, BackColor = Theme.PanelBg };
        header.Controls.Add(new Label
        {
            Text = Strings.Get("g_title") + "  ·  hackch", ForeColor = Theme.SelGreen, Font = Theme.Impact(18),
            Left = 14, Top = 6, Width = 500, Height = 30, AutoSize = false,
        });
        _docTitle.SetBounds(14, 38, 750, 20);
        _docTitle.ForeColor = Theme.TxtGrey; _docTitle.Font = Theme.Mono(9); _docTitle.AutoSize = false;
        _docTitle.Text = doc.Title;
        header.Controls.Add(_docTitle);
        Controls.Add(header);

        // ── profilo ──
        Controls.Add(MakeLabel(Strings.Get("g_driver"), 14, 78, 120));
        StyleBox(_name, 136, 74, 230); _name.MaxLength = CareerLayout.NameMaxBytes;
        Controls.Add(_name);
        Controls.Add(MakeLabel(Strings.Get("g_money"), 380, 78, 70));
        StyleBox(_money, 452, 74, 150); _money.MaxLength = 10;
        Controls.Add(_money);
        var apply = Theme.MakeButton(Strings.Get("g_apply_profile"), Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 11);
        apply.SetBounds(614, 72, 152, 32);
        apply.Click += (_, _) => ApplyProfile();
        Controls.Add(apply);
        _checks.SetBounds(14, 110, 750, 20);
        _checks.ForeColor = Theme.TxtGrey; _checks.Font = Theme.Mono(9); _checks.AutoSize = false;
        Controls.Add(_checks);

        // ── slot ──
        Controls.Add(MakeSection(Strings.Get("g_slots"), 14, 140, 752));
        _slots.SetBounds(14, 172, 330, 290);
        _slots.BackColor = Theme.PanelBg; _slots.ForeColor = Theme.TxtWhite; _slots.Font = Theme.Impact(11);
        _slots.BorderStyle = BorderStyle.FixedSingle; _slots.DrawMode = DrawMode.OwnerDrawFixed; _slots.ItemHeight = 30;
        _slots.DrawItem += DrawSlot;
        _slots.SelectedIndexChanged += (_, _) => UpdateDetail();
        Controls.Add(_slots);

        _slotTitle.SetBounds(360, 172, 406, 30);
        _slotTitle.ForeColor = Theme.TxtWhite; _slotTitle.Font = Theme.Impact(15); _slotTitle.AutoSize = false;
        _slotState.SetBounds(360, 204, 406, 22);
        _slotState.ForeColor = Theme.Gold; _slotState.Font = Theme.Impact(11); _slotState.AutoSize = false;
        Controls.Add(_slotTitle);
        Controls.Add(_slotState);

        _btnLock = SmallButton("", 360, 236, 196);
        _btnLock.Click += (_, _) => ToggleLock();
        _btnParts = SmallButton("", 570, 236, 196);
        _btnParts.Click += (_, _) => ToggleParts();
        _btnTuning = SmallButton(Strings.Get("g_tuning"), 360, 276, 196);
        _btnTuning.Click += (_, _) => OpenTuning();
        var unlockAll = SmallButton(Strings.Get("g_unlock_all"), 570, 276, 196);
        unlockAll.Click += (_, _) => UnlockAllSlots();

        _donors.SetBounds(360, 326, 406, 28);
        _donors.DropDownStyle = ComboBoxStyle.DropDownList; _donors.FlatStyle = FlatStyle.Flat;
        _donors.BackColor = Theme.PanelBg; _donors.ForeColor = Theme.TxtWhite; _donors.Font = Theme.Impact(11);
        _donors.DrawMode = DrawMode.OwnerDrawFixed; _donors.ItemHeight = 24; _donors.MaxDropDownItems = 12;
        foreach (var d in Donors.All) _donors.Items.Add(d.Name);
        if (_donors.Items.Count > 0) _donors.SelectedIndex = 0;
        _donors.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _donors.Items[e.Index]?.ToString() ?? "", e.Font, Rectangle.Inflate(e.Bounds, -6, 0),
                sel ? Theme.SelGreen : Theme.TxtWhite, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
        Controls.Add(_donors);

        _btnReplace = Theme.MakeButton("", Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 12);
        _btnReplace.SetBounds(360, 362, 406, 36);
        _btnReplace.Click += (_, _) => ReplaceCar();
        Controls.Add(_btnReplace);

        var btnExtract = SmallButton(Strings.Get("g_extract_slots"), 360, 412, 196);
        btnExtract.Click += (_, _) => ExtractSlots();
        var btnApplySlots = SmallButton(Strings.Get("g_apply_slots"), 570, 412, 196);
        btnApplySlots.Click += (_, _) => ApplySlots();

        // ── barra inferiore ──
        _status.SetBounds(14, 478, 560, 24);
        _status.ForeColor = Theme.TxtGrey; _status.Font = Theme.Mono(9); _status.AutoSize = false;
        Controls.Add(_status);
        _btnSave = Theme.MakeButton(Strings.Get("g_save"), Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 14);
        _btnSave.SetBounds(590, 470, 176, 42);
        _btnSave.Click += (_, _) => Save();
        Controls.Add(_btnSave);

        var note = new Label
        {
            Text = doc.Note, ForeColor = Theme.TxtGrey, Font = Theme.Mono(8), AutoSize = false,
            Left = 14, Top = 520, Width = 752, Height = 28,
        };
        Controls.Add(note);

        FormClosing += (_, e) =>
        {
            if (_dirty && !ThemedDialog.Confirm(this, Strings.Get("t_confirm"), Strings.Get("g_close_dirty"))) e.Cancel = true;
        };

        LoadProfileFields();
        for (int i = 0; i < _doc.SlotCount; i++) _slots.Items.Add(i);
        _slots.SelectedIndex = 0;
        RefreshAll();
    }

    // ───────── helper UI ─────────

    private static Label MakeLabel(string text, int x, int y, int w) => new()
    {
        Text = text, Left = x, Top = y, Width = w, Height = 22, AutoSize = false,
        ForeColor = Theme.TxtGrey, Font = Theme.Impact(12), TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Panel MakeSection(string text, int x, int y, int w)
    {
        var p = new Panel { Left = x, Top = y, Width = w, Height = 26, BackColor = Theme.Bg };
        p.Controls.Add(new Label
        {
            Text = text, Left = 12, Top = 0, Height = 26, AutoSize = true, ForeColor = Theme.TxtWhite,
            Font = Theme.Impact(13), TextAlign = ContentAlignment.MiddleLeft,
        });
        p.Paint += (_, e) => { using var b = new SolidBrush(Theme.SelGreen); e.Graphics.FillRectangle(b, 0, 4, 4, 18); };
        return p;
    }

    private static void StyleBox(TextBox t, int x, int y, int w)
    {
        t.SetBounds(x, y, w, 28);
        t.BackColor = Theme.PanelBg; t.ForeColor = Theme.Gold; t.Font = Theme.Impact(13);
        t.BorderStyle = BorderStyle.FixedSingle;
    }

    private Button SmallButton(string text, int x, int y, int w)
    {
        var b = Theme.MakeButton(text, Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
        b.SetBounds(x, y, w, 32);
        Controls.Add(b);
        return b;
    }

    // ───────── stato ─────────

    private int Slot => Math.Max(0, _slots.SelectedIndex);

    private string CarNameOf(int slot) => GetSlotState(D, slot) switch
    {
        SlotState.Locked => Strings.Get("g_hidden_car"),
        SlotState.Open => Strings.Get("g_open_car"),
        _ => Donors.Identify(GetCarTypeHash(D, slot))?.Name ?? Strings.Get("g_unknown_car"),
    };

    private string StateText(int slot) => GetSlotState(D, slot) switch
    {
        SlotState.Locked => Strings.Get("g_locked"),
        SlotState.Open => Strings.Get("g_open"),
        _ => Strings.Get("g_filled"),
    };

    private void DrawSlot(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg)) e.Graphics.FillRectangle(b, e.Bounds);
        var state = GetSlotState(D, e.Index);
        Color stateColor = state == SlotState.Filled ? Theme.SelGreen : state == SlotState.Open ? Theme.Gold : Theme.TxtGrey;
        string car = state == SlotState.Filled ? (Donors.Identify(GetCarTypeHash(D, e.Index))?.Name ?? Strings.Get("g_unknown_car")) : StateText(e.Index);
        var numRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 28, e.Bounds.Height);
        var textRect = new Rectangle(e.Bounds.X + 38, e.Bounds.Y, e.Bounds.Width - 44, e.Bounds.Height);
        const TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(e.Graphics, (e.Index + 1).ToString(), e.Font, numRect, Theme.TxtGrey, f);
        TextRenderer.DrawText(e.Graphics, car, e.Font, textRect, sel ? Theme.SelGreen : stateColor, f);
    }

    private void LoadProfileFields()
    {
        _name.Text = GetName(D);
        _money.Text = GetMoney(D).ToString();
    }

    private void RefreshAll()
    {
        _slots.Invalidate();
        UpdateDetail();
        _checks.Text = Strings.Get("g_checks") + (_doc.Checksums.AllOk ? Strings.Get("g_ok_valid") : Strings.Get("g_mismatch"));
        _status.Text = _dirty ? Strings.Get("g_dirty") : Strings.Get("g_clean");
        _status.ForeColor = _dirty ? Theme.Gold : Theme.TxtGrey;
    }

    private void UpdateDetail()
    {
        if (_slots.SelectedIndex < 0) return;
        int s = Slot;
        var state = GetSlotState(D, s);
        _slotTitle.Text = $"{s + 1}  ·  {CarNameOf(s)}";
        _slotState.Text = StateText(s);
        _btnLock.Text = (state == SlotState.Locked ? Strings.Get("g_unlock_slot") : Strings.Get("g_lock_slot"));
        bool filled = state == SlotState.Filled;
        _btnTuning.Enabled = filled;
        _btnParts.Enabled = filled;
        int unlocked = filled ? CountUnlocked(D, s) : 0;
        _btnParts.Text = (filled && unlocked == UnlockTable.Offsets.Length ? Strings.Get("g_lock_parts") : Strings.Get("g_unlock_parts"));
        _btnReplace.Text = IsCarBought(D, s) ? Strings.Get("g_replace") : Strings.Get("g_add");
        _btnReplace.Enabled = state != SlotState.Locked && _donors.Items.Count > 0;
        _donors.Enabled = _btnReplace.Enabled;
    }

    private void MarkDirty()
    {
        _dirty = true;
        RefreshAll();
    }

    // ───────── azioni ─────────

    private void ApplyProfile()
    {
        if (!long.TryParse(_money.Text.Trim(), out long money) || money < 0 || money > CareerLayout.MaxMoney)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("g_err_money"), DialogKind.Error);
            return;
        }
        string name = _name.Text;
        if (ValidateName(name) != null)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("g_err_name"), DialogKind.Error);
            return;
        }
        SetMoney(D, money);
        SetName(D, name);
        MarkDirty();
        _status.Text = Strings.Get("g_profile_ok");
    }

    private void ToggleLock()
    {
        int s = Slot;
        SetSlotLocked(D, s, GetSlotState(D, s) != SlotState.Locked);
        MarkDirty();
    }

    private void ToggleParts()
    {
        int s = Slot;
        bool on = CountUnlocked(D, s) != UnlockTable.Offsets.Length;
        if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), string.Format(Strings.Get("g_confirm_parts"), s + 1))) return;
        SetUnlocks(D, s, on);
        MarkDirty();
    }

    private void UnlockAllSlots()
    {
        for (int i = 0; i < _doc.SlotCount; i++) SetSlotLocked(D, i, false);
        MarkDirty();
    }

    private void OpenTuning()
    {
        int s = Slot;
        using var f = new TuningForm(D, s, CarNameOf(s), MarkDirty);
        f.ShowDialog(this);
        RefreshAll();
    }

    private void ReplaceCar()
    {
        if (_donors.SelectedIndex < 0 || _donors.SelectedIndex >= Donors.All.Count) return;
        var donor = Donors.All[_donors.SelectedIndex];
        int s = Slot;
        if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), string.Format(Strings.Get("g_confirm_replace"), donor.Name, s + 1))) return;
        try
        {
            SetSlotCar(D, s, donor);
            MarkDirty();
        }
        catch (Exception ex)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), ex.Message, DialogKind.Error);
        }
    }

    private string SlotLabel(int slot) => IsCarBought(D, slot)
        ? (Donors.Identify(GetCarTypeHash(D, slot))?.Name ?? Strings.Get("g_unknown_car"))
        : Strings.Get("g_open");

    private void ExtractSlots()
    {
        var rows = Enumerable.Range(0, _doc.SlotCount)
            .Select(i => ($"{i + 1}  ·  {SlotLabel(i)}", IsCarBought(D, i)))
            .ToList();
        var sel = SlotDialog.Select(this, Strings.Get("sl_extract_title"), Strings.Get("sl_extract_msg"), rows);
        if (sel == null) return;
        if (sel.Count == 0)
        {
            ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get("sl_none"), DialogKind.Warn);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            DefaultExt = "json", Filter = "JSON (*.json)|*.json", FileName = "nfsu2_slots.json",
            Title = Strings.Get("dlg_save_slots"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var pack = SlotPackService.Extract(D, sel.Select(x => x.Row), GetName(D));
            SlotPackService.Save(pack, dlg.FileName);
            ThemedDialog.Show(this, Strings.Get("t_done"), string.Format(Strings.Get("sl_saved"), pack.Slots.Count), DialogKind.Info);
        }
        catch (Exception ex)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), ex.Message, DialogKind.Error);
        }
    }

    private void ApplySlots()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json", Title = Strings.Get("dlg_open_slots"), CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var pack = SlotPackService.Load(dlg.FileName);
            int count = _doc.SlotCount;
            var rows = pack.Slots
                .Select(e => ($"{e.Slot}  ·  {(string.IsNullOrEmpty(e.CarName) ? Strings.Get("g_unknown_car") : e.CarName)}", true))
                .ToList();
            var defaults = pack.Slots.Select(e => Math.Min(Math.Max(e.Slot - 1, 0), count - 1)).ToList();
            var sel = SlotDialog.Select(this, Strings.Get("sl_apply_title"), Strings.Get("sl_apply_msg"), rows, count, defaults);
            if (sel == null) return;
            if (sel.Count == 0)
            {
                ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get("sl_none"), DialogKind.Warn);
                return;
            }
            if (sel.Select(x => x.Target).Distinct().Count() != sel.Count)
            {
                ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("sl_dup"), DialogKind.Error);
                return;
            }
            if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), string.Format(Strings.Get("sl_confirm_apply"), sel.Count))) return;
            SlotPackService.Apply(D, pack, sel.Select(x => (x.Row, x.Target)).ToList());
            MarkDirty();
            ThemedDialog.Show(this, Strings.Get("t_done"), string.Format(Strings.Get("sl_applied"), sel.Count), DialogKind.Info);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("sl_invalid"), DialogKind.Error);
        }
        catch (Exception ex)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), ex.Message, DialogKind.Error);
        }
    }

    private void Save()
    {
        try
        {
            string? backup = _doc.Save();
            _dirty = false;
            RefreshAll();
            string msg = backup != null ? string.Format(Strings.Get("g_saved"), backup) : Strings.Get("g_saved2");
            ThemedDialog.Show(this, Strings.Get("t_done"), msg, DialogKind.Info);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("err_admin"), DialogKind.Error);
        }
        catch (Exception ex)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), ex.Message, DialogKind.Error);
        }
    }
}
