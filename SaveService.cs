using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2ProfileMaster;

/// <summary>Formato del file patch .json (compatibile con la versione Python).</summary>
public sealed class CarPatch
{
    [JsonPropertyName("car_name")] public string CarName { get; set; } = "";
    [JsonPropertyName("source_profile")] public string SourceProfile { get; set; } = "";
    [JsonPropertyName("blocks")] public List<PatchBlock> Blocks { get; set; } = new();
}

public sealed class PatchBlock
{
    [JsonPropertyName("off")] public int Off { get; set; }
    [JsonPropertyName("hex")] public string Hex { get; set; } = "";
}

/// <summary>Tutta la logica sui file di salvataggio, senza UI.</summary>
public static class SaveService
{
    private const int MoneyOffset = 41322;      // 0xA16A
    private const int NameOffset = 0xD220 + 5;
    private const int NameLength = 7;

    // Regioni usate dall'estrazione di un'auto dal salvataggio
    private static readonly (int Off, int Size)[] CarRegions =
    {
        (0x5870, 0xC8), (0x5950, 0x40), (0x5B20, 0x10),
        (0xAD80, 0x20), (0xC2D0, 0x10), (0xC3B0, 0x36),
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static byte[] HexToBytes(string hex) =>
        Convert.FromHexString(hex.Replace(" ", "").Replace("\r", "").Replace("\n", ""));

    /// <summary>Cerca i profili. Chiave = nome profilo, valore = percorso del file di salvataggio.</summary>
    public static Dictionary<string, string> ScanProfiles()
    {
        var found = new Dictionary<string, string>();
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        var roots = new List<string>
        {
            Path.Combine(local, "VirtualStore"),
            local,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        try
        {
            foreach (var d in DriveInfo.GetDrives())
                if (d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    roots.Add(d.RootDirectory.FullName);
        }
        catch { /* ignora */ }

        string[] targets = { "NFS Underground 2", "Need for Speed Underground 2" };
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            foreach (var target in targets)
            {
                string baseDir = Path.Combine(root, target);
                if (!Directory.Exists(baseDir)) continue;
                try
                {
                    foreach (var dir in Directory.GetDirectories(baseDir))
                    {
                        string name = Path.GetFileName(dir);
                        string file = Path.Combine(dir, name);
                        if (File.Exists(file) && !found.ContainsKey(name))
                            found[name] = file;
                    }
                }
                catch { /* cartella non accessibile */ }
            }
        }
        return found;
    }

    /// <summary>Scrive i blocchi controllando che restino dentro il file (non lo allunga mai).</summary>
    public static void WriteBlocks(string path, IEnumerable<(int Off, byte[] Data)> blocks)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var list = blocks.ToList();
        foreach (var (off, data) in list)
            if (off < 0 || (long)off + data.Length > fs.Length)
                throw new InvalidDataException($"Offset 0x{off:X} fuori dal file.");
        foreach (var (off, data) in list)
        {
            fs.Seek(off, SeekOrigin.Begin);
            fs.Write(data, 0, data.Length);
        }
        FixChecksums(fs);
    }

    /// <summary>Se il file e' un salvataggio 20CM valido, ricalcola i checksum dopo la scrittura (come l'editor di hackch).</summary>
    private static void FixChecksums(FileStream fs)
    {
        if (fs.Length > 10_000_000) return;
        var buf = new byte[fs.Length];
        fs.Seek(0, SeekOrigin.Begin);
        fs.ReadExactly(buf, 0, buf.Length);
        if (CareerSave.Check(buf) != null) return;       // non e' un 20CM: non tocco nulla
        CareerSave.RecalculateChecksums(buf);
        fs.Seek(0, SeekOrigin.Begin);
        fs.Write(buf, 0, CareerLayout.HeaderSize);       // i checksum stanno nell'header
    }

    public static void WriteBlocks(string path, IEnumerable<Block> blocks) =>
        WriteBlocks(path, blocks.Select(b => (b.Offset, HexToBytes(b.Hex))));

    public static void InjectCar(string path, string carName)
    {
        var car = GameData.Cars.First(c => c.Name == carName);
        WriteBlocks(path, car.Blocks);
    }

    public static void UnlockParts(string path) => WriteBlocks(path, GameData.Parts);

    public static void SetMoney(string path, uint amount) =>
        WriteBlocks(path, new[] { (MoneyOffset, BitConverter.GetBytes(amount)) });

    public static void ExtractCar(string savePath, string carName, string profile, string jsonPath)
    {
        var patch = new CarPatch { CarName = carName, SourceProfile = profile };
        using (var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            foreach (var (off, size) in CarRegions)
            {
                var buf = new byte[size];
                fs.Seek(off, SeekOrigin.Begin);
                fs.ReadExactly(buf, 0, size);
                patch.Blocks.Add(new PatchBlock { Off = off, Hex = SpaceHex(Convert.ToHexString(buf)) });
            }
        }
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(patch, JsonOpts), new UTF8Encoding(false));
    }

    private static string SpaceHex(string h)
    {
        var sb = new StringBuilder(h.Length + h.Length / 2);
        for (int i = 0; i < h.Length; i += 2)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(h, i, 2);
        }
        return sb.ToString();
    }

    public static CarPatch LoadPatch(string jsonPath)
    {
        var patch = JsonSerializer.Deserialize<CarPatch>(File.ReadAllText(jsonPath))
                    ?? throw new InvalidDataException("Patch vuota.");
        if (patch.Blocks.Count == 0) throw new InvalidDataException("Nessun blocco.");
        return patch;
    }

    public static void ApplyPatch(string savePath, CarPatch patch) =>
        WriteBlocks(savePath, patch.Blocks.Select(b => (b.Off, HexToBytes(b.Hex))));

    public static bool IsValidProfileName(string name) =>
        name.Length is >= 1 and <= NameLength
        && name.All(c => c is >= ' ' and <= '~')
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Dimensione minima per considerare un file un salvataggio (ultimo byte del nome profilo).</summary>
    public const int MinSaveSize = NameOffset + NameLength;

    public static bool LooksLikeSave(string path) =>
        File.Exists(path) && new FileInfo(path).Length >= MinSaveSize;

    /// <summary>Rinomina il nome interno + file (+ cartella se ha lo stesso nome del file). Ritorna il nuovo percorso del file.</summary>
    public static string RenameProfile(string savePath, string newName)
    {
        var nameBytes = new byte[NameLength];
        Encoding.ASCII.GetBytes(newName).CopyTo(nameBytes, 0);
        WriteBlocks(savePath, new[] { (NameOffset, nameBytes) });

        string oldDir = Path.GetDirectoryName(savePath)!;
        string oldName = Path.GetFileName(savePath);
        string newFile = Path.Combine(oldDir, newName);
        File.Move(savePath, newFile);

        // Cartella del profilo: la rinomino solo se e' quella standard (stesso nome del file)
        if (string.Equals(Path.GetFileName(oldDir), oldName, StringComparison.OrdinalIgnoreCase))
        {
            string parent = Path.GetDirectoryName(oldDir)!;
            string newDir = Path.Combine(parent, newName);
            Directory.Move(oldDir, newDir);
            return Path.Combine(newDir, newName);
        }
        return newFile;
    }

    public static string BackupToDesktop(string savePath, string profile)
    {
        string dst = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NFSU2_Backups");
        Directory.CreateDirectory(dst);
        string file = Path.Combine(dst, Path.GetFileName(savePath) + $"_{profile}.bak");
        File.Copy(savePath, file, overwrite: true);
        return file;
    }
}
