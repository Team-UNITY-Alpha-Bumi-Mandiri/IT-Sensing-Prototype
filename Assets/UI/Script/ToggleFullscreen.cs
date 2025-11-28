using UnityEngine;

public class ToggleFullscreen : MonoBehaviour
{
    private bool isFullscreen = false;

    public void OnButtonClick()
    {
        isFullscreen = !isFullscreen;

        if (isFullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
        }
    }
}
