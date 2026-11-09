// Salvataggio/applicazione della "customizzazione" (record completo) di piu' slot.
// Il record copiato e' lo stesso usato dallo scambio slot di hackch: dati auto dello slot, escluso il suo handle CAREER_SLOT_<n>.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFSU2ProfileMaster;

public sealed class SlotPackEntry
{
    [JsonPropertyName("slot")] public int Slot { get; set; }              // numero slot di origine, da 1
    [JsonPropertyName("car_name")] public string CarName { get; set; } = "";
    [JsonPropertyName("car_hash")] public uint CarHash { get; set; }
    [JsonPropertyName("state")] public string State { get; set; } = "";
    [JsonPropertyName("hex")] public string Hex { get; set; } = "";
}

public sealed class SlotPack
{
    [JsonPropertyName("format")] public string Format { get; set; } = SlotPackService.FormatId;
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("source_profile")] public string SourceProfile { get; set; } = "";
    [JsonPropertyName("slots")] public List<SlotPackEntry> Slots { get; set; } = new();
}

public static class SlotPackService
{
    public const string FormatId = "nfsu2-slot-pack";
    /// <summary>Lunghezza del record copiato per ogni slot (2026 byte).</summary>
    public static int RecordLength => CareerLayout.SlotRecordEndRel;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static SlotPack Extract(byte[] d, IEnumerable<int> slots, string profileName)
    {
        var pack = new SlotPack { SourceProfile = profileName };
        foreach (int s in slots.Distinct().OrderBy(x => x))
        {
            int b = CareerLayout.CheckedSlotBase(d, s);
            uint hash = CareerSave.GetCarTypeHash(d, s);
            pack.Slots.Add(new SlotPackEntry
            {
                Slot = s + 1,
                CarName = Donors.Identify(hash)?.Name ?? "",
                CarHash = hash,
                State = CareerSave.GetSlotState(d, s).ToString(),
                Hex = Convert.ToHexString(d, b, RecordLength),
            });
        }
        return pack;
    }

    public static void Save(SlotPack pack, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(pack, JsonOpts), new UTF8Encoding(false));

    /// <summary>Legge e valida un file slot. Errori di formato: InvalidDataException / JsonException.</summary>
    public static SlotPack Load(string path)
    {
        var pack = JsonSerializer.Deserialize<SlotPack>(File.ReadAllText(path))
                   ?? throw new InvalidDataException("empty");
        if (pack.Format != FormatId || pack.Version != 1 || pack.Slots == null || pack.Slots.Count == 0)
            throw new InvalidDataException("format");
        foreach (var e in pack.Slots)
        {
            byte[] bytes;
            try { bytes = Convert.FromHexString(e.Hex ?? ""); }
            catch (FormatException) { throw new InvalidDataException("hex"); }
            if (bytes.Length != RecordLength) throw new InvalidDataException("length");
        }
        return pack;
    }

    /// <summary>Scrive le voci scelte sugli slot di destinazione. map = (indice voce nel pack, slot destinazione da 0).</summary>
    public static void Apply(byte[] d, SlotPack pack, IReadOnlyList<(int Entry, int Target)> map)
    {
        // validazione completa prima di toccare il buffer
        if (map.Select(m => m.Target).Distinct().Count() != map.Count) throw new InvalidDataException("duplicate target");
        var prepared = new List<(int Base, byte[] Data)>();
        foreach (var (entry, target) in map)
        {
            if (entry < 0 || entry >= pack.Slots.Count) throw new InvalidDataException("entry");
            int b = CareerLayout.CheckedSlotBase(d, target);
            var bytes = Convert.FromHexString(pack.Slots[entry].Hex);
            if (bytes.Length != RecordLength) throw new InvalidDataException("length");
            prepared.Add((b, bytes));
        }
        foreach (var (b, bytes) in prepared) Buffer.BlockCopy(bytes, 0, d, b, bytes.Length);
    }
}
