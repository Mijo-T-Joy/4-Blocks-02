using UnityEngine;
using System.Text;

public class HierarchyDumper : MonoBehaviour 
{
    void Start() 
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- SCENE HIERARCHY ---");
        
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            DumpGameObject(go, sb, "");
        }
        
        System.IO.File.WriteAllText(Application.dataPath + "/../scene_dump.txt", sb.ToString());
        Debug.Log("Dumped hierarchy to scene_dump.txt");
    }

    void DumpGameObject(GameObject go, StringBuilder sb, string indent)
    {
        sb.AppendLine($"{indent}- {go.name} (Pos: {go.transform.position})");
        
        var components = go.GetComponents<Component>();
        foreach (var c in components)
        {
            if (c != null) sb.AppendLine($"{indent}  [C] {c.GetType().Name}");
        }

        foreach (Transform child in go.transform)
        {
            DumpGameObject(child.gameObject, sb, indent + "  ");
            
        }
    }
}
