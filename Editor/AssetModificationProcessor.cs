using UnityEditor;
using System.IO;

namespace UnityTest
{
    /// <summary>
    /// This class is for detecting changes to assets so we can do things like clean up old assets and follow certain assets when they move around.
    /// https://docs.unity3d.com/ScriptReference/AssetModificationProcessor.html
    /// </summary>
    public class AssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string sourcePath, RemoveAssetOptions options)
        {
            // Check if this asset is our TestManager.cs script. If so, when this asset gets deleted, the TestManager ScriptableObject will not have a valid script reference anymore.
            // So we need to delete the TestManager ScriptableObject
            if (Path.GetFileName(sourcePath) == nameof(TestManager) + ".cs")
            {
                // Unity told us not to use AssetDatabase APIs here, so we have to resort to pure C# methods and pray it works.
                string fullPath = Path.Join(Utilities.assetsPath, sourcePath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                // Do we need to also delete the .meta files??
            }
            if (sourcePath == TestManager.filePath) // Prevent the user from deleting the TestManager asset. Note this isn't called when UnityTest package is uninstalled
            {
                EditorUtility.DisplayDialog("Deletion Failed", "You must uninstall UnityTest first via the Package Manager (Window > Package Manager) before deleting this ScriptableObject.", "Ok");
                return AssetDeleteResult.DidDelete;
            }
            return AssetDeleteResult.DidNotDelete; // We didn't delete the sourcePath file.
        }
        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            if (sourcePath == TestManager.filePath) TestManager.filePath = destinationPath;
            return AssetMoveResult.DidNotMove;
        }
    }
}

