using UnityEngine;
using UnityEngine.SceneManagement;

public class TechnogisSplashController : MonoBehaviour
{
    public string nextSceneName;
    public float delay = 2f;

    void Start()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            int count = SceneManager.sceneCountInBuildSettings;
            if (count > 1)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(1);
                nextSceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            }
        }

        Invoke(nameof(LoadNext), delay);
    }

    void LoadNext()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}

