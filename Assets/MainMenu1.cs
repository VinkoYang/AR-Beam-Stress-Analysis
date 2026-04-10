using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu1 : MonoBehaviour
{
    public void StartSimulation()
    {
        SceneManager.LoadScene("BeamScene 1");
    }
}