using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TruckLib;
using TruckLib.HashFs;
using TruckLib.ScsMap;
using TruckLib.Sii;

namespace DlcCheck
{
    class DlcChecker
    {
        Encoding encoding = Encoding.UTF8;

        Dictionary<string, HashFsReader> hashFsReaders
               = new Dictionary<string, HashFsReader>();

        Dictionary<string, string> assetOrigins 
            = new Dictionary<string, string>();

        HashSet<string> foundAssets = new HashSet<string>();

        public void Check(string mbdPath, string gameRoot, string output)
        {
            var dlcAssetPaths = Directory.GetFiles(gameRoot, "dlc_*.scs");
            LoadScsFiles(dlcAssetPaths);
            AssignOriginOfAllUnits();

            FindUnitRefsInSectors(mbdPath);

            var sortedOrigins = GroupResults();

            var mapName = Path.GetFileNameWithoutExtension(mbdPath);
            OutputResult(sortedOrigins, mapName, output);
        }    

        /// <summary>
        /// Searches for referenced assets by loading each sector individually.
        /// </summary>
        /// <param name="mbdPath"></param>
        private void FindUnitRefsInSectors(string mbdPath)
        {
            var sectorDir = Path.Combine(
                Path.GetDirectoryName(mbdPath),
                Path.GetFileNameWithoutExtension(mbdPath));

            var baseFiles = Directory.GetFiles(sectorDir, "*.base");
            foreach (var baseFile in baseFiles)
            {
                var map = Map.Open(mbdPath, new string[] {
                    Path.GetFileName(baseFile)
                    });
                FindUnitRefs(map);
            }
        }

        /// <summary>
        /// Searches for referenced assets in a Map.
        /// </summary>
        /// <param name="map"></param>
        private void FindUnitRefs(Map map)
        {
            var allItems = map.GetAllItems();
            foreach (var item in allItems)
            {
                HandleItem(item.Value);
            }
        }

        private void HandleItem(MapItem item)
        {
            // don't @ me 

            const string buildingSchemePrefix = "bld_scheme";
            const string cityPrefix = "city";
            const string cornerPrefabPrefix = "corner";
            const string curveModelPrefix = "curve";
            const string farModelPrefix = "far_model";
            const string ferryPrefix = "ferry";
            const string modelPrefix = "model";
            const string moverPrefix = "mover";
            const string prefabPrefix = "prefab";
            const string railingPrefix = "railing";
            const string roadEdgePrefix = "road_edge";
            const string roadPrefix = "road";
            const string roadMaterialPrefix = "road_mat";
            const string roadSidewalkPrefix = "sidewalk";
            const string semaphorePrefix = "tr_sem_prof";
            const string signPrefix = "sign";

            switch (item)
            {
                case Building bld:
                    Add(buildingSchemePrefix, bld.Name);
                    break;
                case CityArea city:
                    Add(cityPrefix, city.Name);
                    break;
                case Curve curve:
                    Add(curveModelPrefix, curve.Model);
                    break;
                case FarModel farModel:
                    foreach (var m in farModel.Models)
                    {
                        Add(farModelPrefix, m.Model);
                    }
                    break;
                case Ferry ferry:
                    Add(ferryPrefix, ferry.Port);
                    break;
                case Model model:
                    Add(modelPrefix, model.Name);
                    break;
                case Mover mover:
                    Add(moverPrefix, mover.Model);
                    break;
                case Prefab prefab:
                    Add(prefabPrefix, prefab.Model);
                    foreach(var corner in prefab.Corners)
                    {
                        Add(cornerPrefabPrefix, corner.Model);
                    }
                    Add(semaphorePrefix, prefab.SemaphoreProfile);
                    break;
                case Road road:
                    Add(roadPrefix, road.RoadType);
                    Add(roadMaterialPrefix, road.Material);
                    foreach(var side in new[] { road.Left, road.Right })
                    {
                        Add(roadSidewalkPrefix, side.Sidewalk.Material);
                        Add(roadEdgePrefix, side.LeftEdge);
                        Add(roadEdgePrefix, side.RightEdge);
                        foreach (var railing in side.Railings.Models)
                        {
                            Add(railingPrefix, railing.Model);
                        }
                    }
                    break;
                case Sign sign:
                    Add(signPrefix, sign.Model);
                    break;
                default:
                    break;
            }

            void Add(string prefix, Token token)
            {
                foundAssets.Add(prefix + "." + token);
            }
        }

        /// <summary>
        /// Groups results by DLC.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, SortedSet<string>> GroupResults()
        {
            // group results by DLC.
            // part 1: create dict. key: DLC, val: used assets from DLC
            var sortedOrigins = new Dictionary<string, SortedSet<string>>();
            foreach (var key in hashFsReaders.Keys)
            {
                sortedOrigins.Add(key, new SortedSet<string>());
            }

            // part 2: put results in dict
            foreach (var foundAsset in foundAssets)
            {
                if (assetOrigins.ContainsKey(foundAsset))
                {
                    var orig = assetOrigins[foundAsset];
                    sortedOrigins[orig].Add(foundAsset);
                }
            }

            return sortedOrigins;
        }

        private void OutputResult(Dictionary<string, SortedSet<string>> sortedOrigins,
            string mapName, string outputFile)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"The map \"{mapName}\" uses assets from the following DLCs:\n");

            // print list of .scs files used
            foreach (var dlc in sortedOrigins)
            {
                if (dlc.Value.Count == 0) continue;
                sb.AppendLine("* " + dlc.Key);
            }
            sb.AppendLine();

            // print detailed list of all DLC assets found
            sb.AppendLine("Detailed list of all DLC assets:\n");
            foreach (var dlc in sortedOrigins)
            {
                if (dlc.Value.Count == 0) continue;

                sb.AppendLine($"[ {dlc.Key} ]");
                foreach (var usedAsset in dlc.Value)
                {
                    sb.AppendLine(usedAsset);
                }
                sb.AppendLine();
            }

            if (string.IsNullOrEmpty(outputFile))
            {
                Console.Write(sb);
            }
            else
            {
                File.WriteAllText(outputFile, sb.ToString());
            }
        }

        private void AssignOriginOfAllUnits()
        {
            foreach (var hashFsKvp in hashFsReaders)
            {
                var hfs = hashFsKvp.Value;
                var hfsFileName = hashFsKvp.Key;

                var toLoad = new (string dir, bool recursive)[]
                {
                    ("/def/", false),
                    ("/def/city", false),
                    ("/def/ferry", true),
                    ("/def/sign", true),
                    ("/def/world", false),
                };

                foreach (var dir in toLoad)
                {
                    if (hfs.EntryExists(dir.dir) == EntryType.Directory)
                    {
                        AssignOriginOfUnitsInDirectory(
                            hfsFileName, hfs, dir.dir, dir.recursive);
                    }
                }
            }

        }

        private void AssignOriginOfUnitsInDirectory(string scsFilename, HashFsReader h, 
            string dir, bool recursive = false)
        {
            var files = h.GetDirectoryListing(dir, true, false);
            foreach (var file in files)
            {
                if (file.EndsWith("/") && recursive)
                {
                    AssignOriginOfUnitsInDirectory(scsFilename, h, file, true);
                }
                else if(file.EndsWith(".sii") || file.EndsWith(".sui"))
                {
                    AssignOriginOfUnitsInDef(scsFilename, h, file);
                }
            }
        }

        private void AssignOriginOfUnitsInDef(string scsFilename, HashFsReader h, string def)
        {
            var content = encoding.GetString(h.ExtractEntry(def));
            var sii = SiiFile.FromString(content);
            foreach (var unit in sii.Units)
            {
                // ignore nameless units
                if (unit.Name.StartsWith("_nameless.") || unit.Name.StartsWith("."))
                    continue;

                assetOrigins.Add(unit.Name, scsFilename);
            }
        }

        private void LoadScsFiles(string[] dlcAssetPaths)
        {
            foreach (var path in dlcAssetPaths)
            {
                var filename = Path.GetFileName(path);

                var hfr = HashFsReader.Open(path);
                hashFsReaders.Add(filename, hfr);
            }
        }
    }
}
