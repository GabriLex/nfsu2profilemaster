using System.Text.Json;

namespace NFSU2ProfileMaster;

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

public sealed class MainForm : Form
{
    private const int W = 520, Mx = 14, Cw = W - 2 * Mx, HeaderH = 110;

    private readonly List<(Control Ctl, string Key, bool Upper)> _i18n = new();
    private readonly List<Panel> _sections = new();
    private Dictionary<string, string> _saves = new();
    private readonly List<string> _manual = new();   // salvataggi caricati a mano (percorsi)
    private ListBox _list = null!;
    private ComboBox _cars = null!;
    private TextBox _money = null!;
    private int _y;

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        AutoScroll = true;

        TrySetIcon();
        BuildUi();
        ApplyLanguage();
        RefreshProfiles();
    }

    private void TrySetIcon()
    {
        try
        {
            string loose = Path.Combine(AppContext.BaseDirectory, "icona.ico");
            if (File.Exists(loose)) { Icon = new Icon(loose); return; }
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;  // icona incorporata nell'exe
        }
        catch { /* icona opzionale */ }
    }

    // ───────────────────────── UI ─────────────────────────

    private void BuildUi()
    {
        // Header
        var header = new BufferedPanel { Left = 0, Top = 0, Width = W, Height = HeaderH, BackColor = Theme.Bg };
        header.Paint += PaintHeader;
        Controls.Add(header);

        int lx = W - 82;
        foreach (var code in new[] { "IT", "EN" })
        {
            var b = Theme.MakeButton(code, Theme.TxtGrey, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 9);
            b.SetBounds(lx, 12, 34, 24);
            b.Click += (_, _) => { Strings.Current = code; ApplyLanguage(); };
            header.Controls.Add(b);
            lx += 36;
        }

        var credits = Theme.MakeButton("", Theme.Gold, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.Gold, 9);
        credits.SetBounds(22, 12, 86, 24);
        credits.Click += (_, _) => ThemedDialog.Show(this, Strings.Get("t_credits"), Strings.Get("credits_text"), DialogKind.Info);
        _i18n.Add((credits, "credits_btn", true));
        header.Controls.Add(credits);

        _y = HeaderH + 2;

        // Profili
        AddSection("profiles");
        var listHost = new Panel { Left = Mx, Top = _y, Width = Cw, Height = 124, BackColor = Theme.Border, Padding = new Padding(2) };
        _list = new ListBox
        {
            Dock = DockStyle.Fill, BackColor = Theme.PanelBg, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22,
        };
        _list.DrawItem += (_, e) => DrawListItem(e.Index >= 0 ? _list.Items[e.Index] : null, e);
        listHost.Controls.Add(_list);
        Controls.Add(listHost);
        _y += 128;
        AddButton("load_btn", LoadManual, height: 30, fontSize: 10);

        // Auto
        AddSection("select_car");
        _cars = new ComboBox
        {
            Left = Mx, Top = _y, Width = Cw, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
            BackColor = Theme.PanelBg, ForeColor = Theme.TxtWhite, Font = Theme.Impact(11),
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24, MaxDropDownItems = 10,
        };
        foreach (var (name, _) in GameData.Cars) _cars.Items.Add(name);
        if (_cars.Items.Count > 0) _cars.SelectedIndex = 0;
        _cars.DrawItem += (_, e) => DrawListItem(e.Index >= 0 ? _cars.Items[e.Index] : null, e);
        Controls.Add(_cars);
        _y += 34;

        AddButton("inject_btn", ApplyCar, primary: true, height: 44, fontSize: 13);
        _y += 8;

        // Car patch
        AddSeparator();
        AddSection("CAR PATCH", literal: true);
        AddButtonRow(("extract_car_btn", ExtractCarPatch), ("apply_patch_btn", ApplyCarPatch));
        AddSeparator();

        // Cheats
        AddSection("CHEATS", literal: true);
        AddButton("unlock_parts", ApplyPartsUnlock);

        var moneyRow = new Panel { Left = Mx, Top = _y + 4, Width = Cw, Height = 44, BackColor = Theme.Bg };
        var lblMoney = new Label { Text = "", ForeColor = Theme.TxtGrey, Font = Theme.Impact(12), AutoSize = false, Left = 0, Top = 0, Width = 90, Height = 44, TextAlign = ContentAlignment.MiddleLeft };
        _i18n.Add((lblMoney, "money", false));
        var moneyBorder = new Panel { Left = 92, Top = 0, Width = Cw - 92, Height = 44, BackColor = Theme.Gold, Padding = new Padding(1) };
        var moneyInner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.PanelBg, Padding = new Padding(8, 8, 8, 0) };
        _money = new TextBox
        {
            Text = "999999", Dock = DockStyle.Fill, BackColor = Theme.PanelBg, ForeColor = Theme.Gold,
            Font = Theme.Impact(16), TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.None, MaxLength = 10,
        };
        moneyInner.Controls.Add(_money);
        moneyBorder.Controls.Add(moneyInner);
        moneyRow.Controls.Add(lblMoney);
        moneyRow.Controls.Add(moneyBorder);
        Controls.Add(moneyRow);
        _y += 52;

        AddButton("patch_btn", ApplyMoney, accent: Theme.Gold);
        AddSeparator();

        // Tools
        AddSection("TOOLS", literal: true);
        AddButtonRow(("rename_btn", ApplyRename), ("backup_btn", ApplyBackup));
        AddSeparator();

        // Garage editor (funzioni da NFSU2-Savegame-Editor di hackch)
        AddSection("hk_section", tag: "hackch");
        AddButtonRow(("hk_garage_btn", OpenGarage), ("hk_zip_btn", OpenZipBackup));

        // Footer
        var footer = new Label
        {
            Left = 0, Width = W, Height = 32, Top = _y + 8, ForeColor = Theme.TxtGrey, Font = Theme.Mono(8),
            TextAlign = ContentAlignment.MiddleCenter, BackColor = Theme.Bg,
        };
        footer.Paint += (_, e) =>
        {
            using var p1 = new Pen(Theme.Border); using var p2 = new Pen(Theme.BorderLt);
            e.Graphics.DrawLine(p1, 0, 0, W, 0); e.Graphics.DrawLine(p2, 0, 1, W, 1);
        };
        _i18n.Add((footer, "footer", false));
        Controls.Add(footer);

        int wanted = footer.Bottom;
        int maxH = Screen.PrimaryScreen is { } scr ? scr.WorkingArea.Height - 80 : wanted;
        bool scroll = wanted > maxH;
        ClientSize = new Size(W + (scroll ? SystemInformation.VerticalScrollBarWidth : 0), Math.Min(wanted, maxH));
    }

    private void PaintHeader(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var bg = new SolidBrush(Theme.PanelBg)) g.FillRectangle(bg, 15, 9, W - 30, HeaderH - 16);
        using (var pb = new Pen(Theme.Border)) g.DrawRectangle(pb, 14, 8, W - 28, HeaderH - 14);
        using (var pl = new Pen(Theme.BorderLt)) { g.DrawLine(pl, 14, 8, W - 14, 8); g.DrawLine(pl, 14, 8, 14, HeaderH - 6); }

        DrawCentered(g, "NEED FOR SPEED", Theme.Impact(14), Theme.TxtGrey, 32);
        DrawCentered(g, "UNDERGROUND 2", Theme.Impact(34), Theme.TxtWhite, 60);
        using (var b1 = new SolidBrush(Theme.SelGreen)) g.FillRectangle(b1, 60, 75, W - 120, 2);
        using (var b2 = new SolidBrush(Theme.SelRing)) g.FillRectangle(b2, 60, 78, W - 120, 1);
        DrawCentered(g, "PROFILES  MASTER", Theme.Impact(13), Theme.SelGreen, 92);
        TextRenderer.DrawText(g, "by GabriLex", Theme.Mono(7), new Rectangle(0, HeaderH - 22, W - 20, 14),
            Theme.TxtGrey, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Color color, int cy)
    {
        var rect = new Rectangle(0, cy - 30, W, 60);
        TextRenderer.DrawText(g, text, font, rect, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static void DrawListItem(object? item, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg))
            e.Graphics.FillRectangle(b, e.Bounds);
        var r = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, item?.ToString() ?? "", e.Font, r, sel ? Theme.SelGreen : Theme.TxtWhite,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private void AddSection(string keyOrText, bool literal = false, string? tag = null)
    {
        var pnl = new Panel { Left = Mx, Top = _y + 6, Width = Cw, Height = 24, BackColor = Theme.Bg };
        var lbl = new Label
        {
            Left = 12, Top = 0, Height = 24, AutoSize = true, ForeColor = Theme.TxtWhite, Font = Theme.Impact(13),
            TextAlign = ContentAlignment.MiddleLeft, Text = literal ? keyOrText : "",
        };
        if (!literal) _i18n.Add((lbl, keyOrText, true));
        pnl.Controls.Add(lbl);
        int tagWidth = tag == null ? 0 : TextRenderer.MeasureText(tag, Theme.Mono(9)).Width;
        pnl.Paint += (_, e) =>
        {
            using (var bar = new SolidBrush(Theme.SelGreen)) e.Graphics.FillRectangle(bar, 0, 3, 4, 18);
            using var pen = new Pen(Theme.LineSep);
            e.Graphics.DrawLine(pen, lbl.Right + 8, 12, Cw - (tag == null ? 0 : tagWidth + 10), 12);
            if (tag != null)
                TextRenderer.DrawText(e.Graphics, tag, Theme.Mono(9), new Rectangle(Cw - tagWidth - 4, 0, tagWidth + 4, 24),
                    Theme.Gold, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };
        lbl.TextChanged += (_, _) => pnl.Invalidate();
        _sections.Add(pnl);
        Controls.Add(pnl);
        _y += 30;
    }

    private void AddSeparator()
    {
        Controls.Add(new Panel { Left = Mx, Top = _y + 6, Width = Cw, Height = 1, BackColor = Theme.Border });
        Controls.Add(new Panel { Left = Mx, Top = _y + 8, Width = Cw, Height = 1, BackColor = Theme.LineSep });
        _y += 10;
    }

    private Button AddButton(string key, Action onClick, bool primary = false, Color? accent = null, int height = 38, float fontSize = 12)
    {
        Button b = primary
            ? Theme.MakeButton("", Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, fontSize)
            : Theme.MakeButton("", accent ?? Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, fontSize);
        b.SetBounds(Mx, _y + 2, Cw, height);
        b.Click += (_, _) => onClick();
        _i18n.Add((b, key, true));
        Controls.Add(b);
        _y += height + 4;
        return b;
    }

    private void AddButtonRow((string Key, Action Click) left, (string Key, Action Click) right)
    {
        int w = (Cw - 6) / 2;
        int i = 0;
        foreach (var (key, click) in new[] { left, right })
        {
            var b = Theme.MakeButton("", Theme.TxtWhite, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
            b.SetBounds(Mx + i * (w + 6), _y + 3, w, 46);
            b.MouseEnter += (_, _) => b.ForeColor = Theme.SelGreen;
            b.MouseLeave += (_, _) => b.ForeColor = Theme.TxtWhite;
            var c = click;
            b.Click += (_, _) => c();
            _i18n.Add((b, key, true));
            Controls.Add(b);
            i++;
        }
        _y += 54;
    }

    private void ApplyLanguage()
    {
        Text = Strings.Get("title");
        foreach (var (ctl, key, upper) in _i18n)
        {
            string t = Strings.Get(key);
            ctl.Text = upper ? t.ToUpperInvariant() : t;
        }
        foreach (var s in _sections) s.Invalidate();
    }

    // ───────────────────────── Logica ─────────────────────────

    private void RefreshProfiles(string? select = null)
    {
        _saves = SaveService.ScanProfiles();
        foreach (var path in _manual.ToList())
        {
            if (!File.Exists(path)) { _manual.Remove(path); continue; }
            if (_saves.Values.Any(v => string.Equals(v, path, StringComparison.OrdinalIgnoreCase))) continue;
            string name = Path.GetFileName(path), unique = name;
            for (int i = 2; _saves.ContainsKey(unique); i++) unique = $"{name} ({i})";
            _saves[unique] = path;
        }
        _list.Items.Clear();
        foreach (var name in _saves.Keys) _list.Items.Add(name);
        if (select != null && _list.Items.Contains(select)) _list.SelectedItem = select;
    }

    private void LoadManual()
    {
        using var dlg = new OpenFileDialog
        {
            Title = Strings.Get("dlg_open_save"),
            Filter = "*.*|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string path = Path.GetFullPath(dlg.FileName);
        if (!SaveService.LooksLikeSave(path))
        {
            Info("t_error", Strings.Get("err_not_save"), DialogKind.Error);
            return;
        }
        if (!_manual.Contains(path, StringComparer.OrdinalIgnoreCase)) _manual.Add(path);
        RefreshProfiles();
        string? key = _saves.FirstOrDefault(kv => string.Equals(kv.Value, path, StringComparison.OrdinalIgnoreCase)).Key;
        if (key != null) _list.SelectedItem = key;
    }

    private void Info(string titleKey, string msg, DialogKind kind = DialogKind.Info) =>
        ThemedDialog.Show(this, Strings.Get(titleKey), msg, kind);

    private (string Name, string Path)? Selected()
    {
        if (_list.SelectedItem is string name && _saves.TryGetValue(name, out var path))
            return (name, path);
        Info("t_warn", Strings.Get("err_select"), DialogKind.Warn);
        return null;
    }

    /// <summary>Esegue un'azione sul profilo selezionato gestendo gli errori in modo uniforme.</summary>
    private void Run(string? successKey, Action<string, string> action)
    {
        var sel = Selected();
        if (sel is null) return;
        try
        {
            action(sel.Value.Path, sel.Value.Name);
            if (successKey != null) Info("t_done", Strings.Get(successKey));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Info("t_error", Strings.Get("err_admin"), DialogKind.Error);
        }
        catch (Exception ex)
        {
            Info("t_error", ex.Message, DialogKind.Error);
        }
    }

    private void ApplyCar()
    {
        if (_cars.SelectedItem is not string car) return;
        Run("success_inject", (path, _) => SaveService.InjectCar(path, car));
    }

    private void ApplyPartsUnlock() =>
        Run("success_parts", (path, _) => SaveService.UnlockParts(path));

    private void ApplyMoney()
    {
        if (_list.SelectedItem == null) { Selected(); return; }
        if (!uint.TryParse(_money.Text.Trim(), out uint amount) || amount > int.MaxValue)
        {
            Info("t_error", Strings.Get("err_money"), DialogKind.Error);
            return;
        }
        Run("success_money", (path, _) => SaveService.SetMoney(path, amount));
    }

    private void ApplyRename()
    {
        var sel = Selected();
        if (sel is null) return;
        string? input = ThemedDialog.Prompt(this, Strings.Get("t_rename"), Strings.Get("rename_prompt"));
        if (string.IsNullOrWhiteSpace(input)) return;
        string newName = input.Trim();
        if (newName.Length > 7) newName = newName[..7];
        if (!SaveService.IsValidProfileName(newName))
        {
            Info("t_error", Strings.Get("err_name"), DialogKind.Error);
            return;
        }
        if (!newName.Equals(sel.Value.Name, StringComparison.OrdinalIgnoreCase) &&
            _saves.Keys.Any(k => k.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            Info("t_error", Strings.Get("err_exists"), DialogKind.Error);
            return;
        }
        try
        {
            string oldPath = sel.Value.Path;
            string newPath = SaveService.RenameProfile(oldPath, newName);
            int mi = _manual.FindIndex(p => string.Equals(p, oldPath, StringComparison.OrdinalIgnoreCase));
            if (mi >= 0) _manual[mi] = newPath;
            RefreshProfiles(newName);
            Info("t_done", Strings.Get("success_rename"));
        }
        catch
        {
            Info("t_error", Strings.Get("err_admin"), DialogKind.Error);
        }
    }

    private void ApplyBackup() =>
        Run(null, (path, name) =>
        {
            SaveService.BackupToDesktop(path, name);
            Info("t_backup", Strings.Get("backup_done"));
        });

    private void ExtractCarPatch()
    {
        var sel = Selected();
        if (sel is null) return;
        string? carName = ThemedDialog.Prompt(this, Strings.Get("t_extract"), Strings.Get("extract_name_prompt"));
        if (string.IsNullOrWhiteSpace(carName)) return;
        carName = carName.Trim();

        using var dlg = new SaveFileDialog
        {
            DefaultExt = "json",
            Filter = "JSON Patch (*.json)|*.json",
            FileName = carName.Replace(' ', '_') + "_patch.json",
            Title = Strings.Get("dlg_save_patch"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            SaveService.ExtractCar(sel.Value.Path, carName, sel.Value.Name, dlg.FileName);
            Info("t_done", Strings.Get("success_extract"));
        }
        catch (Exception ex)
        {
            Info("t_error", ex.Message, DialogKind.Error);
        }
    }

    private void ApplyCarPatch()
    {
        var sel = Selected();
        if (sel is null) return;

        using var dlg = new OpenFileDialog
        {
            Filter = "JSON Patch (*.json)|*.json",
            Title = Strings.Get("dlg_open_patch"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var patch = SaveService.LoadPatch(dlg.FileName);
            string msg = string.Format(Strings.Get("confirm_patch"),
                string.IsNullOrEmpty(patch.CarName) ? "?" : patch.CarName, sel.Value.Name);
            if (!ThemedDialog.Confirm(this, Strings.Get("t_confirm"), msg)) return;

            SaveService.ApplyPatch(sel.Value.Path, patch);
            Info("t_done", Strings.Get("success_patch"));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            Info("t_error", Strings.Get("err_patch_invalid"), DialogKind.Error);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Info("t_error", Strings.Get("err_admin"), DialogKind.Error);
        }
        catch (Exception ex)
        {
            Info("t_error", ex.Message, DialogKind.Error);
        }
    }

    // ───────── Garage editor / backup ZIP (hackch) ─────────

    private void OpenGarage()
    {
        var sel = Selected();
        if (sel is null) return;
        try
        {
            var doc = SaveDocument.OpenFile(sel.Value.Path);
            using var f = new GarageForm(doc);
            f.ShowDialog(this);
            RefreshProfiles(sel.Value.Name);
        }
        catch (InvalidDataException)
        {
            Info("t_error", Strings.Get("err_not_20cm"), DialogKind.Error);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Info("t_error", Strings.Get("err_admin"), DialogKind.Error);
        }
    }

    private void OpenZipBackup()
    {
        using var dlg = new OpenFileDialog
        {
            Title = Strings.Get("dlg_open_zip"),
            Filter = "ZIP (*.zip)|*.zip|*.*|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (!SaveDocument.LooksLikeZip(dlg.FileName))
            {
                Info("t_error", Strings.Get("err_zip_none"), DialogKind.Error);
                return;
            }
            var profiles = SaveDocument.ReadBackupProfiles(dlg.FileName);
            if (profiles.Count == 0)
            {
                Info("t_error", Strings.Get("err_zip_none"), DialogKind.Error);
                return;
            }
            int pick = 0;
            if (profiles.Count > 1)
            {
                pick = PickDialog.Choose(this, Strings.Get("hk_zip_btn").TrimEnd('.'), Strings.Get("zip_pick"),
                    profiles.Select(p => p.GarageState == GarageState.Loaded ? p.Name + "  (+GARAGE.DAT)" : p.Name).ToList());
                if (pick < 0) return;
            }
            var doc = SaveDocument.OpenBackup(dlg.FileName, profiles[pick]);
            using var f = new GarageForm(doc);
            f.ShowDialog(this);
        }
        catch (Exception ex) when (ex is InvalidDataException)
        {
            Info("t_error", Strings.Get("err_zip_none"), DialogKind.Error);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Info("t_error", Strings.Get("err_admin"), DialogKind.Error);
        }
    }
}
