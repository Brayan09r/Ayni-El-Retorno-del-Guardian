using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";

    public void NewGame()
    {
    Debug.Log("¡El botón funciona y está intentando cargar SampleScene!");
    Go(gameSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }

    private void Go(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}