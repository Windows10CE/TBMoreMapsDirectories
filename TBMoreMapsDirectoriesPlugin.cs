using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Timberborn.Common;
using Timberborn.Core;
using Timberborn.MapSystem;
using Timberborn.MapSystemUI;

namespace TBMoreMapsDirectories
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    [HarmonyPatch]
    public class MoreMapsDirectoriesPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "io.thunderstore.timberborn.moremapdirs";
        public const string ModName = "MoreMapDirectories";
        public const string ModVer = "1.0.1";
        
        public static readonly List<string> MapDirs = new List<string>();

        new internal static ManualLogSource Logger;

        public void Awake()
        {
            Logger = base.Logger;
            
            if (!Directory.Exists(Path.Combine(Paths.BepInExRootPath, "Maps")))
                Directory.CreateDirectory(Path.Combine(Paths.BepInExRootPath, "Maps"));
            
            var extras = Config.Bind<string>("CONFIG", "MapDirectories", "%USERDATAFOLDER%/Maps;%BEPINEX%/Maps", "Directories to search for maps.\n%BEPINEX% will be autoreplaced with the BepInEx folder, %USERDATAFOLDER% will be replaced by the directory above where maps are normally stored\nSeperated by the ';' character").Value;
            extras = extras.Replace("%BEPINEX%", Paths.BepInExRootPath).Replace("%USERDATAFOLDER%", UserDataFolder.Folder);
            MapDirs.AddRange(extras.Split(new char[] {';'}, StringSplitOptions.RemoveEmptyEntries));
            Harmony.CreateAndPatchAll(typeof(MoreMapsDirectoriesPlugin).Assembly, ModGUID);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapRepository), nameof(MapRepository.GetCustomMapNames))]
        private static bool GetMapNamesPrefix(out IEnumerable<string> __result)
        {
            List<string> mapNames = new List<string>();
            for (int i = 0; i < MapDirs.Count; i++)
            {
                foreach (var file in Directory.GetFiles(MapDirs[i], "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".json") || x.EndsWith(".timber")))
                {
                    var actualFile = file.Substring(0, file.Split('.').Last().Length + 1).Replace(MapDirs[i], "");
                    actualFile = actualFile.Substring(1, actualFile.Length - 1);
                    mapNames.Add($"[{i}];{actualFile}");
                }
            }
            __result = mapNames;
            return false;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapRepository), "CustomMapNameToFileName", new Type[] { typeof(string), typeof(string) })]
        private static bool GetMapFileNamePrefix(MapRepository __instance, string mapName, string extension, IFileService ____fileService, out string __result)
        {
            var parts = mapName.Split(';');
            var realMapName = string.Join(";", parts.Skip(1));
            if (!int.TryParse(parts[0].Substring(1, parts[0].Length - 2), out int directoryIndex))
            {
                __result = Path.Combine(MapDirs[0], mapName) + extension;
                return false;
            }
            __result = Path.Combine(MapDirs[directoryIndex], realMapName) + extension;
            return false;
        }

        private static Regex StartRegex = new Regex("^\\[([0-9]+)\\];");
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapService), "DisplayName")]
        private static void FixGetDisplayName(ref string mapName)
        {
            var match = StartRegex.Match(mapName);
            if (match.Success)
            {
                if (match.Value == "[0];")
                    mapName = mapName.Substring(match.Value.Length);
                else
                {
                    mapName = $"[BepInEx Extra Maps {match.Groups[1].Value}] " + mapName.Substring(match.Value.Length);
                }
            }
        }
    }
}
