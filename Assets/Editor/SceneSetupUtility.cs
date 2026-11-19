using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Collection of editor utilities that "perfect" the main game scenes:
///   • LevelSelect – scrollable level grid, fade‑in/out canvas.
///   • GamePlay   – debug gizmo for the grid, async‑load overlay.
///   • Home       – fade transition and optional splash logo helper.
/// </summary>
public static class SceneSetupUtility
{
    // -----------------------------------------------------------------
    // PUBLIC MENU ENTRY
    // -----------------------------------------------------------------
    [MenuItem("Tools/Perfect Game Scenes")]
    public static void FixAllScenes()
    {
        FixLevelSelectScene();
        FixGamePlayScene();
        FixHomeScene();

        AssetDatabase.SaveAssets();
        Debug.Log("[SceneSetupUtility] All scene fixes applied.");
    }

    // -----------------------------------------------------------------
    // LEVEL SELECT
    // -----------------------------------------------------------------
    private static void FixLevelSelectScene()
    {
        const string scenePath = "Assets/Scenes/LevelSelect.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("[SceneSetupUtility] Cannot open LevelSelect scene."); return; }

        // 1️⃣ Find the Grid that holds the level buttons
        var grid = GameObject.Find("LevelGrid");
        if (grid == null) { Debug.LogWarning("[LevelSelect] LevelGrid not found."); return; }

        // Make the grid a scrollable view if it isn’t already
        var scrollRect = grid.GetComponentInParent<ScrollRect>();
        if (scrollRect == null)
        {
            var parent = new GameObject("LevelScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Mask), typeof(Image));
            parent.transform.SetParent(grid.transform.parent, false);
            var rt = parent.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Re‑parent the grid under the scroll view’s viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewport.transform.SetParent(parent.transform, false);
            var vRt = viewport.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0, 0);
            vRt.anchorMax = new Vector2(1, 1);
            vRt.offsetMin = vRt.offsetMax = Vector2.zero;

            // Move the actual grid under the viewport
            grid.transform.SetParent(viewport.transform, false);

            // Configure ScrollRect
            var sr = parent.GetComponent<ScrollRect>();
            sr.content = grid.GetComponent<RectTransform>();
            sr.horizontal = false;   // vertical list of levels
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 20f;

            Debug.Log("[LevelSelect] Added ScrollRect wrapper for LevelGrid.");
        }

        // 2️⃣ Add a simple fade‑in CanvasGroup on the root Canvas
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<CanvasGroup>() == null)
        {
            var cg = canvas.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            var fade = canvas.gameObject.AddComponent<FadeInOnEnable>();
            fade.Target = cg;
            Debug.Log("[LevelSelect] Added CanvasGroup + FadeInOnEnable.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // -----------------------------------------------------------------
    // GAME PLAY
    // -----------------------------------------------------------------
    private static void FixGamePlayScene()
    {
        const string scenePath = "Assets/Scenes/GamePlay.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("[SceneSetupUtility] Cannot open GamePlay scene."); return; }

        // 1️⃣ Add a debug gizmo that draws the grid walls in the Scene view
        var gridSys = Object.FindFirstObjectByType<GridSystem>();
        if (gridSys != null && gridSys.gameObject.GetComponent<DebugGridGizmo>() == null)
        {
            gridSys.gameObject.AddComponent<DebugGridGizmo>();
            Debug.Log("[GamePlay] Added DebugGridGizmo component.");
        }

        // 2️⃣ Async‑load overlay (Canvas + Image + simple script)
        var overlay = GameObject.Find("LoadingOverlay");
        if (overlay == null)
        {
            var canvasObj = new GameObject("LoadingOverlay", typeof(Canvas), typeof(CanvasGroup));
            canvasObj.transform.SetParent(null, false);
            var c = canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<LoadingOverlay>();
            Debug.Log("[GamePlay] Added LoadingOverlay prefab.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // -----------------------------------------------------------------
    // HOME / SPLASH
    // -----------------------------------------------------------------
    private static void FixHomeScene()
    {
        const string scenePath = "Assets/Scenes/Home.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("[SceneSetupUtility] Cannot open Home scene."); return; }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null && canvas.gameObject.GetComponent<CanvasGroup>() == null)
        {
            var cg = canvas.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            var fade = canvas.gameObject.AddComponent<FadeInOnEnable>();
            fade.Target = cg;
            Debug.Log("[Home] Added CanvasGroup + FadeInOnEnable.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}

// ---------------------------------------------------------------------
// Helper MonoBehaviours (they are editor‑only but live in the runtime folder
// so they can be attached to objects in the scenes)
// ---------------------------------------------------------------------
public class FadeInOnEnable : MonoBehaviour
{
    public CanvasGroup Target;
    public float Duration = 0.5f;

    private void OnEnable()
    {
        if (Target == null) Target = GetComponent<CanvasGroup>();
        if (Target != null) StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < Duration)
        {
            t += Time.unscaledDeltaTime;
            Target.alpha = Mathf.Lerp(0f, 1f, t / Duration);
            yield return null;
        }
        Target.alpha = 1f;
    }
}
