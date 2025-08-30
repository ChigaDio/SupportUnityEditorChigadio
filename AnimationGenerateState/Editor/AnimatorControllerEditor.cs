#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static AnimatorControllerEditorWindow;

public class AnimatorControllerEditorWindow : EditorWindow
{
    private string className = "MyAnimState";
    private int selectedJsonIndex = 0;
    private string[] jsonFiles = new string[0];
    private bool useExistingJson = false;

    [MenuItem("Tools/Generate AnimState")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimatorControllerEditorWindow>("AnimState Generator");
        window.minSize = new Vector2(400, 200);
        window.RefreshJsonList();
    }

    private void RefreshJsonList()
    {
        string jsonFolder = GetJsonFolder();
        if (Directory.Exists(jsonFolder))
        {
            jsonFiles = Directory.GetFiles(jsonFolder, "*.json")
                                 .Select(Path.GetFileName)
                                 .ToArray();
        }
        else
        {
            jsonFiles = new string[0];
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("AnimState Class Generator", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(useExistingJson);
        className = EditorGUILayout.TextField("Class Name", className);
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);

        useExistingJson = EditorGUILayout.Toggle("Use Existing JSON", useExistingJson);

        if (useExistingJson && jsonFiles.Length > 0)
        {
            selectedJsonIndex = EditorGUILayout.Popup("Select JSON", selectedJsonIndex, jsonFiles);
        }
        else if (useExistingJson && jsonFiles.Length == 0)
        {
            EditorGUILayout.HelpBox("JSON files not found in Json folder.", MessageType.Warning);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Generate"))
        {
            if (string.IsNullOrEmpty(className) && !useExistingJson)
            {
                Debug.LogWarning("Class name is empty!");
                return;
            }

            if (useExistingJson && jsonFiles.Length > 0)
            {
                GenerateFromJson(jsonFiles[selectedJsonIndex]);
            }
            else
            {
                GenerateFromController(className);
            }
        }
    }

    private static string GetBaseFolder()
    {
        string[] guids = AssetDatabase.FindAssets("AnimationGenerateState t:Folder");
        if (guids.Length > 0)
        {
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }
        return "Assets/AnimationGenerateState";
    }

    private static string GetJsonFolder()
    {
        return Path.Combine(GetBaseFolder(), "Json");
    }

    private void GenerateFromJson(string jsonFile)
    {
        string jsonPath = Path.Combine(GetJsonFolder(), jsonFile);
        string json = File.ReadAllText(jsonPath);
        var data = JsonUtility.FromJson<ControllerData>(json);

        string outputFolder = Path.Combine(GetBaseFolder(), "Output");

        // éQè∆ÉpÉXÇ™ë∂ç›Ç∑ÇÍÇŒì«Ç›çûÇÒÇ≈ê∂ê¨
        if (!string.IsNullOrEmpty(data.ControllerPath) && File.Exists(data.ControllerPath))
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                data.ControllerPath.Replace(Application.dataPath, "Assets"));
            if (controller != null)
            {
                data = AnimatorControllerEditorHelper.ExtractControllerData(controller, data.ClassName, data.ControllerPath);
            }
        }

        AnimatorControllerEditorHelper.GenerateNewAnimStateClass(data, outputFolder, data.ClassName);
        AssetDatabase.Refresh();

        Debug.Log($"Class regenerated from JSON: {jsonFile}");
    }

    private void GenerateFromController(string className)
    {
        string controllerPath = EditorUtility.OpenFilePanel("Select Animator Controller", "Assets", "controller");
        if (string.IsNullOrEmpty(controllerPath)) return;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            controllerPath.Replace(Application.dataPath, "Assets"));

        if (controller == null)
        {
            Debug.LogError("Failed to load AnimatorController.");
            return;
        }

        var data = AnimatorControllerEditorHelper.ExtractControllerData(controller, className, controllerPath);

        // Save JSON
        string jsonFolder = GetJsonFolder();
        if (!Directory.Exists(jsonFolder)) Directory.CreateDirectory(jsonFolder);
        File.WriteAllText(Path.Combine(jsonFolder, $"{className}Data.json"),
                          JsonUtility.ToJson(data, true));

        string outputFolder = Path.Combine(GetBaseFolder(), "Output");
        AnimatorControllerEditorHelper.GenerateNewAnimStateClass(data, outputFolder, className);
        AssetDatabase.Refresh();

        Debug.Log($"Class generated from AnimatorController and JSON saved.");
        RefreshJsonList();
    }

    [Serializable]
    public class ControllerData
    {
        public string ClassName;
        public string ControllerPath;
        public List<LayerData> Layers;
        public List<string> Parameters;
    }

    [Serializable]
    public class LayerData
    {
        public string Name;
        public List<string> States;
    }
}

public static class AnimatorControllerEditorHelper
{
    public static ControllerData ExtractControllerData(AnimatorController controller, string className, string controllerPath)
    {
        var data = new AnimatorControllerEditorWindow.ControllerData
        {
            ClassName = className,
            ControllerPath = controllerPath,
            Layers = new List<AnimatorControllerEditorWindow.LayerData>(),
            Parameters = new List<string>()
        };

        foreach (var layer in controller.layers)
        {
            var layerData = new AnimatorControllerEditorWindow.LayerData { Name = layer.name, States = new List<string>() };
            foreach (var state in layer.stateMachine.states)
            {
                layerData.States.Add(state.state.name);
            }
            data.Layers.Add(layerData);
        }

        foreach (var param in controller.parameters)
        {
            data.Parameters.Add(param.name);
        }

        return data;
    }

    public static void GenerateNewAnimStateClass(AnimatorControllerEditorWindow.ControllerData data, string outputFolder, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"public enum {data.ClassName}Enum");
        sb.AppendLine("{");
        foreach (var layer in data.Layers)
        {
            foreach (var state in layer.States)
            {
                string enumName = $"{layer.Name}_{state}".Replace(" ", "_");
                sb.AppendLine($"    {enumName},");
            }
        }
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public class {data.ClassName} : BaseAnimState<{data.ClassName}Enum>");
        sb.AppendLine("{");
        sb.AppendLine($"    static {data.ClassName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        StateNames = new Dictionary<{data.ClassName}Enum, string>");
        sb.AppendLine("        {");
        foreach (var layer in data.Layers)
        {
            foreach (var state in layer.States)
            {
                string enumName = $"{layer.Name}_{state}".Replace(" ", "_");
                string stateName = $"{layer.Name}.{state}";
                sb.AppendLine($"            {{ {data.ClassName}Enum.{enumName}, \"{stateName}\" }},");
            }
        }
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected override void InitializeStateNames() { }");
        sb.AppendLine("}");
        sb.AppendLine("// Parameters:");
        foreach (var param in data.Parameters) sb.AppendLine($"// {param}");

        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
        string classPath = Path.Combine(outputFolder, $"{data.ClassName}.cs");
        File.WriteAllText(classPath, sb.ToString());
        Debug.Log($"Generated class saved to: {classPath}");
    }
}
#endif
