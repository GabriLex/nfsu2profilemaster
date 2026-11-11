using System.Reflection;

namespace NFSU2ProfileMaster;

/// <summary>Versione letta dall'exe (la imposta NFSU2ProfileMaster.csproj): niente numeri da ricordare nel codice.</summary>
public static class AppVersion
{
    /// <summary>1.10.0.0 -> "1.10", 1.0.9.0 -> "1.0.9", 1.2.3.4 -> "1.2.3.4".</summary>
    public static string Text
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);
            int build = Math.Max(v.Build, 0), rev = Math.Max(v.Revision, 0);
            return rev != 0 ? $"{v.Major}.{v.Minor}.{build}.{rev}"
                 : build != 0 ? $"{v.Major}.{v.Minor}.{build}"
                 : $"{v.Major}.{v.Minor}";
        }
    }
}
