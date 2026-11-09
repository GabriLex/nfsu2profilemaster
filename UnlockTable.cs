// Tabella unlock per slot (420 byte a 0xFF = sbloccato), scritta anche da NFSU2-ProfileCreator.
// Intervalli inclusivi relativi all'inizio dello slot. Fonte: NFSU2-Savegame-Editor di hackch (MIT).
namespace NFSU2ProfileMaster;

public static class UnlockTable
{
    private static readonly (int Start, int End)[] Ranges =
    {
        (0x462, 0x475),
        (0x47E, 0x485),
        (0x59A, 0x5FD),
        (0x61E, 0x64D),
        (0x696, 0x6B1),
        (0x6B6, 0x6BD),
        (0x6C2, 0x6C9),
        (0x486, 0x495),
        (0x49A, 0x49D),
        (0x4A2, 0x4AD),
        (0x4B6, 0x4BD),
        (0x5FE, 0x61D),
        (0x7DA, 0x7E1),
        (0x4CE, 0x53D),
        (0x586, 0x58D),
    };

    public static readonly int[] Offsets = Ranges
        .SelectMany(r => Enumerable.Range(r.Start, r.End - r.Start + 1))
        .OrderBy(x => x).ToArray();
}
