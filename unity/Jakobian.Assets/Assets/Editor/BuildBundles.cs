using System.IO;
using UnityEditor;

public static class JakobianAssets
{
    [MenuItem("Jakobian/Build Windows Asset Bundles")]
    public static void BuildBundles()
    {
        string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/unity/windows");
        Directory.CreateDirectory(output);
        AssetDatabase.Refresh();
        var builds = new[] { new AssetBundleBuild { assetBundleName = "jakobian-shaders",
            assetNames = new[] { "Assets/JakobianMappedTerrain.shader" } } };
        if (BuildPipeline.BuildAssetBundles(output, builds, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64) == null) throw new System.Exception("Shader bundle build failed.");
    }
}
