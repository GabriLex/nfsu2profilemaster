// Layout del salvataggio 20CM e logica di modifica.
// Porting in C# di NFSU2-Savegame-Editor di hackch (MIT, https://github.com/hackch/NFSU2-Savegame-Editor)
using System.Buffers.Binary;

namespace NFSU2ProfileMaster;

public static class CareerLayout
{
    public static readonly byte[] Magic = { 0x32, 0x30, 0x43, 0x4D };   // "20CM"
    public const int HeaderSize = 0x1C;
    public const int MoneyOff = 0xA16A;
    public const int MaxMoney = int.MaxValue;
    public const int NameOff = 0xD225;
    public const int NameFieldSize = 32;
    public const int NameMaxBytes = 31;

    public const int SlotBase = 0x5870;
    public const int SlotStride = 0x7F2;
    public const int SlotCount = 5;
    public const int SlotRecordRel = -8;
    public const int SlotRecordEndRel = SlotStride + SlotRecordRel;
    public const int ProfileSize = 54966;
    public const int ExtraSlotRecordBase = ProfileSize;
    public const int MaxSlotCount = 10;
    public const int ExtendedSaveSize = ExtraSlotRecordBase + SlotCount * SlotStride;

    public const int CarTypeHashRel = 0x14;
    public const int CarKeyRel = 0x2BC;
    public const int SlotLockRel = 0x428;
    public const int SlotBoughtRel = 0x429;
    public const int PerfOff = 0x310;
    public const int VisualOff = 0x18;
    public const int VisualCount = 170;
    public const int UniquePerfOff = 0x2E8;

    public static int SlotBaseOf(int slot)
    {
        if (slot < 0 || slot >= MaxSlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        if (slot < SlotCount) return SlotBase + slot * SlotStride;
        return ExtraSlotRecordBase + (slot - SlotCount) * SlotStride - SlotRecordRel;
    }

    public static int SlotsIn(byte[] data) => data.Length == ExtendedSaveSize ? MaxSlotCount : SlotCount;

    public static int CheckedSlotBase(byte[] data, int slot)
    {
        if (slot < 0 || slot >= SlotsIn(data)) throw new ArgumentOutOfRangeException(nameof(slot));
        return SlotBaseOf(slot);
    }
}

/// <summary>CRC-32 MSB-first (poly 0x04C11DB7) usato dal gioco per i blocchi del salvataggio.</summary>
public static class Nfsu2Crc
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i << 24;
            for (int k = 0; k < 8; k++) c = (c & 0x80000000) != 0 ? (c << 1) ^ 0x04C11DB7 : c << 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(byte[] data, int start, int end)
    {
        if (end - start < 4) return 0;
        uint first = ((uint)data[start] << 24) | ((uint)data[start + 1] << 16) | ((uint)data[start + 2] << 8) | data[start + 3];
        uint r = ~first;
        for (int i = start + 4; i < end; i++)
            r = ((r << 8) | data[i]) ^ Table[r >> 24];
        return ~r;
    }
}

public readonly record struct ChecksumStatus(bool BlockA, bool BlockB, bool Header)
{
    public bool AllOk => BlockA && BlockB && Header;
}

/// <summary>Operazioni sul buffer di lavoro del salvataggio (modificano il buffer sul posto).</summary>
public static class CareerSave
{
    private const int HdrLenA = 0x08, HdrLenB = 0x0C, HdrCrcA = 0x10, HdrCrcB = 0x14, HdrCrcH = 0x18;

    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o, 4));
    private static void SetU32(byte[] d, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(o, 4), v);

    // ───── validazione e checksum ─────

    /// <summary>Controlla magic, dimensione header e lunghezza dei blocchi. Ritorna null se ok, altrimenti il motivo.</summary>
    public static string? Check(byte[] data)
    {
        if (data.Length < CareerLayout.HeaderSize) return "File too small";
        for (int i = 0; i < 4; i++) if (data[i] != CareerLayout.Magic[i]) return "Not a 20CM NFSU2 career save";
        if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2)) != (ushort)(data.Length & 0xFFFF)) return "Header size mismatch";
        long endB = (long)CareerLayout.HeaderSize + U32(data, HdrLenA) + U32(data, HdrLenB);
        if (endB != data.Length) return "Block lengths mismatch";
        return null;
    }

    private static (int aStart, int aEnd, int bStart, int bEnd) Blocks(byte[] d)
    {
        int aStart = CareerLayout.HeaderSize;
        int aEnd = aStart + (int)U32(d, HdrLenA);
        int bEnd = aEnd + (int)U32(d, HdrLenB);
        return (aStart, aEnd, aEnd, bEnd);
    }

    public static ChecksumStatus Verify(byte[] d)
    {
        var (a0, a1, b0, b1) = Blocks(d);
        return new ChecksumStatus(
            Nfsu2Crc.Compute(d, a0, a1) == U32(d, HdrCrcA),
            Nfsu2Crc.Compute(d, b0, b1) == U32(d, HdrCrcB),
            Nfsu2Crc.Compute(d, 0, CareerLayout.HeaderSize - 4) == U32(d, HdrCrcH));
    }

    /// <summary>Riscrive i tre checksum (prima i blocchi, poi l'header). Solo sui primi bytes della carriera.</summary>
    public static void RecalculateChecksums(byte[] d)
    {
        var (a0, a1, b0, b1) = Blocks(d);
        SetU32(d, HdrCrcA, Nfsu2Crc.Compute(d, a0, a1));
        SetU32(d, HdrCrcB, Nfsu2Crc.Compute(d, b0, b1));
        SetU32(d, HdrCrcH, Nfsu2Crc.Compute(d, 0, CareerLayout.HeaderSize - 4));
    }

    // ───── profilo ─────

    public static int GetMoney(byte[] d) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(CareerLayout.MoneyOff, 4));

    public static void SetMoney(byte[] d, long value)
    {
        if (value < 0 || value > CareerLayout.MaxMoney) throw new ArgumentOutOfRangeException(nameof(value));
        BinaryPrimitives.WriteInt32LittleEndian(d.AsSpan(CareerLayout.MoneyOff, 4), (int)value);
    }

    public static string GetName(byte[] d)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < CareerLayout.NameFieldSize; i++)
        {
            byte c = d[CareerLayout.NameOff + i];
            if (c == 0) break;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    /// <summary>Nome pilota: 1-31 caratteri Latin-1.</summary>
    public static string? ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "empty";
        if (name.Length > CareerLayout.NameMaxBytes) return "too long";
        if (name.Any(c => c > 0xFF)) return "latin1";
        return null;
    }

    public static void SetName(byte[] d, string name)
    {
        if (ValidateName(name) is { } err) throw new ArgumentException(err, nameof(name));
        for (int i = 0; i < CareerLayout.NameFieldSize; i++)
            d[CareerLayout.NameOff + i] = i < name.Length ? (byte)name[i] : (byte)0;
    }

    // ───── slot ─────

    public enum SlotState { Locked, Open, Filled }

    public static uint GetCarTypeHash(byte[] d, int slot) => U32(d, CareerLayout.SlotBaseOf(slot) + CareerLayout.CarTypeHashRel);
    public static bool IsSlotLocked(byte[] d, int slot) => d[CareerLayout.SlotBaseOf(slot) + CareerLayout.SlotLockRel] != 0;
    public static bool IsCarBought(byte[] d, int slot) => d[CareerLayout.SlotBaseOf(slot) + CareerLayout.SlotBoughtRel] != 0;

    public static SlotState GetSlotState(byte[] d, int slot) =>
        IsSlotLocked(d, slot) ? SlotState.Locked : IsCarBought(d, slot) ? SlotState.Filled : SlotState.Open;

    public static void SetSlotLocked(byte[] d, int slot, bool locked) =>
        d[CareerLayout.CheckedSlotBase(d, slot) + CareerLayout.SlotLockRel] = locked ? (byte)1 : (byte)0;

    /// <summary>Scrive un'auto donatrice nello slot: reset aspetto, vinili/decal/neon azzerati, parti a stock, slot sbloccato e comprato.</summary>
    public static void SetSlotCar(byte[] d, int slot, Donor donor)
    {
        int b = CareerLayout.CheckedSlotBase(d, slot);
        foreach (var blk in donor.Blocks)
        {
            int rel = blk.Offset - CareerLayout.SlotBase;
            if (rel < 0 || rel + blk.Bytes.Length > CareerLayout.SlotRecordEndRel)
                throw new InvalidDataException("Donor block does not fit inside a car slot.");
        }
        Array.Clear(d, b + CareerLayout.VisualOff, CareerLayout.VisualCount * 4);
        foreach (var blk in donor.Blocks)
            Buffer.BlockCopy(blk.Bytes, 0, d, b + blk.Offset - CareerLayout.SlotBase, blk.Bytes.Length);
        // vinili, decal e neon non vengono mai portati in un'auto sostituita
        foreach (var (first, end) in new[] { (64, 68), (77, 93), (97, 153), (154, 159) })
            Array.Clear(d, b + CareerLayout.VisualOff + first * 4, (end - first) * 4);
        Array.Clear(d, b + CareerLayout.PerfOff, PerfData.Parts.Count);
        d[b + CareerLayout.SlotLockRel] = 0;
        d[b + CareerLayout.SlotBoughtRel] = 1;
    }

    // ───── performance ─────

    public static bool GetPerfFlag(byte[] d, int slot, int index) => d[CareerLayout.CheckedSlotBase(d, slot) + CareerLayout.PerfOff + index] != 0;
    public static void SetPerfFlag(byte[] d, int slot, int index, bool on) =>
        d[CareerLayout.CheckedSlotBase(d, slot) + CareerLayout.PerfOff + index] = on ? (byte)1 : (byte)0;

    public static uint GetUniqueValue(byte[] d, int slot, int index) =>
        U32(d, CareerLayout.CheckedSlotBase(d, slot) + CareerLayout.UniquePerfOff + index * 4);
    public static void SetUniqueFlag(byte[] d, int slot, int index, bool on) =>
        SetU32(d, CareerLayout.CheckedSlotBase(d, slot) + CareerLayout.UniquePerfOff + index * 4, on ? 1u : 0u);

    /// <summary>Abilita tutte le parti attive e i 9 flag unici (come MAX OUT del sito).</summary>
    public static void MaxOut(byte[] d, int slot)
    {
        foreach (int i in PerfData.TuningIndices) SetPerfFlag(d, slot, i, true);
        foreach (var u in PerfData.UniqueParts) SetUniqueFlag(d, slot, u.Index, true);
    }

    // ───── tabella unlock (420 byte per slot) ─────

    public static int CountUnlocked(byte[] d, int slot)
    {
        int b = CareerLayout.CheckedSlotBase(d, slot), n = 0;
        foreach (int rel in UnlockTable.Offsets) if (d[b + rel] == 0xFF) n++;
        return n;
    }

    public static void SetUnlocks(byte[] d, int slot, bool on)
    {
        int b = CareerLayout.CheckedSlotBase(d, slot);
        foreach (int rel in UnlockTable.Offsets) d[b + rel] = on ? (byte)0xFF : (byte)0;
    }
}
