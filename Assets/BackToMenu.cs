using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToMenu : MonoBehaviour
{
    // 🔹 Call this from button
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}