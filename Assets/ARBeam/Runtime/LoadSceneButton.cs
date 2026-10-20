using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARBeam
{
    /// <summary>
    /// Menu button target: loads one scene on a desktop and another on a phone or headset (for HoloMech: the
    /// screen-space scene on a PC, the mixed-reality scene on a device).
    /// </summary>
    public class LoadSceneButton : MonoBehaviour
    {
        [SerializeField] string desktopScene = "HoloMech";
        [SerializeField] string deviceScene = "HoloMechXR";

        public void Load()
        {
            bool device = Application.isMobilePlatform || UnityEngine.XR.XRSettings.isDeviceActive;
            SceneManager.LoadScene(device ? deviceScene : desktopScene);
        }
    }
}
