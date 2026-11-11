// Gestione slot garage: esporta/importa, rimuovi auto, sposta/scambia, nomi XNAME.
// Funzioni e formati file (.u2cc/.u2ci) basati su MemphisRider di AJ_Lethal (MIT).
namespace NFSU2ProfileMaster;

public sealed class SlotManagerForm : Form
{
    private readonly byte[] _d;
    private readonly Action _changed;
    private SlotKind _kind = SlotKind.Career;

    private readonly ListBox _list = new();
    private readonly Label _status = new();
    private readonly Button _tabCareer, _tabMy, _btnExport, _btnImport, _btnClear, _btnUp, _btnDown, _btnSwap, _btnName;

    public SlotManagerForm(byte[] data, Action changed)
    {
        _d = data; _changed = changed;
        Text = Strings.Get("sm_title") + " - AJLethal";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(740, 560);

        var header = new Panel { Left = 0, Top = 0, Width = 740, Height = 64, BackColor = Theme.PanelBg };
        header.Controls.Add(new Label
        {
            Text = Strings.Get("sm_title") + "  ·  MemphisRider by AJLethal", ForeColor = Theme.SelGreen, Font = Theme.Impact(18),
            Left = 14, Top = 6, Width = 700, Height = 30, AutoSize = false,
        });
        header.Controls.Add(new Label
        {
            Text = Strings.Get("sm_hint"), ForeColor = Theme.TxtGrey, Font = Theme.Mono(8),
            Left = 14, Top = 38, Width = 710, Height = 20, AutoSize = false,
        });
        Controls.Add(header);

        _tabCareer = Add(Button(Strings.Get("sm_career"), 11), 14, 76, 180, 34);
        _tabCareer.Click += (_, _) => SwitchKind(SlotKind.Career);
        _tabMy = Add(Button(Strings.Get("sm_mycars"), 11), 202, 76, 180, 34);
        _tabMy.Click += (_, _) => SwitchKind(SlotKind.MyCars);

        _list.SetBounds(14, 122, 368, 360);
        _list.BackColor = Theme.PanelBg; _list.ForeColor = Theme.TxtWhite; _list.Font = Theme.Impact(11);
        _list.BorderStyle = BorderStyle.FixedSingle; _list.DrawMode = DrawMode.OwnerDrawFixed; _list.ItemHeight = 26;
        _list.DrawItem += DrawSlot;
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        Controls.Add(_list);

        int x = 398, w = 328, y = 122;
        _btnExport = Add(Button(Strings.Get("sm_export"), 10), x, y, w, 36); _btnExport.Click += (_, _) => ExportSlot(); y += 44;
        _btnImport = Add(Button(Strings.Get("sm_import"), 10), x, y, w, 36); _btnImport.Click += (_, _) => ImportSlot(); y += 44;
        _btnClear = Add(Button(Strings.Get("sm_clear"), 10), x, y, w, 36); _btnClear.Click += (_, _) => ClearSlot(); y += 52;
        _btnUp = Add(Button(Strings.Get("sm_up"), 10), x, y, 160, 36); _btnUp.Click += (_, _) => MoveBy(-1);
        _btnDown = Add(Button(Strings.Get("sm_down"), 10), x + 168, y, 160, 36); _btnDown.Click += (_, _) => MoveBy(1); y += 44;
        _btnSwap = Add(Button(Strings.Get("sm_swap"), 10), x, y, w, 36); _btnSwap.Click += (_, _) => SwapWith(); y += 52;
        _btnName = Add(Button(Strings.Get("sm_name"), 10), x, y, w, 36); _btnName.Click += (_, _) => NameCar();

        _status.SetBounds(14, 494, 548, 22);   // deve finire prima del pulsante CHIUDI (x=576)
        _status.ForeColor = Theme.TxtGrey; _status.Font = Theme.Mono(9); _status.AutoSize = false;
        Controls.Add(_status);

        var close = Theme.MakeButton(Strings.Get("tn_close"), Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 13);
        close.SetBounds(576, 500, 150, 42);
        close.Click += (_, _) => Close();
        Controls.Add(close);

        SwitchKind(SlotKind.Career);
    }

    // ───────── helper UI ─────────

    private static Button Button(string text, float size) =>
        Theme.MakeButton(text, Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, size);

    private Button Add(Button b, int x, int y, int w, int h)
    {
        b.SetBounds(x, y, w, h);
        Controls.Add(b);
        return b;
    }

    private void SetTab(Button b, bool active)
    {
        b.ForeColor = active ? Theme.SelGreen : Theme.TxtWhite;
        b.BackColor = active ? Theme.SelDark : Theme.BtnNorm;
        b.FlatAppearance.BorderColor = active ? Theme.SelRing : Theme.Border;
    }

    private void SwitchKind(SlotKind k)
    {
        _kind = k;
        SetTab(_tabCareer, k == SlotKind.Career);
        SetTab(_tabMy, k == SlotKind.MyCars);
        _list.Items.Clear();
        for (int i = 0; i < GarageSlots.Count(_d, k); i++) _list.Items.Add(i);
        _list.SelectedIndex = 0;
        UpdateButtons();
    }

    private int Sel => Math.Max(0, _list.SelectedIndex);
    private bool Occupied(int i) => GarageSlots.IsOccupied(_d, _kind, i);

    private string LabelOf(int i) => Occupied(i)
        ? GarageSlots.CarName(GarageSlots.Hash(_d, _kind, i))
        : Strings.Get("sm_empty");

    private void DrawSlot(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg)) e.Graphics.FillRectangle(b, e.Bounds);
        bool occ = Occupied(e.Index);
        string text = LabelOf(e.Index) + (GarageSlots.IsLocked(_d, _kind, e.Index) ? "  [" + Strings.Get("g_locked").ToLowerInvariant() + "]" : "");
        const TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(e.Graphics, (e.Index + 1).ToString(), e.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, 30, e.Bounds.Height), Theme.TxtGrey, f);
        TextRenderer.DrawText(e.Graphics, text, e.Font, new Rectangle(e.Bounds.X + 40, e.Bounds.Y, e.Bounds.Width - 46, e.Bounds.Height),
            sel ? Theme.SelGreen : occ ? Theme.TxtWhite : Theme.TxtGrey, f);
    }

    private void UpdateButtons()
    {
        if (_list.SelectedIndex < 0) return;
        int i = Sel, n = GarageSlots.Count(_d, _kind);
        bool occ = Occupied(i);
        bool career = _kind == SlotKind.Career;
        _btnExport.Enabled = occ;
        _btnImport.Enabled = true;
        _btnClear.Enabled = occ && !(career && i == 0);
        bool movable = occ && !(career && i == 0);
        _btnUp.Enabled = movable && i > 0;
        _btnDown.Enabled = movable && i < n - 1;
        _btnSwap.Enabled = movable;
        _btnName.Enabled = occ;
    }

    private void Changed(string? status = null)
    {
        _changed();
        _list.Invalidate();
        UpdateButtons();
        if (status != null) _status.Text = status;
    }

    private void Info(string msg, DialogKind kind = DialogKind.Info) =>
        ThemedDialog.Show(this, Strings.Get(kind == DialogKind.Error ? "t_error" : "t_done"), msg, kind);

    // ───────── azioni ─────────

    private static string SafeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }

    private void ExportSlot()
    {
        int i = Sel;
        if (!Occupied(i)) return;
        using var dlg = new SaveFileDialog
        {
            DefaultExt = "u2cc", Filter = "MemphisRider custom car slot (*.u2cc)|*.u2cc",
            FileName = SafeName(LabelOf(i)) + ".u2cc", Title = Strings.Get("dlg_save_u2cc"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var (head, inv) = GarageSlots.Export(_d, _kind, i);
            File.WriteAllBytes(dlg.FileName, head);
            if (inv != null) File.WriteAllBytes(Path.ChangeExtension(dlg.FileName, ".u2ci"), inv);
            Info(string.Format(Strings.Get("sm_exported"), i + 1));
        }
        catch (Exception ex) { Info(ex.Message, DialogKind.Error); }
    }

    private void ImportSlot()
    {
        int i = Sel;
        using var dlg = new OpenFileDialog
        {
            Filter = "MemphisRider custom car slot (*.u2cc)|*.u2cc", Title = Strings.Get("dlg_open_u2cc"), CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            byte[] head = File.ReadAllBytes(dlg.FileName);
            byte[]? inv = null;
            string invPath = Path.ChangeExtension(dlg.FileName, ".u2ci");
            if (_kind == SlotKind.Career && File.Exists(invPath)) inv = File.ReadAllBytes(invPath);
            if (head.Length != GarageSlots.HeadSize || (inv != null && inv.Length != GarageSlots.InventorySize))
            {
                Info(Strings.Get("sm_err_file"), DialogKind.Error);
                return;
            }
            if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), string.Format(Strings.Get("sm_confirm_import"), i + 1))) return;
            GarageSlots.Import(_d, _kind, i, head, inv);
            Changed(string.Format(Strings.Get("sm_imported"), i + 1));
            if (_kind == SlotKind.Career && inv == null) Info(Strings.Get("sm_no_inv"));
        }
        catch (InvalidDataException) { Info(Strings.Get("sm_err_file"), DialogKind.Error); }
        catch (Exception ex) { Info(ex.Message, DialogKind.Error); }
    }

    private void ClearSlot()
    {
        int i = Sel;
        if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), string.Format(Strings.Get("sm_confirm_clear"), i + 1))) return;
        try
        {
            GarageSlots.Clear(_d, _kind, i);
            Changed(string.Format(Strings.Get("sm_cleared"), i + 1));
        }
        catch (InvalidOperationException ex)
        {
            Info(Strings.Get(ex.Message == "first" ? "sm_err_first" : "sm_err_last"), DialogKind.Error);
        }
    }

    private void MoveBy(int delta)
    {
        int i = Sel, j = i + delta;
        if (j < 0 || j >= GarageSlots.Count(_d, _kind)) return;
        GarageSlots.Swap(_d, _kind, i, j);
        _list.SelectedIndex = j;
        Changed(string.Format(Strings.Get("sm_swapped"), i + 1, j + 1));
    }

    private void SwapWith()
    {
        int i = Sel, n = GarageSlots.Count(_d, _kind);
        var targets = Enumerable.Range(0, n).Where(t => t != i).ToList();
        var labels = targets.Select(t => $"{t + 1}  ·  {LabelOf(t)}").ToList();
        int pick = PickDialog.Choose(this, Strings.Get("sm_swap").TrimEnd('.'), string.Format(Strings.Get("sm_swap_pick"), i + 1), labels);
        if (pick < 0) return;
        int j = targets[pick];
        GarageSlots.Swap(_d, _kind, i, j);
        _list.SelectedIndex = j;
        Changed(string.Format(Strings.Get("sm_swapped"), i + 1, j + 1));
    }

    private void NameCar()
    {
        int i = Sel;
        uint slotHash = GarageSlots.Hash(_d, _kind, i);
        string? input = ThemedDialog.Prompt(this, Strings.Get("sm_name"), Strings.Get("sm_name_prompt"));
        if (string.IsNullOrWhiteSpace(input)) return;
        string xname = input.Trim().ToUpperInvariant();
        if (!UserXnames.IsValidName(xname))
        {
            Info(Strings.Get("sm_name_err"), DialogKind.Error);
            return;
        }
        uint hash = UserXnames.Hash(xname);
        string msg = string.Format(Strings.Get("sm_name_confirm"), xname, UserXnames.HashText(hash));
        if (hash != slotHash) msg += "\n\n" + string.Format(Strings.Get("sm_name_mismatch"), UserXnames.HashText(slotHash));
        if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), msg)) return;
        try
        {
            UserXnames.Add(xname);
            _list.Invalidate();
            _status.Text = Strings.Get("sm_name_saved");
        }
        catch (Exception ex) { Info(ex.Message, DialogKind.Error); }
    }
}
