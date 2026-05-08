#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Max9126 {
    public class GUIDFixer : EditorWindow {
        struct FileData {
            public long fileID;
            public string guid;
            public FileData(long fileID, string guid) {
                this.fileID = fileID;
                this.guid = guid;
            }
        };
        
        static readonly Regex GuidPattern = new Regex(@"guid:\s(?<guid>[0-9A-Fa-f]+)", RegexOptions.Compiled);
        static readonly Regex FileDataPropPattern = new Regex(@"m_Script: {fileID: (?<file>-?\d+), guid: (?<guid>[0-9A-Fa-f]+), type: (?<type>\d+)}", RegexOptions.Compiled);
        static readonly long STANDARD_MONO_BEHAVIOUR_FILE_ID = 11500000;
        static readonly List<string> FixableExtensions = new List<string> {
            "unity",
            "prefab",
            "asset"
        };
        
        static string[] FixablePlugins;
        static string[] FixableGUIDs;
        static List<FileData> ResolvedDataOlds = new List<FileData>();
        static List<FileData> ResolvedDataNews = new List<FileData>();
        static List<FileData> UnresolvableData = new List<FileData>();
        [MenuItem("Max9126/Fix Script References")]
        static void Init() {
            ResolvedDataOlds.Clear();
            ResolvedDataNews.Clear();
            UnresolvableData.Clear();
            GUIDFixer.UpdateFixablePluginsData();
            FixGUIDsInFolder(Application.dataPath);
            EditorUtility.ClearProgressBar();
            Debug.Log("Done");
            PrintAvailableForPurgingPlugins();
            FixablePlugins = null;
            FixableGUIDs = null;
            ResolvedDataOlds.Clear();
            ResolvedDataNews.Clear();
            UnresolvableData.Clear();
        }
        static void PrintAvailableForPurgingPlugins() {
            string DeleteThisPlugins = "";
            for (int i = 0; i < FixableGUIDs.Length; i++) {
                bool HaveUnresolvedGUIDs = false;
                for (int j = 0; j < UnresolvableData.Count; j++)
                    if (string.Equals(UnresolvableData[j].guid, FixableGUIDs[i]))
                        HaveUnresolvedGUIDs = true;
                if (HaveUnresolvedGUIDs)
                    DeleteThisPlugins += FixablePlugins[i] + "\t-\t" + FixableGUIDs[i] + "\n";
            }
            if (string.IsNullOrEmpty(DeleteThisPlugins))
                return;
            DeleteThisPlugins += "Save This plugins";
            Debug.Log(DeleteThisPlugins);
        }
        static void FixGUIDsInFolder(string path) {
            string[] files = Directory.GetFiles(path);
            int filesCount = files.Length;
            for (int i = 0; i < filesCount; i++) {
                EditorUtility.DisplayProgressBar("Progress", path, i / (float)filesCount);
                string ext = Path.GetExtension(files[i]).ToLowerInvariant().Substring(1);
                if (FixableExtensions.Contains(ext))
                    GUIDFixer.ProcessAsset(files[i]);
            }
            EditorUtility.DisplayProgressBar("Progress", path, 1f);
            foreach (var sub in Directory.GetDirectories(path))
                GUIDFixer.FixGUIDsInFolder(sub);
        }
        static void ProcessAsset(string path) {
            int ReplacementsCount = 0;
            string content = File.ReadAllText(path);

            content = FileDataPropPattern.Replace(content, m => {
                string guid = m.Groups["guid"].Value;
                long file = long.Parse(m.Groups["file"].Value);
                string type = m.Groups["type"].Value;

                FileData oldFileData = new FileData(file, guid);
                if (UnresolvableData.Contains(oldFileData))
                    return m.Value;
                int ResolvedDataIndex = ResolvedDataOlds.IndexOf(oldFileData);
                if (ResolvedDataIndex != -1) {
                    ReplacementsCount++;
                    return "m_Script: {fileID: " + ResolvedDataNews[ResolvedDataIndex].fileID + ", guid: " + ResolvedDataNews[ResolvedDataIndex].guid + ", type: " + type + "}";
                }
                if (!FixableGUIDs.Contains(oldFileData.guid))
                    return m.Value;
                FileData ResolvedFileData = ResolveInvalidGUID(oldFileData);
                if (string.IsNullOrEmpty(ResolvedFileData.guid)) {
                    UnresolvableData.Add(oldFileData);
                    Debug.LogWarning(oldFileData.fileID + "\t" + oldFileData.guid + "\nUnresolvable");
                    return m.Value;
                }
                ReplacementsCount++;
                Debug.Log(oldFileData.fileID + "\t" + oldFileData.guid + "\n" + ResolvedFileData.guid);
                ResolvedDataOlds.Add(oldFileData);
                ResolvedDataNews.Add(ResolvedFileData);
                return "m_Script: {fileID: " + ResolvedFileData.fileID + ", guid: " + ResolvedFileData.guid + ", type: " + type + "}";
            });

            if (ReplacementsCount > 0) {
                File.WriteAllText(path, content);
                Debug.Log(path + "\nRewritten");
            }
        }
        static void UpdateFixablePluginsData() {
            List<string> guids = new List<string>();
            List<string> Plugins = new List<string>();
            string path = Path.Combine(Application.dataPath, "Plugins");
            string[] files = Directory.GetFiles(path);
            foreach (string file in files) {
                if (Path.GetExtension(file) == ".meta") {
                    string fileContent = File.ReadAllText(file);
                    Match match = GuidPattern.Match(fileContent);
                    if (!match.Success)
                        continue;
                    string guid = match.Groups["guid"].Value;
                    if (!string.IsNullOrEmpty(guid)) {
                        Plugins.Add(Path.GetFileNameWithoutExtension(file));
                        guids.Add(guid);
                    }
                }
            }
            FixablePlugins = Plugins.ToArray();
            FixableGUIDs = guids.ToArray();
        }
        static System.Type GetType(FileData data) {
            System.Type type = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(data.guid));
            foreach (var obj in assets) {
                long thisFileID = Unsupported.GetLocalIdentifierInFile(obj.GetInstanceID());
                if (thisFileID != data.fileID)
                    continue;
                if (obj is MonoScript script) {
                    type = script.GetClass();
                } else if (thisFileID == data.fileID) {
                    type = obj.GetType();
                }
            }
            return type;
        }
        static string GetTypeValidGUID(System.Type type) {
            if (type == null)
                return null;
            string[] guids = AssetDatabase.FindAssets(type.Name + " t:MonoScript a:packages");
            foreach (var guid in guids) {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(assetPath) == type.Name)
                    return guid;
            }
            return null;
        }
        static FileData ResolveInvalidGUID(FileData data) {
            return new FileData(STANDARD_MONO_BEHAVIOUR_FILE_ID, GUIDFixer.GetTypeValidGUID(GUIDFixer.GetType(data)));
        }
    }
}
#endif