// Tabelle parti performance (ID byte dal name table di SPEED2.EXE; livelli/prezzi/conflitti da GLOBALB.BUN).
// Dati e struttura da NFSU2-Savegame-Editor di hackch (MIT).
namespace NFSU2ProfileMaster;

public sealed record PerfPart(int Index, string Key, string Label, string Category, bool Verified, bool Active, int? Level, int? Price, int[] Conflicts);
public sealed record PerfCategory(string Code, string Label, List<int> Indices);
public sealed record UniquePerfPart(int Index, string Category, string Label);

public static class PerfData
{
    private static readonly (string Code, string Label, string[] Parts)[] Categories =
    {
        ("WT", "Weight reduction", new[] { "REMOVE_REAR_SEATS", "REMOVE_INTERIOR_PANELS", "LIGHTWEIGHT_WINDOWS", "LIGHTWEIGHT_SEATS", "LIGHTWEIGHT_DOORS", "FOAM_FILLED_INTERIOR" }),
        ("TR", "Transmission", new[] { "SHORT_THROW_SHIFT_KIT", "LIGHT_FLYWHEEL", "DIFFERENTIAL", "LIMITED_SLIP_DIFFERENTIAL", "HIGH_PERFORMANCE_CLUTCH", "6_SPEED_TRANSMISSION" }),
        ("NO", "Nitrous", new[] { "DRY_SHOT_OF_NITROUS", "WET_SHOT_OF_NITROUS", "DIRECT_PORT_NITROUS_OXIDE" }),
        ("EN", "Engine", new[] { "MUFFLER", "COLD_AIR_INTAKE_SYSTEM", "REPLACE_HEADERS", "MILD_CAMSHAFT_AND_CAM_GEARS", "CAT_BACK_EXHAUST_SYSTEM", "HIGH_FLOW_INTAKE_MANIFOLD", "LARGER_DIAMETER_DOWNPIPE", "RACING_CAMSHAFT_AND_GEARS", "PORT_AND_POLISH_HEADS", "BLUEPRINT_THE_BLOCK", "HIGH_FLOW_HEADERS" }),
        ("TU", "Turbo", new[] { "STAGE_1_TURBO_KIT", "STAGE_2_TURBO_KIT", "STAGE_3_TURBO_KIT" }),
        ("SU", "Suspension", new[] { "SPORT_SPRINGS_AND_SHOCKS", "STRUT_TOWER_BAR", "PERFORMANCE_SPRINGS_AND_SHOCKS", "FRONT_AND_REAR_SWAY_BARS", "FRONT_AND_REAR_TIE_BARS", "UPGRADE_BUSHINGS", "COIL_OVER_SUSPENSION_SYSTEM", "CAMBER_KIT", "LARGE_DIAMETER_SWAY_BARS" }),
        ("BR", "Brakes", new[] { "STREET_COMPOUND_BRAKE_PADS", "STEEL_BRAIDED_BRAKE_LINES", "CROSS_DRILLED_ROTORS", "LARGE_DIAMETER_ROTORS", "RACE_COMPOUND_BRAKE_PADS", "CROSS_DRILLED_AND_SLOTTED_ROTORS", "6_PISTON_RACING_CALIPERS" }),
        ("EC", "Engine control & fuel", new[] { "REMOVE_SPEED_LIMITER", "PERFORMANCE_CHIP", "HIGH_FLOW_FUEL_PUMP", "FUEL_PRESSURE_REGULATOR", "FUEL_RAIL", "FUEL_FILTER", "ENGINE_MANAGEMENT_UNIT", "FUEL_INJECTORS" }),
        ("TI", "Tires", new[] { "STREET_PERFORMANCE_TIRES", "PRO_PERFORMANCE_TIRES", "EXTREME_PERFORMANCE_TIRES" }),
        ("AE", "Aero & body", new[] { "FRONT_BUMPER_L1", "FRONT_BUMPER_L2", "FRONT_BUMPER_L3", "SIDESKIRTS_L1", "SIDESKIRTS_L2", "SIDESKIRTS_L3", "REAR_BUMPER_L1", "REAR_BUMPER_L2", "REAR_BUMPER_L3", "SPOILER_L1", "SPOILER_L2", "SPOILER_L3" }),
    };

    private static readonly HashSet<int> VerifiedIndices = new() { 15, 16 };

    // (index, level, price, conflitti...) - gli ID assenti (33/34) restano fuori per non spostare i flag successivi
    private static readonly int[][] ItemDetails =
    {
        new[]{0,1,250}, new[]{1,1,250}, new[]{2,2,1000}, new[]{3,2,1000}, new[]{4,3,3000}, new[]{5,3,1000},
        new[]{6,1,100}, new[]{7,1,400}, new[]{8,1,500,9,11}, new[]{9,2,1000,8,11}, new[]{10,2,1500}, new[]{11,3,5000,9,8},
        new[]{12,1,750,13,14}, new[]{13,2,2000,12,14}, new[]{14,3,4000,12,13},
        new[]{15,1,500,19}, new[]{16,1,250}, new[]{17,1,500,25}, new[]{18,1,250,22},
        new[]{19,2,1000,15}, new[]{20,2,500}, new[]{21,2,500},
        new[]{22,3,500,18}, new[]{23,3,1000}, new[]{24,3,1500}, new[]{25,3,500,17},
        new[]{26,1,1000,27,28}, new[]{27,2,4000,26,28}, new[]{28,3,8000,27,26},
        new[]{29,1,500,31,35}, new[]{30,1,250}, new[]{31,2,2000,29,35}, new[]{32,2,1000,37},
        new[]{35,3,5000,29,31}, new[]{36,2,500}, new[]{37,3,2000,32},
        new[]{38,1,250,42}, new[]{39,1,250}, new[]{40,1,500,41,43}, new[]{41,2,1500,40,43},
        new[]{42,2,1000,38}, new[]{43,3,3000,40,41}, new[]{44,3,3000},
        new[]{45,1,100}, new[]{46,2,1000,51}, new[]{47,2,500}, new[]{48,1,250}, new[]{49,1,250}, new[]{50,1,250},
        new[]{51,3,7000,46}, new[]{52,3,1000},
        new[]{53,1,500,54,55}, new[]{54,2,1500,53,55}, new[]{55,3,3000,53,54},
        new[]{56,1,0,57,58}, new[]{57,2,0,56,58}, new[]{58,3,0,57,56},
        new[]{59,1,0,60,61}, new[]{60,2,0,59,61}, new[]{61,3,0,60,59},
        new[]{62,1,0,63,64}, new[]{63,2,0,62,64}, new[]{64,3,0,63,62},
        new[]{65,1,0,66,67}, new[]{66,2,0,65,67}, new[]{67,3,0,66,65},
    };

    public static readonly List<PerfPart> Parts = new();
    public static readonly List<PerfCategory> AllCategories = new();
    public static readonly List<PerfCategory> TuningCategories;
    public static readonly List<int> TuningIndices;
    public static readonly List<UniquePerfPart> UniqueParts;

    static PerfData()
    {
        var details = ItemDetails.ToDictionary(r => r[0]);
        foreach (var (code, label, names) in Categories)
        {
            var group = new PerfCategory(code, label, new List<int>());
            foreach (var name in names)
            {
                int index = Parts.Count;
                details.TryGetValue(index, out var d);
                Parts.Add(new PerfPart(index, $"{code}_{name}", Humanize(name), code, VerifiedIndices.Contains(index),
                    d != null, d?[1], d?[2], d == null ? Array.Empty<int>() : d.Skip(3).ToArray()));
                group.Indices.Add(index);
            }
            AllCategories.Add(group);
        }
        // AE (aero/body) deriva dalle parti visive: non e' nel tuning delle prestazioni
        TuningCategories = AllCategories.Where(c => c.Code != "AE").ToList();
        TuningIndices = TuningCategories.SelectMany(c => c.Indices.Where(i => Parts[i].Active)).ToList();
        UniqueParts = TuningCategories.Select((c, i) => new UniquePerfPart(i, c.Code, c.Label)).ToList();
    }

    private static string Humanize(string key) =>
        string.Join(' ', key.Split('_').Select(w =>
            w.Length == 2 && w[0] is 'L' or 'l' && char.IsDigit(w[1]) ? w.ToUpperInvariant()
            : w[..1] + w[1..].ToLowerInvariant()));
}
