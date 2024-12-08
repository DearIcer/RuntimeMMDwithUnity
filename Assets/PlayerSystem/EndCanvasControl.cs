using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndCanvasControl : MonoBehaviour
{
    public void Exit()
    {
        // 在编辑器模式下使用EditorApplication.Exit()
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        // 在运行时使用Application.Quit()
        Application.Quit();
#endif
    }
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
