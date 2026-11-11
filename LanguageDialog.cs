namespace NFSU2ProfileMaster;

/// <summary>Finestra iniziale: chiede la lingua a ogni avvio.</summary>
public sealed class LanguageDialog : Form
{
    private string _choice = "IT";

    private LanguageDialog()
    {
        Text = "NFSU2 Profiles Master";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ClientSize = new Size(420, 236);
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; } catch { /* icona opzionale */ }

        Controls.Add(Centered("NFSU2 PROFILES MASTER", Theme.Impact(20), Theme.SelGreen, 16, 40));
        Controls.Add(Centered("LINGUA  /  LANGUAGE", Theme.Impact(13), Theme.TxtWhite, 62, 26));
        Controls.Add(Centered("Scegli la lingua  ·  Choose your language", Theme.Mono(9), Theme.TxtGrey, 92, 22));

        var it = Theme.MakeButton("ITALIANO", Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 17);
        it.SetBounds(30, 130, 170, 62);
        it.Click += (_, _) => Pick("IT");
        var en = Theme.MakeButton("ENGLISH", Theme.SelGreen, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 17);
        en.SetBounds(220, 130, 170, 62);
        en.Click += (_, _) => Pick("EN");
        Controls.Add(it);
        Controls.Add(en);

        Controls.Add(Centered("I = Italiano     E = English", Theme.Mono(8), Theme.TxtGrey, 206, 18));

        Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.I) Pick("IT");
            else if (e.KeyCode == Keys.E) Pick("EN");
            else if (e.KeyCode == Keys.Escape) Close();     // chiusa senza scegliere: resta l'italiano
        };
    }

    private static Label Centered(string text, Font font, Color color, int top, int height) => new()
    {
        Text = text, Font = font, ForeColor = color, BackColor = Theme.Bg, AutoSize = false,
        Left = 0, Top = top, Width = 420, Height = height, TextAlign = ContentAlignment.MiddleCenter,
    };

    private void Pick(string code)
    {
        _choice = code;
        Close();
    }

    /// <summary>Mostra la scelta e ritorna "IT" oppure "EN".</summary>
    public static string Ask()
    {
        using var d = new LanguageDialog();
        d.ShowDialog();
        return d._choice;
    }
}
