using UnityEngine;
using UnityEditor;
using GLTFast.Export;
using System.IO;
using System.Threading.Tasks;

namespace CityExportTools
{
    public static class ExportLandscapeBuildingsToGLB
    {
        [MenuItem("Tools/Export Landscape/Buildings → glb")]
        public static async void Export()
        {
            var root = GameObject.Find("Landscape");
            var buildings = root?.transform.Find("Buildings");
            if (buildings == null)
            {
                Debug.LogError("找不到 Landscape/Buildings 節點");
                return;
            }

            var folder = Path.Combine(Application.dataPath, "ExportedGLB");
            Directory.CreateDirectory(folder);

            var exportSettings = new ExportSettings
            {
                Format = GltfFormat.Binary,
                FileConflictResolution = FileConflictResolution.Overwrite
            };
            var objSettings = new GameObjectExportSettings();

            int count = 0;
            foreach (var mf in buildings.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                var name = Sanitize(go.name);
                var path = Path.Combine(folder, $"{name}.glb");

                var temp = Object.Instantiate(go);
                temp.name = name;
                try
                {
                    ProcessMaterials(temp);

                    var exporter = new GameObjectExport(exportSettings, objSettings);
                    exporter.AddScene(new GameObject[] { temp }, name);
                    bool ok = await exporter.SaveToFileAndDispose(path);
                    if (ok) count++;
                    Debug.Log(ok ? $"✅ 匯出: {name}" : $"❌ 匯出失敗: {name}");
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"🎉 完成匯出，共 {count} 個 glTF");
        }

        static void ProcessMaterials(GameObject go)
        {
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
#if UNITY_EDITOR
                var mats = rend.sharedMaterials;
                var newMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++) {
                    var o = mats[i];
                    if (o == null) { newMats[i] = null; continue; }

                    Object mainTex = null;
                    if (o.HasProperty("_ColorMap")) mainTex = o.GetTexture("_ColorMap");
                    else if (o.HasProperty("_MainTex")) mainTex = o.GetTexture("_MainTex");
                    else {
                        for (int p = 0; p < ShaderUtil.GetPropertyCount(o.shader); p++) {
                            if (ShaderUtil.GetPropertyType(o.shader, p) ==
                                ShaderUtil.ShaderPropertyType.TexEnv) {
                                var prop = ShaderUtil.GetPropertyName(o.shader, p);
                                if (o.HasProperty(prop)) {
                                    mainTex = o.GetTexture(prop);
                                    break;
                                }
                            }
                        }
                    }

                    var m2 = new Material(Shader.Find("Standard"));
                    if (mainTex != null) m2.mainTexture = (Texture)mainTex;
                    newMats[i] = m2;
                }
                rend.sharedMaterials = newMats;
#endif
            }
        }

        static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
