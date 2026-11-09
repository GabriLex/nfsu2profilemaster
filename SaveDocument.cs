// Documento di salvataggio in memoria (file 20CM grezzo oppure profilo di un backup ZIP nfsu2-web con GARAGE.DAT).
// Il formato GARAGE.DAT e la logica di abbinamento seguono NFSU2-Savegame-Editor di hackch (MIT) / nfsu2-web.
using System.IO.Compression;

namespace NFSU2ProfileMaster;

public enum GarageState { Loaded, Missing, Stale, Invalid }

public sealed record BackupProfile(string Name, string Folder, string CareerEntry, string? GarageEntry, GarageState GarageState, string Note);

public sealed class SaveDocument
{
    private const string GarageFile = "GARAGE.DAT";
    private const int GarageHeaderSize = 32;
    private const int GarageSize = GarageHeaderSize + CareerLayout.SlotCount * CareerLayout.SlotStride;
    private const int PayloadOff = 36;
    private const int CarsPartSize = 39230;
    private static readonly byte[] GarageMagic = { 0x4E, 0x32, 0x47, 0x41 };   // "N2GA"

    private readonly string _path;                 // file raw oppure zip
    private readonly BackupProfile? _profile;      // null = file raw
    private byte[]? _originalGarage;
    private bool _backedUp;

    public string Title { get; }
    public byte[] Data { get; private set; }       // buffer di lavoro (54966 o esteso a 10 slot)
    public int SlotCount => CareerLayout.SlotsIn(Data);
    public bool IsBackup => _profile != null;
    public string Note { get; private set; } = "";

    private SaveDocument(string path, BackupProfile? profile, byte[] data, byte[]? garage, string title)
    {
        _path = path; _profile = profile; Data = data; _originalGarage = garage; Title = title;
    }

    // ───── apertura ─────

    public static bool LooksLikeZip(string path)
    {
        using var fs = File.OpenRead(path);
        var h = new byte[4];
        return fs.Read(h, 0, 4) == 4 && h[0] == 0x50 && h[1] == 0x4B && ((h[2] == 3 && h[3] == 4) || (h[2] == 5 && h[3] == 6));
    }

    public static SaveDocument OpenFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (CareerSave.Check(bytes) is { } err) throw new InvalidDataException(err);
        return new SaveDocument(path, null, bytes, null, path);
    }

    public static List<BackupProfile> ReadBackupProfiles(string zipPath)
    {
        var result = new List<BackupProfile>();
        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.ToDictionary(e => e.FullName.ToLowerInvariant(), e => e);
        foreach (var e in zip.Entries)
        {
            var parts = e.FullName.Split('/');
            if (parts.Length < 2 || e.Length == 0 || e.Length > 4_000_000) continue;
            string name = parts[^1], dir = parts[^2];
            if (name.Length == 0 || name != dir) continue;
            var career = ReadEntry(e);
            if (CareerSave.Check(career) != null) continue;
            string folder = string.Join('/', parts[..^1]);
            string garageName = $"{folder}/{GarageFile}";
            if (!names.TryGetValue(garageName.ToLowerInvariant(), out var g))
            {
                result.Add(new BackupProfile(name, folder, e.FullName, null, GarageState.Missing, "No GARAGE.DAT: slots 1-5 only."));
                continue;
            }
            var (state, note) = CheckGarage(ReadEntry(g), career);
            result.Add(new BackupProfile(name, folder, e.FullName, g.FullName, state, note));
        }
        return result;
    }

    public static SaveDocument OpenBackup(string zipPath, BackupProfile profile)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var career = ReadEntry(zip.GetEntry(profile.CareerEntry) ?? throw new InvalidDataException("Profile missing."));
        byte[]? garage = null;
        byte[] working = career;
        if (profile.GarageEntry != null)
        {
            garage = ReadEntry(zip.GetEntry(profile.GarageEntry) ?? throw new InvalidDataException("GARAGE.DAT missing."));
            if (CheckGarage(garage, career).State == GarageState.Loaded)
            {
                working = new byte[CareerLayout.ExtendedSaveSize];
                Buffer.BlockCopy(career, 0, working, 0, career.Length);
                Buffer.BlockCopy(garage, GarageHeaderSize, working, CareerLayout.ExtraSlotRecordBase, CareerLayout.SlotCount * CareerLayout.SlotStride);
            }
            else garage = null;   // non valido/obsoleto: lasciato com'e'
        }
        return new SaveDocument(zipPath, profile, working, garage, $"{Path.GetFileName(zipPath)} / {profile.Name}") { Note = profile.Note };
    }

    private static byte[] ReadEntry(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    // ───── GARAGE.DAT ─────

    private static ulong Fingerprint(byte[] career)
    {
        if (career.Length < PayloadOff + CarsPartSize) throw new InvalidDataException("Career save too small for a GARAGE.DAT fingerprint.");
        ulong h = 0xCBF29CE484222325UL;
        unchecked
        {
            for (int i = PayloadOff; i < PayloadOff + CarsPartSize; i++) { h ^= career[i]; h *= 0x100000001B3UL; }
        }
        return h;
    }

    private static (GarageState State, string Note) CheckGarage(byte[] garage, byte[] career)
    {
        if (career.Length != CareerLayout.ProfileSize)
            return (GarageState.Invalid, "Career saves with GARAGE.DAT must be 54966 bytes.");
        if (garage.Length != GarageSize || !garage.Take(4).SequenceEqual(GarageMagic) || BitConverter.ToUInt32(garage, 4) != 1)
            return (GarageState.Invalid, "GARAGE.DAT is not an nfsu2-web version 1 garage; slots 6-10 left unchanged.");
        if (BitConverter.ToUInt64(garage, 8) != Fingerprint(career))
            return (GarageState.Stale, "GARAGE.DAT belongs to another version of this profile; slots 6-10 left unchanged.");
        return (GarageState.Loaded, "nfsu2-web GARAGE.DAT loaded: slots 6-10 available.");
    }

    // ───── salvataggio ─────

    public ChecksumStatus Checksums => CareerSave.Verify(Data);

    /// <summary>Salva con checksum ricalcolati. Al primo salvataggio crea un backup sul Desktop.</summary>
    public string? Save()
    {
        string? backupPath = null;
        if (!_backedUp)
        {
            backupPath = BackupOriginal();
            _backedUp = true;
        }

        CareerSave.RecalculateChecksums(Data);   // solo header + blocchi A/B nella parte carriera

        if (_profile == null)
        {
            File.WriteAllBytes(_path, Data);
            return backupPath;
        }

        var career = Data.AsSpan(0, CareerLayout.ProfileSize).ToArray();
        string temp = _path + ".tmp";
        File.Copy(_path, temp, overwrite: true);
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Update))
            {
                Replace(zip, _profile.CareerEntry, career);
                if (_originalGarage != null && _profile.GarageEntry != null && Data.Length == CareerLayout.ExtendedSaveSize)
                {
                    var garage = (byte[])_originalGarage.Clone();
                    Buffer.BlockCopy(Data, CareerLayout.ExtraSlotRecordBase, garage, GarageHeaderSize, CareerLayout.SlotCount * CareerLayout.SlotStride);
                    BitConverter.TryWriteBytes(garage.AsSpan(8, 8), Fingerprint(career));
                    Replace(zip, _profile.GarageEntry, garage);
                    _originalGarage = garage;
                }
            }
            File.Move(temp, _path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backupPath;
    }

    private static void Replace(ZipArchive zip, string entryName, byte[] data)
    {
        var e = zip.GetEntry(entryName) ?? throw new InvalidDataException(entryName);
        var stamp = e.LastWriteTime;
        using (var s = e.Open()) { s.SetLength(0); s.Write(data, 0, data.Length); }
        e.LastWriteTime = stamp;
    }

    private string BackupOriginal()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NFSU2_Backups");
        Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string target = Path.Combine(dir, $"{Path.GetFileName(_path)}_{stamp}.bak");
        File.Copy(_path, target, overwrite: true);
        return target;
    }
}
