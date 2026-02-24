#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Starfield2026.ModelLoader.Helpers;

public static class TrainerGender
{
    public enum BodyType { Boy, Girl, Man, Woman, Unknown }

    public static BodyType Classify(string characterFolderPath)
    {
        string folderName = Path.GetFileName(characterFolderPath.TrimEnd('/', '\\'));
        if (folderName.Length >= 6 && folderName.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
        {
            string numPart = folderName.Substring(2, 4);
            if (int.TryParse(numPart, out int id))
            {
                // Scarlet characters use a separate table for IDs that overlap with PLZA
                bool isScarlet = characterFolderPath.Contains("scarlet", StringComparison.OrdinalIgnoreCase);
                if (isScarlet && ScarletBodyTypeTable.TryGetValue(id, out var scarletType))
                    return scarletType;
                if (BodyTypeTable.TryGetValue(id, out var bodyType))
                    return bodyType;
            }
        }

        return BodyType.Unknown;
    }

    public static bool IsTrainerFolder(string characterFolderPath)
    {
        string folderName = Path.GetFileName(characterFolderPath.TrimEnd('/', '\\'));
        return folderName.Length >= 6 && folderName.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(folderName[2]);
    }

    public static bool IsFeminine(string characterFolderPath)
    {
        var bt = Classify(characterFolderPath);
        return bt == BodyType.Girl || bt == BodyType.Woman;
    }

    public static string GetSharedFolderName(BodyType bodyType) => bodyType switch
    {
        BodyType.Boy   => "boy",
        BodyType.Girl  => "girl",
        BodyType.Man   => "man",
        BodyType.Woman => "woman",
        _              => "man",
    };

    public static readonly Dictionary<int, BodyType> BodyTypeTable = new()
    {
        // PLZA / sun-moon (IDs 1-122)
        [1]  = BodyType.Girl,   [2]  = BodyType.Girl,
        [3]  = BodyType.Boy,    [4]  = BodyType.Girl,
        [5]  = BodyType.Man,    [6]  = BodyType.Woman,
        [7]  = BodyType.Boy,    [8]  = BodyType.Girl,
        [9]  = BodyType.Woman,  [10] = BodyType.Woman,
        [11] = BodyType.Man,    [12] = BodyType.Man,
        [13] = BodyType.Man,    [14] = BodyType.Man,
        [15] = BodyType.Woman,  [16] = BodyType.Man,
        [17] = BodyType.Woman,  [18] = BodyType.Man,
        [19] = BodyType.Man,    [20] = BodyType.Woman,
        [21] = BodyType.Boy,    [22] = BodyType.Girl,
        [23] = BodyType.Woman,  [24] = BodyType.Man,
        [25] = BodyType.Man,    [26] = BodyType.Man,
        [27] = BodyType.Man,    [28] = BodyType.Woman,
        [29] = BodyType.Man,    [30] = BodyType.Woman,
        [31] = BodyType.Boy,    [32] = BodyType.Man,
        [33] = BodyType.Man,    [34] = BodyType.Boy,
        [35] = BodyType.Girl,   [37] = BodyType.Woman,
        [38] = BodyType.Woman,  [39] = BodyType.Boy,
        [40] = BodyType.Man,    [41] = BodyType.Boy,
        [42] = BodyType.Girl,   [43] = BodyType.Woman,
        [44] = BodyType.Girl,   [45] = BodyType.Girl,
        [46] = BodyType.Man,    [47] = BodyType.Boy,
        [48] = BodyType.Girl,   [49] = BodyType.Man,
        [50] = BodyType.Woman,  [51] = BodyType.Man,
        [52] = BodyType.Girl,   [55] = BodyType.Man,
        [56] = BodyType.Man,    [58] = BodyType.Man,
        [59] = BodyType.Man,    [60] = BodyType.Man,
        [61] = BodyType.Man,    [62] = BodyType.Woman,
        [63] = BodyType.Boy,    [64] = BodyType.Girl,
        [65] = BodyType.Man,    [66] = BodyType.Man,
        [67] = BodyType.Man,    [68] = BodyType.Woman,
        [69] = BodyType.Man,    [70] = BodyType.Man,
        [71] = BodyType.Woman,  [72] = BodyType.Woman,
        [73] = BodyType.Man,    [74] = BodyType.Man,
        [75] = BodyType.Woman,  [76] = BodyType.Man,
        [77] = BodyType.Man,    [78] = BodyType.Man,
        [79] = BodyType.Woman,  [80] = BodyType.Man,
        [81] = BodyType.Man,    [82] = BodyType.Man,
        [83] = BodyType.Woman,  [84] = BodyType.Man,
        [85] = BodyType.Man,    [86] = BodyType.Woman,
        [87] = BodyType.Man,    [88] = BodyType.Man,
        [89] = BodyType.Man,    [90] = BodyType.Man,
        [91] = BodyType.Man,    [92] = BodyType.Man,
        [93] = BodyType.Man,    [94] = BodyType.Man,
        [95] = BodyType.Woman,  [96] = BodyType.Man,
        [97] = BodyType.Woman,  [98] = BodyType.Man,
        [99] = BodyType.Man,    [100] = BodyType.Man,
        [101] = BodyType.Woman, [102] = BodyType.Man,
        [103] = BodyType.Man,   [104] = BodyType.Man,
        [105] = BodyType.Man,   [106] = BodyType.Man,
        [107] = BodyType.Man,   [108] = BodyType.Woman,
        [109] = BodyType.Man,   [110] = BodyType.Man,
        [111] = BodyType.Woman, [112] = BodyType.Man,
        [115] = BodyType.Man,   [116] = BodyType.Man,
        [118] = BodyType.Boy,   [119] = BodyType.Man,
        [120] = BodyType.Man,   [121] = BodyType.Woman,
        [122] = BodyType.Man,   [202] = BodyType.Man,
        [203] = BodyType.Man,   [204] = BodyType.Man,
        [205] = BodyType.Man,   [206] = BodyType.Woman,
        [207] = BodyType.Man,   [208] = BodyType.Man,
        [213] = BodyType.Man,

        // sun-moon high IDs
        [1000] = BodyType.Man,  [1001] = BodyType.Man,
        [1002] = BodyType.Woman,[1003] = BodyType.Man,
        [1004] = BodyType.Man,  [1005] = BodyType.Woman,
        [1006] = BodyType.Man,  [1007] = BodyType.Man,
        [1008] = BodyType.Man,  [1009] = BodyType.Woman,
        [1010] = BodyType.Man,
    };

    // Scarlet characters — separate table to avoid ID collisions with PLZA/sun-moon.
    // Data from trtype FlatBuffer sex enum + archive folder body labels.
    public static readonly Dictionary<int, BodyType> ScarletBodyTypeTable = new()
    {
        // Unique characters (model_uq)
        [0]   = BodyType.Boy,    // player (primitive)
        [20]  = BodyType.Man,    // Professor Sada/Turo (future)
        [40]  = BodyType.Girl,   // Nemona (rival/friend)
        [50]  = BodyType.Girl,   // Penny (junior)
        [70]  = BodyType.Boy,    // Arven (senior)
        [80]  = BodyType.Man,    // Director Clavell
        [81]  = BodyType.Man,    // Director Clavell (alt)
        [90]  = BodyType.Woman,  // Ms. Dendra (battle teacher)
        [100] = BodyType.Woman,  // Ms. Tyme (math teacher)
        [110] = BodyType.Woman,  // Ms. Raifort (history)
        [120] = BodyType.Woman,  // Ms. Miriam (nursing)
        [130] = BodyType.Man,    // Mr. Saguaro (home ec)
        [140] = BodyType.Man,    // Mr. Jacq (biology)
        [150] = BodyType.Man,    // Mr. Salvatore (languages)
        [160] = BodyType.Man,    // Hassel (Dragon, E4)
        [170] = BodyType.Woman,  // Rika (Ground, E4)
        [180] = BodyType.Woman,  // Poppy (Steel, E4)
        [190] = BodyType.Woman,  // Geeta (Champion)
        [200] = BodyType.Man,    // Larry (Normal GL / E4)
        [210] = BodyType.Woman,  // Katy (Bug GL)
        [220] = BodyType.Man,    // Kofu (Water GL)
        [230] = BodyType.Man,    // Brassius (Grass GL)
        [240] = BodyType.Girl,   // Iono (Electric GL)
        [241] = BodyType.Girl,   // Iono (Electric GL alt)
        [250] = BodyType.Man,    // Grusha (Ice GL)
        [260] = BodyType.Woman,  // Tulip (Psychic GL)
        [270] = BodyType.Woman,  // Ryme (Ghost GL)
        [280] = BodyType.Woman,  // Mela (Fire, Team Star)
        [290] = BodyType.Woman,  // Eri (Fighting, Team Star)
        [300] = BodyType.Man,    // Atticus (Poison, Team Star)
        [310] = BodyType.Man,    // Ortega (Fairy, Team Star)
        [320] = BodyType.Man,    // Giacomo (Dark, Team Star)

        // Trainer classes (model_tr)
        [330] = BodyType.Man,    // karate
        [340] = BodyType.Man,    // artist
        [350] = BodyType.Woman,  // researcher
        [360] = BodyType.Man,    // tanpan (youngster)
        [370] = BodyType.Man,    // worker
        [380] = BodyType.Man,    // musician
        [390] = BodyType.Man,    // businessman
        [400] = BodyType.Woman,  // ol (office lady)
        [410] = BodyType.Man,    // mania
        [420] = BodyType.Man,    // mountain
        [430] = BodyType.Woman,  // chef
        [440] = BodyType.Man,    // backpacker
        [450] = BodyType.Man,    // waiter
        [460] = BodyType.Woman,  // waitress
        [470] = BodyType.Man,    // cleaning
        [480] = BodyType.Man,    // dragontamer
        [490] = BodyType.Woman,  // model
        [500] = BodyType.Man,    // taxidriver
        [510] = BodyType.Woman,  // pc (pokecenter nurse)
        [520] = BodyType.Man,    // fs (shop clerk)
        [600] = BodyType.Man,    // tribem (Team Star male)
        [601] = BodyType.Man,    // tribemvat
        [610] = BodyType.Woman,  // tribef (Team Star female)
        [611] = BodyType.Woman,  // tribefvat
        [880] = BodyType.Woman,  // mother
        [890] = BodyType.Man,    // deliverer
        [900] = BodyType.Man,    // richm
        [910] = BodyType.Woman,  // richf
        [920] = BodyType.Man,    // staffm
        [930] = BodyType.Woman,  // stafff

        // Variation NPCs (model_vr)
        [620] = BodyType.Boy,    // schildm
        [630] = BodyType.Boy,    // syouthm
        [640] = BodyType.Man,    // sadultm
        [641] = BodyType.Man,    // smiddlem
        [650] = BodyType.Man,    // soldm
        [660] = BodyType.Boy,    // sinfantm
        [670] = BodyType.Girl,   // schildf
        [680] = BodyType.Girl,   // syouthf
        [690] = BodyType.Woman,  // sadultf
        [691] = BodyType.Woman,  // smiddlef
        [692] = BodyType.Woman,  // soldf
        [720] = BodyType.Boy,    // infantm
        [730] = BodyType.Girl,   // infantf
        [740] = BodyType.Boy,    // childm
        [750] = BodyType.Girl,   // childf
        [760] = BodyType.Boy,    // youthm
        [770] = BodyType.Girl,   // youthf
        [780] = BodyType.Man,    // adultm
        [790] = BodyType.Woman,  // adultf
        [800] = BodyType.Man,    // middlem
        [810] = BodyType.Woman,  // middlef
        [840] = BodyType.Man,    // oldm
        [850] = BodyType.Woman,  // oldf
        [860] = BodyType.Man,    // hunkm
        [870] = BodyType.Woman,  // hunkf
    };
}
