
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor.Animations;



public class BlendShapeEditor : EditorWindow
{
    [MenuItem("Tools/BlendShape Data Editor")]
    public static void ShowWindow()
    {
        string baseFolder = GetBaseFolder();
        string jsonFolder = $"{baseFolder}/Json";
        string jsonPath = $"{jsonFolder}/BlendEntries.json";

        // Create folders if they don't exist
        if (!AssetDatabase.IsValidFolder(baseFolder))
        {
            AssetDatabase.CreateFolder("Assets", "AnimationBlendShape");
        }
        if (!AssetDatabase.IsValidFolder(jsonFolder))
        {
            AssetDatabase.CreateFolder(baseFolder, "Json");
        }
        string outputFolder = $"{baseFolder}/Output";
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            AssetDatabase.CreateFolder(baseFolder, "Output");
        }

        BlendShapeEditor window = GetWindow<BlendShapeEditor>("BlendShape Editor");
        window.jsonPath = jsonPath; // Update the instance's jsonPath
        // Load blend entries from JSON if it exists
        if (File.Exists(jsonPath))
        {
            string json = File.ReadAllText(jsonPath);
            BlendData data = JsonUtility.FromJson<BlendData>(json);
            if (data != null)
            {
                window.blendEntries = data.entries ?? new List<BlendEntry>();
            }
        }
    }

    private static string GetBaseFolder()
    {
        string[] guids = AssetDatabase.FindAssets("AnimationBlendShape t:Folder");
        if (guids.Length > 0)
        {
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        return "Assets/AnimationBlendShape";
    }

    [System.Serializable]
    public class BlendEntry
    {
        public string englishID;
        public string japaneseName;
        public List<string> aliases = new List<string>();
    }

    [System.Serializable]
    public class BlendData
    {
        public List<BlendEntry> entries = new List<BlendEntry>();
    }

    private List<BlendEntry> blendEntries = new List<BlendEntry>();
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;
    private GameObject targetObject;
    private List<string> blendShapeNames = new List<string>();
    private string jsonPath = "BlendEntries.json";
    private string generatedFilePath = "BlendSkinData.cs";

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();

        // Left side: Registered Blend Entries
        GUILayout.BeginVertical(GUILayout.Width(position.width / 2));
        GUILayout.Label("Registered Blend Shapes", EditorStyles.boldLabel);
        leftScrollPos = GUILayout.BeginScrollView(leftScrollPos);
        for (int i = 0; i < blendEntries.Count; i++)
        {
            BlendEntry entry = blendEntries[i];
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label("English ID:");
            entry.englishID = GUILayout.TextField(entry.englishID);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Japanese Name:");
            entry.japaneseName = GUILayout.TextField(entry.japaneseName);
            GUILayout.EndHorizontal();
            GUILayout.Label("Aliases:");
            for (int j = 0; j < entry.aliases.Count; j++)
            {
                GUILayout.BeginHorizontal();
                entry.aliases[j] = GUILayout.TextField(entry.aliases[j]);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    entry.aliases.RemoveAt(j);
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Alias"))
            {
                entry.aliases.Add("");
            }
            if (GUILayout.Button("Remove Entry"))
            {
                blendEntries.RemoveAt(i);
                i--;
            }
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Add New Entry"))
        {
            blendEntries.Add(new BlendEntry());
        }
        GUILayout.EndVertical();

        // Right side: Reference GameObject/Prefab and BlendShapes
        GUILayout.BeginVertical();
        GUILayout.Label("Reference Object", EditorStyles.boldLabel);
        var local_targetObject = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), false);
        if(targetObject != local_targetObject)
        {
            UpdateBlendShapeNames();
            targetObject = local_targetObject;
        }
        if (targetObject != null)
        {

            GUILayout.Label("Blend Shapes in Mesh", EditorStyles.boldLabel);
            rightScrollPos = GUILayout.BeginScrollView(rightScrollPos);
            foreach (string name in blendShapeNames)
            {
                bool exists = false;
                foreach (BlendEntry entry in blendEntries)
                {
                    if (entry.japaneseName == name || entry.aliases.Contains(name))
                    {
                        exists = true;
                        break;
                    }
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(exists ? "›" : "~", GUILayout.Width(20));
                GUILayout.Label(name);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("All IN"))
            {
                foreach (string name in blendShapeNames)
                {
                    bool exists = false;
                    foreach (BlendEntry entry in blendEntries)
                    {
                        if (entry.japaneseName == name || entry.aliases.Contains(name))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        string sanitizedEnglish = SanitizeForEnum(name);
                        blendEntries.Add(new BlendEntry { englishID = sanitizedEnglish, japaneseName = name });
                    }
                }
            }
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        // Submenu for Save and Generate
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save"))
        {
            SaveToJson();
        }
        if (GUILayout.Button("Generate"))
        {
            GenerateCodeFile();
        }
        GUILayout.EndHorizontal();
    }

    private void UpdateBlendShapeNames()
    {
        blendShapeNames.Clear();
        SkinnedMeshRenderer smr = null;

        if (targetObject == null) return;
        smr = targetObject.GetComponentInChildren<SkinnedMeshRenderer>();

        if (smr != null && smr.sharedMesh != null)
        {
            Mesh mesh = smr.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                blendShapeNames.Add(mesh.GetBlendShapeName(i));
            }
        }
    }

    private string SanitizeForEnum(string input)
    {
        // Remove invalid characters for enum names, start with letter, etc.
        input = Regex.Replace(input, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(input) || char.IsDigit(input[0]))
        {
            input = "_" + input;
        }
        return input;
    }

    private void SaveToJson()
    {
        BlendData data = new BlendData { entries = blendEntries };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(jsonPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"Saved blend entries to {jsonPath}");
    }

    private void GenerateCodeFile()
    {
        // Ensure Output folder exists
        string baseFolder = GetBaseFolder();
        string outputFolder = $"{baseFolder}/Output";
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            AssetDatabase.CreateFolder(baseFolder, "Output");
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        sb.AppendLine("public enum BlendSkinID");
        sb.AppendLine("{");
        foreach (BlendEntry entry in blendEntries)
        {
            if (!string.IsNullOrEmpty(entry.englishID))
            {
                sb.AppendLine($"    {entry.englishID},");
            }
        }
        sb.AppendLine("}");

        sb.AppendLine();

        sb.AppendLine("public static class BlendSkinData");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly Dictionary<BlendSkinID, List<string>> BlendMappings = new Dictionary<BlendSkinID, List<string>>");
        sb.AppendLine("    {");
        foreach (BlendEntry entry in blendEntries)
        {
            if (!string.IsNullOrEmpty(entry.englishID))
            {
                sb.Append($"        {{ BlendSkinID.{entry.englishID}, new List<string> {{ \"{entry.japaneseName}\"");
                foreach (string alias in entry.aliases)
                {
                    sb.Append($", \"{alias}\"");
                }
                sb.AppendLine(" } },");
            }
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        File.WriteAllText($"{outputFolder}/{generatedFilePath}", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"Generated file at {generatedFilePath}");
    }
}
#endif