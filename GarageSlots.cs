// Gestione degli slot garage: esporta/importa (.u2cc/.u2ci), rimuovi auto, sposta/scambia, nomi XNAME.
// Funzioni e formati file basati su MemphisRider di AJ_Lethal (MIT, https://github.com/AJLethal/MemphisRider_NFSU2).
// Gli offset di MemphisRider sono confermati sui byte reali di un salvataggio; i record sono gestiti come in hackch (ID dello slot lasciato al suo posto).
using System.Text;
using System.Text.Json;

namespace NFSU2ProfileMaster;

public enum SlotKind { Career, MyCars }

public static class GarageSlots
{
    public const int MyCarsStart = 1196, MyCarsStride = 1072, MyCarsCount = 20;
    public const int HeadSize = 1072, InventorySize = 962;          // dimensioni dei file .u2cc e .u2ci
    public const int CareerPointer = 44415, MyCarsPointer = 50639;   // ID dello slot mostrato nel menu principale

    private const int OffEnable = 8, OffMark = 12, OffHash = 24, OffFillStart = 856, OffFillEnd = 1061, OffLock = 1068, OffBought = 1069;

    public static int Count(byte[] d, SlotKind k) => k == SlotKind.MyCars ? MyCarsCount : CareerLayout.SlotsIn(d);

    /// <summary>Inizio del record: 4 byte di ID dello slot, poi i dati dell'auto.</summary>
    public static int RecordStart(byte[] d, SlotKind k, int i)
    {
        if (i < 0 || i >= Count(d, k)) throw new ArgumentOutOfRangeException(nameof(i));
        return k == SlotKind.MyCars ? MyCarsStart + i * MyCarsStride : CareerLayout.SlotBaseOf(i) - 4;
    }

    /// <summary>Byte di dati copiati dopo l'ID (career: 2026 come lo scambio slot di hackch).</summary>
    private static int DataLen(SlotKind k) => k == SlotKind.MyCars ? HeadSize - 4 : CareerLayout.SlotRecordEndRel;

    public static uint Hash(byte[] d, SlotKind k, int i) => BitConverter.ToUInt32(d, RecordStart(d, k, i) + OffHash);

    public static bool IsOccupied(byte[] d, SlotKind k, int i)
    {
        int rs = RecordStart(d, k, i);
        return k == SlotKind.Career ? d[rs + OffBought] == 1 : d[rs + OffEnable] == 1 && BitConverter.ToUInt32(d, rs + OffHash) != 0;
    }

    public static bool IsLocked(byte[] d, SlotKind k, int i) => k == SlotKind.Career && d[RecordStart(d, k, i) + OffLock] != 0;

    public static int OccupiedCount(byte[] d, SlotKind k) => Enumerable.Range(0, Count(d, k)).Count(i => IsOccupied(d, k, i));

    public static string CarName(uint hash) =>
        Donors.Identify(hash)?.Name ?? UserXnames.Lookup(hash) ?? "0x" + Convert.ToHexString(BitConverter.GetBytes(hash));

    // ───── esporta / importa ─────

    public static (byte[] Head, byte[]? Inventory) Export(byte[] d, SlotKind k, int i)
    {
        int rs = RecordStart(d, k, i);
        var head = new byte[HeadSize];
        Buffer.BlockCopy(d, rs, head, 0, HeadSize);
        if (k == SlotKind.MyCars) return (head, null);
        var inv = new byte[InventorySize];                              // gli ultimi 4 byte possono cadere oltre il buffer (slot 10)
        int avail = Math.Max(0, Math.Min(InventorySize, d.Length - (rs + HeadSize)));
        Buffer.BlockCopy(d, rs + HeadSize, inv, 0, avail);
        return (head, inv);
    }

    public static void Import(byte[] d, SlotKind k, int i, byte[] head, byte[]? inventory)
    {
        if (head.Length != HeadSize) throw new InvalidDataException("head size");
        if (inventory != null && inventory.Length != InventorySize) throw new InvalidDataException("inventory size");
        if (BitConverter.ToUInt32(head, OffHash) == 0) throw new InvalidDataException("empty slot");
        int rs = RecordStart(d, k, i);

        Buffer.BlockCopy(head, 4, d, rs + 4, HeadSize - 4);              // l'ID dello slot di destinazione resta com'e'
        d[rs + OffEnable] = 1;
        if (k == SlotKind.Career)
        {
            if (inventory != null) Buffer.BlockCopy(inventory, 0, d, rs + HeadSize, InventorySize - 4);
            d[rs + OffMark] = 4;
            d[rs + OffLock] = 0;                                         // l'auto importata deve essere visibile
            d[rs + OffBought] = 1;
        }
        else
        {
            d[rs + OffMark] = 2;
            EnsureMyCarsId(d, i);
        }
        UpdatePointer(d, k);
    }

    // ───── rimuovi auto ─────

    /// <summary>Svuota lo slot come lo scrive il gioco. Carriera: mai il primo slot, mai l'ultima auto rimasta.</summary>
    public static void Clear(byte[] d, SlotKind k, int i)
    {
        int rs = RecordStart(d, k, i);
        if (k == SlotKind.Career)
        {
            if (i == 0) throw new InvalidOperationException("first");
            if (OccupiedCount(d, k) <= 1) throw new InvalidOperationException("last");
            Array.Clear(d, rs + 4, DataLen(k));                          // ID invariato
            d[rs + OffEnable] = 1;
            d[rs + OffMark] = 4;
            for (int p = rs + OffFillStart; p < rs + OffFillEnd; p++) d[p] = 0x64;
        }
        else
        {
            Array.Clear(d, rs, HeadSize);                                // uno slot My Cars vuoto e' tutto a zero tranne il byte 12
            d[rs + OffMark] = 2;
        }
        UpdatePointer(d, k);
    }

    // ───── sposta / scambia ─────

    public static void Swap(byte[] d, SlotKind k, int a, int b)
    {
        if (a == b) return;
        int ra = RecordStart(d, k, a), rb = RecordStart(d, k, b), len = DataLen(k);
        byte lockA = d[ra + OffLock], lockB = d[rb + OffLock];
        uint idA = BitConverter.ToUInt32(d, ra), idB = BitConverter.ToUInt32(d, rb);

        var tmp = new byte[len];
        Buffer.BlockCopy(d, ra + 4, tmp, 0, len);
        Buffer.BlockCopy(d, rb + 4, d, ra + 4, len);
        Buffer.BlockCopy(tmp, 0, d, rb + 4, len);

        if (k == SlotKind.Career)
        {
            // il blocco dello slot appartiene alla posizione; una posizione che riceve un'auto viene sbloccata
            d[ra + OffLock] = d[ra + OffBought] == 1 ? (byte)0 : lockA;
            d[rb + OffLock] = d[rb + OffBought] == 1 ? (byte)0 : lockB;
        }
        else
        {
            FixMyCarsId(d, a, idA);
            FixMyCarsId(d, b, idB);
        }
        UpdatePointer(d, k);
    }

    // ───── ID di My Cars e puntatori del menu principale ─────

    private static byte[] AsciiId(int i) => Encoding.ASCII.GetBytes(i < 9 ? $"0{i + 1}MC" : $"{i + 1}MC");

    private static void EnsureMyCarsId(byte[] d, int i)
    {
        int rs = RecordStart(d, SlotKind.MyCars, i);
        if (BitConverter.ToUInt32(d, rs) == 0) Buffer.BlockCopy(AsciiId(i), 0, d, rs, 4);   // come MemphisRider
    }

    private static void FixMyCarsId(byte[] d, int i, uint previousId)
    {
        int rs = RecordStart(d, SlotKind.MyCars, i);
        if (!IsOccupied(d, SlotKind.MyCars, i)) { Array.Clear(d, rs, 4); return; }
        if (previousId != 0) Buffer.BlockCopy(BitConverter.GetBytes(previousId), 0, d, rs, 4);
        else Buffer.BlockCopy(AsciiId(i), 0, d, rs, 4);
    }

    /// <summary>
    /// Il menu principale ricorda l'ID di uno slot. Se punta ancora a uno slot con un'auto non si tocca;
    /// altrimenti (slot svuotato o spostato) lo si porta sul primo slot occupato, cosi' non resta mai su uno slot vuoto.
    /// </summary>
    public static void UpdatePointer(byte[] d, SlotKind k)
    {
        int n = k == SlotKind.MyCars ? MyCarsCount : Math.Min(CareerLayout.SlotCount, Count(d, k));
        int ptr = k == SlotKind.MyCars ? MyCarsPointer : CareerPointer;
        uint current = BitConverter.ToUInt32(d, ptr);
        int first = -1;
        for (int i = 0; i < n; i++)
        {
            if (!IsOccupied(d, k, i)) continue;
            int rs = RecordStart(d, k, i);
            if (first < 0) first = rs;
            if (BitConverter.ToUInt32(d, rs) == current) return;       // il puntatore e' ancora valido
        }
        if (first >= 0) Buffer.BlockCopy(d, first, d, ptr, 4);
    }
}

/// <summary>Nomi XNAME definiti dall'utente (auto add-on), salvati in %APPDATA%. Hash come in MemphisRider.</summary>
public static class UserXnames
{
    private static Dictionary<string, string>? _names;     // XNAME -> "0x" + byte dell'hash nell'ordine del file
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NFSU2ProfileMaster", "userXnames.json");

    /// <summary>Hash di una stringa: result = result * 33 + carattere, partendo da -1.</summary>
    public static uint Hash(string s)
    {
        uint r = 0xFFFFFFFF;
        unchecked { foreach (char c in s) r = r * 0x21 + c; }
        return r;
    }

    public static string HashText(uint hash) => "0x" + Convert.ToHexString(BitConverter.GetBytes(hash));

    public static bool IsValidName(string s) => s.Length is >= 1 and <= 32 && s.All(c => c > ' ' && c <= '~');

    private static Dictionary<string, string> Names
    {
        get
        {
            if (_names != null) return _names;
            try
            {
                _names = File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new()
                    : new();
            }
            catch { _names = new(); }
            return _names;
        }
    }

    public static string? Lookup(uint hash)
    {
        string wanted = HashText(hash);
        foreach (var kv in Names)
            if (string.Equals(kv.Value, wanted, StringComparison.OrdinalIgnoreCase)) return kv.Key;
        return null;
    }

    public static uint Add(string xname)
    {
        xname = xname.ToUpperInvariant();
        if (!IsValidName(xname)) throw new ArgumentException("xname");
        uint h = Hash(xname);
        Names[xname] = HashText(h);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var sorted = Names.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return h;
    }
}
