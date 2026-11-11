using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanels : MonoBehaviour
{
    private const float BaseMaster = 0.75f;

    private Button[] menuButtons;
    private bool optionsOpen;
    private float masterVolume = BaseMaster;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        menuButtons = GetComponentsInChildren<Button>(true);

        foreach (Button button in menuButtons)
        {
            if (button.gameObject.name == "opciones")
                button.onClick.AddListener(OpenOptions);
        }

        masterVolume = AudioListener.volume;
    }

    private void Update()
    {
        if (optionsOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseOptions();
    }

    private void OpenOptions()
    {
        optionsOpen = true;
        SetMenuButtonsInteractable(false);
    }

    private void CloseOptions()
    {
        optionsOpen = false;
        SetMenuButtonsInteractable(true);
    }

    private void SetMenuButtonsInteractable(bool value)
    {
        foreach (Button button in menuButtons)
            button.interactable = value;
    }

    private void OnGUI()
    {
        if (!optionsOpen)
            return;

        EnsureStyles();
        GUI.depth = -120;

        float scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2f);
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        Fill(new Rect(0, 0, width, height), new Color(0f, 0f, 0f, 0.72f));

        float panelWidth = Mathf.Min(620f, width - 40f);
        float panelHeight = 330f;
        var panel = new Rect((width - panelWidth) / 2f, (height - panelHeight) / 2f, panelWidth, panelHeight);
        Fill(new Rect(panel.x - 2f, panel.y - 2f, panel.width + 4f, panel.height + 4f), new Color(0.87f, 0.68f, 0.28f));
        Fill(panel, new Color(0.09f, 0.075f, 0.06f, 0.98f));

        GUI.color = new Color(0.96f, 0.77f, 0.31f);
        GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 46f), "OPCIONES", titleStyle);
        GUI.color = Color.white;

        float rowY = panel.y + 104f;
        GUI.Label(new Rect(panel.x + 32f, rowY, 235f, 36f), "Volumen general", labelStyle);
        float updatedMaster = GUI.HorizontalSlider(new Rect(panel.x + 270f, rowY + 11f, panel.width - 365f, 18f), masterVolume, 0f, 1f);
        if (!Mathf.Approximately(updatedMaster, masterVolume))
        {
            masterVolume = updatedMaster;
            AudioListener.volume = masterVolume;
        }
        GUI.Label(new Rect(panel.xMax - 84f, rowY, 58f, 36f), Mathf.RoundToInt(masterVolume * 100f) + " %", valueStyle);

        if (GUI.Button(new Rect(panel.x + panel.width - 178f, panel.yMax - 66f, 146f, 42f), "VOLVER", buttonStyle))
            CloseOptions();

        GUI.matrix = previousMatrix;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft
        };
        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleRight
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
