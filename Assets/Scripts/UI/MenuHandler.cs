using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuHandler : MonoBehaviour
{
    public void LoadLevel(){
        SceneManager.LoadScene("LevelSelect");
    }
}
