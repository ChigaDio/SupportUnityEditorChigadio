#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public class SplineTerrainPainter : EditorWindow
{
    [Header("Targets")]
    public SplineContainer spline;
    public Terrain terrain;

    [Header("Paint Settings")]
    public int textureIndex = 0;
    public float width = 6f;
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float spacing = 1.0f;
    [Range(0, 1)] public float strength = 1.0f;
    public bool normalizeLayers = true;

    [Header("Offset Settings")]
    public bool paintCenter = true;
    public bool paintLeft = false;
    public bool paintRight = false;

    [Header("Clear Settings")]
    public bool clearDetails = false; // 草を削除するオプション
    public bool clearTrees = false;   // 木を削除するオプション

    [MenuItem("Tools/Spline Terrain Painter")]
    private static void Open()
    {
        GetWindow<SplineTerrainPainter>(false, "Spline Terrain Painter");
    }

    private void OnGUI()
    {
        spline = (SplineContainer)EditorGUILayout.ObjectField("Spline", spline, typeof(SplineContainer), true);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        textureIndex = EditorGUILayout.IntField("Texture Index", textureIndex);
        width = EditorGUILayout.FloatField("Width (world)", width);
        spacing = EditorGUILayout.FloatField("Spacing (world)", spacing);
        strength = EditorGUILayout.Slider("Strength", strength, 0f, 1f);
        falloff = EditorGUILayout.CurveField("Falloff", falloff);
        normalizeLayers = EditorGUILayout.Toggle("Normalize Layers", normalizeLayers);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Offset Modes", EditorStyles.boldLabel);
        paintCenter = EditorGUILayout.Toggle("Paint Center", paintCenter);
        paintLeft = EditorGUILayout.Toggle("Paint Left Edge", paintLeft);
        paintRight = EditorGUILayout.Toggle("Paint Right Edge", paintRight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Clear Settings", EditorStyles.boldLabel);
        clearDetails = EditorGUILayout.Toggle("Clear Details (Grass)", clearDetails);
        clearTrees = EditorGUILayout.Toggle("Clear Trees", clearTrees);

        using (new EditorGUI.DisabledScope(!CanPaint()))
        {
            if (GUILayout.Button("Paint")) PaintAlongSpline();
        }
    }

    private bool CanPaint()
    {
        if (!spline || !terrain) return false;
        var td = terrain.terrainData;
        return td && textureIndex >= 0 && textureIndex < td.alphamapLayers;
    }

    private List<(Vector3 pos, Vector3 tangent)> SamplePositionsAndTangentsByDistance(
        SplineContainer container, float spacing, int sampleResolution = 2048)
    {
        // 既存のコード（変更なし）
        var results = new List<(Vector3, Vector3)>();
        if (container == null || spacing <= 0f) return results;

        int res = Mathf.Max(8, sampleResolution);
        Vector3[] samples = new Vector3[res + 1];
        float[] cum = new float[res + 1];

        samples[0] = container.EvaluatePosition(0f);
        cum[0] = 0f;
        for (int i = 1; i <= res; i++)
        {
            float t = (float)i / res;
            samples[i] = container.EvaluatePosition(t);
            cum[i] = cum[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
        }

        float total = cum[res];
        if (total <= 0f)
        {
            results.Add((samples[0], Vector3.forward));
            return results;
        }

        int count = Mathf.Max(1, Mathf.CeilToInt(total / spacing));
        for (int k = 0; k <= count; k++)
        {
            float d = Mathf.Min(k * spacing, total);
            int j = 0;
            while (j < res && cum[j + 1] < d) j++;
            if (j >= res)
            {
                Vector3 tan = (samples[res] - samples[res - 1]).normalized;
                results.Add((samples[res], tan));
            }
            else
            {
                float segLen = cum[j + 1] - cum[j];
                float localT = segLen <= 0f ? 0f : (d - cum[j]) / segLen;
                Vector3 pos = Vector3.Lerp(samples[j], samples[j + 1], localT);
                Vector3 tan = (samples[j + 1] - samples[j]).normalized;
                results.Add((pos, tan));
            }
        }
        return results;
    }

    private void PaintAlongSpline()
    {
        var td = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Spline Terrain Paint");

        // アルファマップの操作（テクスチャペイント）
        int aw = td.alphamapWidth;
        int ah = td.alphamapHeight;
        int layers = td.alphamapLayers;
        float[,,] maps = td.GetAlphamaps(0, 0, aw, ah);

        // 草（Details）の操作
        int detailWidth = td.detailWidth;
        int detailHeight = td.detailHeight;
        List<int[,]> detailLayers = new List<int[,]>();
        if (clearDetails)
        {
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                detailLayers.Add(td.GetDetailLayer(0, 0, detailWidth, detailHeight, i));
            }
        }

        // 木（Trees）の操作
        List<TreeInstance> treeInstances = new List<TreeInstance>();
        if (clearTrees)
        {
            treeInstances.AddRange(td.treeInstances);
        }

        float radiusWorld = Mathf.Max(0.01f, width * 0.5f);
        float pxPerMeterX = (aw - 1) / td.size.x;
        float pxPerMeterY = (ah - 1) / td.size.z;
        int radiusPxX = Mathf.CeilToInt(radiusWorld * pxPerMeterX);
        int radiusPxY = Mathf.CeilToInt(radiusWorld * pxPerMeterY);

        float detailPxPerMeterX = (float)detailWidth / td.size.x;
        float detailPxPerMeterY = (float)detailHeight / td.size.z;
        int detailRadiusPxX = Mathf.CeilToInt(radiusWorld * detailPxPerMeterX);
        int detailRadiusPxY = Mathf.CeilToInt(radiusWorld * detailPxPerMeterY);

        var positions = SamplePositionsAndTangentsByDistance(spline, spacing, 2048);
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = td.size;

        foreach (var (worldPosRaw, tangent) in positions)
        {
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            List<Vector3> paintPoints = new List<Vector3>();
            if (paintCenter) paintPoints.Add(worldPosRaw);
            if (paintLeft) paintPoints.Add(worldPosRaw - right * (width / 2f));
            if (paintRight) paintPoints.Add(worldPosRaw + right * (width / 2f));

            foreach (var worldPos in paintPoints)
            {
                float u = Mathf.InverseLerp(tPos.x, tPos.x + tSize.x, worldPos.x);
                float v = Mathf.InverseLerp(tPos.z, tPos.z + tSize.z, worldPos.z);
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;

                int cx = Mathf.RoundToInt(u * (aw - 1));
                int cy = Mathf.RoundToInt(v * (ah - 1));
                int detailCx = Mathf.RoundToInt(u * detailWidth);
                int detailCy = Mathf.RoundToInt(v * detailHeight);

                // テクスチャペイント（既存のコード）
                for (int y = -radiusPxY; y <= radiusPxY; y++)
                {
                    int py = cy + y;
                    if (py < 0 || py >= ah) continue;

                    for (int x = -radiusPxX; x <= radiusPxX; x++)
                    {
                        int px = cx + x;
                        if (px < 0 || px >= aw) continue;

                        float nx = x / (float)radiusPxX;
                        float ny = y / (float)radiusPxY;
                        float r = Mathf.Sqrt(nx * nx + ny * ny);
                        if (r > 1f) continue;

                        float fall = Mathf.Clamp01(falloff.Evaluate(r));
                        float add = strength * fall;

                        float current = maps[py, px, textureIndex];
                        current = Mathf.Clamp01(current + add * (1f - current));
                        maps[py, px, textureIndex] = current;

                        if (normalizeLayers)
                        {
                            float otherSum = 0f;
                            for (int l = 0; l < layers; l++)
                            {
                                if (l == textureIndex) continue;
                                otherSum += maps[py, px, l];
                            }

                            float remain = Mathf.Max(0f, 1f - current);
                            if (otherSum > 0f)
                            {
                                float scale = remain / otherSum;
                                for (int l = 0; l < layers; l++)
                                {
                                    if (l == textureIndex) continue;
                                    maps[py, px, l] *= scale;
                                }
                            }
                            else
                            {
                                maps[py, px, textureIndex] = 1f;
                            }
                        }
                    }
                }

                // 草（Details）の削除
                if (clearDetails)
                {
                    for (int y = -detailRadiusPxY; y <= detailRadiusPxY; y++)
                    {
                        int py = detailCy + y;
                        if (py < 0 || py >= detailHeight) continue;

                        for (int x = -detailRadiusPxX; x <= detailRadiusPxX; x++)
                        {
                            int px = detailCx + x;
                            if (px < 0 || px >= detailWidth) continue;

                            float nx = x / (float)detailRadiusPxX;
                            float ny = y / (float)detailRadiusPxY;
                            float r = Mathf.Sqrt(nx * nx + ny * ny);
                            if (r > 1f) continue;

                            float fall = Mathf.Clamp01(falloff.Evaluate(r));
                            foreach (var detailLayer in detailLayers)
                            {
                                detailLayer[py, px] = 0; // 草の密度を0に設定
                            }
                        }
                    }
                }
            }
        }

        // 木（Trees）の削除
        if (clearTrees)
        {
            List<TreeInstance> newTreeInstances = new List<TreeInstance>();
            foreach (var tree in treeInstances)
            {
                Vector3 treePos = Vector3.Scale(tree.position, tSize) + tPos;
                bool keepTree = true;

                foreach (var (worldPosRaw, tangent) in positions)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                    List<Vector3> checkPoints = new List<Vector3>();
                    if (paintCenter) checkPoints.Add(worldPosRaw);
                    if (paintLeft) checkPoints.Add(worldPosRaw - right * (width / 2f));
                    if (paintRight) checkPoints.Add(worldPosRaw + right * (width / 2f));

                    foreach (var checkPoint in checkPoints)
                    {
                        float distance = Vector2.Distance(
                            new Vector2(treePos.x, treePos.z),
                            new Vector2(checkPoint.x, checkPoint.z)
                        );
                        if (distance <= radiusWorld)
                        {
                            keepTree = false;
                            break;
                        }
                    }
                    if (!keepTree) break;
                }

                if (keepTree) newTreeInstances.Add(tree);
            }
            td.treeInstances = newTreeInstances.ToArray();
        }

        // 変更を適用
        td.SetAlphamaps(0, 0, maps);
        if (clearDetails)
        {
            for (int i = 0; i < detailLayers.Count; i++)
            {
                td.SetDetailLayer(0, 0, i, detailLayers[i]);
            }
        }
        EditorUtility.SetDirty(td);
        Debug.Log("Spline Terrain Painter: Painted along spline and cleared details/trees if enabled.");
    }
}
#endif