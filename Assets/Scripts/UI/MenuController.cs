using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Nombres de las Escenas")]
    [SerializeField] private string gameSceneName = "SampleScene"; 
    [SerializeField] private string videoIntroSceneName = "Intro";  

    // "NUEVA PARTIDA": Si existe la escena de vídeo la carga; si no, entra directo al mapa
    public void NewGame()
    {
        PlayerPrefs.DeleteAll(); 

        if (Application.CanStreamedLevelBeLoaded(videoIntroSceneName))
        {
            SceneManager.LoadScene(videoIntroSceneName);
        }
        else
        {
            Debug.LogWarning("No se encontró la escena de vídeo '" + videoIntroSceneName + "'. Cargando el juego directo...");
            SceneManager.LoadScene(gameSceneName);
        }
    }

    // "CONTINUAR": Carga directo el mapa de juego sin pasar por el vídeo
    public void ContinueGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // "SALIR": Cierra el juego
    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}