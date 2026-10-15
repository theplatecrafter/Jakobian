using System.IO;
using UnityEditor;

public static class JakobianAssets
{
    // Placeholder asset project; only reviewed/labeled assets are eligible for a future bundle.
    [MenuItem("Jakobian/Build Windows Asset Bundles")]
    public static void BuildBundles()
    {
        string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/unity/windows");
        Directory.CreateDirectory(output);
        BuildPipeline.BuildAssetBundles(output, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
    }
}
