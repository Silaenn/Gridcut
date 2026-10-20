using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class EditorAutoRefresh
{
    private static Object lastSelection = null;
    private static EntityId lastSelectionId = EntityId.None;

    static EditorAutoRefresh()
    {
        EditorApplication.update += Update;
    }

    static void Update()
    {
        Object currentSelection = Selection.activeObject;

        if (lastSelectionId.IsValid() && EditorUtility.EntityIdToObject(lastSelectionId) == null)
        {
            EditorApplication.DirtyHierarchyWindowSorting();
            lastSelectionId = EntityId.None;
            lastSelection = null;
        }

        if (currentSelection != lastSelection)
        {
            if (!Application.isPlaying)
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
            }

            lastSelection = currentSelection;
            lastSelectionId = currentSelection ? currentSelection.GetEntityId() : EntityId.None;
        }
    }
}