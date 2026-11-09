namespace NFSU2ProfileMaster;

public sealed record DonorBlock(int Offset, byte[] Bytes);

/// <summary>Un'auto che si puo' scrivere in uno slot (blocchi per-slot della tabella auto).</summary>
public sealed record Donor(int Id, string Name, uint Hash, DonorBlock[] Blocks);

public static class Donors
{
    private static readonly int[] SlotBlockOffsets = { 0x5870, 0x5950, 0x5B20 };

    public static readonly List<Donor> All = Build();
    private static readonly Dictionary<uint, Donor> ByHash = BuildIndex(All);

    private static List<Donor> Build()
    {
        var list = new List<Donor>();
        for (int id = 0; id < GameData.Cars.Length; id++)
        {
            var (name, blocks) = GameData.Cars[id];
            var slotBlocks = blocks.Where(b => SlotBlockOffsets.Contains(b.Offset))
                                   .Select(b => new DonorBlock(b.Offset, SaveService.HexToBytes(b.Hex))).ToArray();
            if (slotBlocks.Length != SlotBlockOffsets.Length) continue;
            var car = slotBlocks.First(b => b.Offset == SlotBlockOffsets[0]);
            if (car.Bytes.Length < CareerLayout.CarTypeHashRel + 4) continue;
            uint hash = BitConverter.ToUInt32(car.Bytes, CareerLayout.CarTypeHashRel);
            list.Add(new Donor(id, name.Trim(), hash, slotBlocks));
        }
        return list;
    }

    private static Dictionary<uint, Donor> BuildIndex(List<Donor> all)
    {
        var map = new Dictionary<uint, Donor>();
        foreach (var d in all)
            if (d.Hash != 0 && !map.ContainsKey(d.Hash)) map[d.Hash] = d;   // varianti con lo stesso hash: vince la prima
        return map;
    }

    public static Donor? Identify(uint hash) => ByHash.TryGetValue(hash, out var d) ? d : null;
}
