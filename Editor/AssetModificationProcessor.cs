using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace GameTest
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
            if (sourcePath == TestManager.filePath) // Prevent the user from deleting the TestManager asset. Note this isn't called when GameTest package is uninstalled
            {
                EditorUtility.DisplayDialog("Deletion Failed", "You must uninstall GameTest first via the Package Manager (Window > Package Manager) before deleting this ScriptableObject.", "Ok");
                return AssetDeleteResult.DidDelete;
            }

            // Keep watch for scripts being deleted that contain tests.
            // We need to prepare the TestManager for those tests being deleted or else it throws errors.
            if (EditorWindow.HasOpenInstances<TestManagerUI>())
            {
                TestManager manager = TestManager.Get();
                if (manager != null)
                {
                    // Get the path to the file, excluding its file extension
                    string path = Utilities.GetUnityPath(Path.Join(Path.GetDirectoryName(sourcePath), Path.GetFileNameWithoutExtension(sourcePath)));
                    foreach (Foldout foldout in manager.foldouts.ToArray())
                    {
                        if (foldout.path != path) continue;
                        manager.RemoveFoldout(foldout);
                    }
                }
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

