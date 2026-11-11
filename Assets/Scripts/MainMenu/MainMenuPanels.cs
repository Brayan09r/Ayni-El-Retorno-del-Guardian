using System;
using Ayni.Core;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuPanels : MonoBehaviour
{
    private const float BaseMaster = 0.75f;
    private const float BaseSfx = 0.8f;
    private const float BaseAmbience = 0.7f;
    private const string MasterKey = "Ayni_VolumenGeneral";
    private const string SfxKey = "Ayni_VolumenEfectos";
    private const string AmbienceKey = "Ayni_VolumenMusica";

    private Button[] menuButtons;
    private bool optionsOpen;
    private float masterVolume = BaseMaster;
    private float sfxVolume = BaseSfx;
    private float ambienceVolume = BaseAmbience;
    private bool fullscreen;
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

        masterVolume = PlayerPrefs.GetFloat(MasterKey, BaseMaster);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, BaseSfx);
        ambienceVolume = PlayerPrefs.GetFloat(AmbienceKey, BaseAmbience);
        ApplyAudioSettings();
        fullscreen = PlayerPrefs.GetInt("Ayni_PantallaCompleta", Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fullscreen;
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
        PlayerPrefs.Save();
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
        float panelHeight = 410f;
        var panel = new Rect((width - panelWidth) / 2f, (height - panelHeight) / 2f, panelWidth, panelHeight);
        Fill(new Rect(panel.x - 2f, panel.y - 2f, panel.width + 4f, panel.height + 4f), new Color(0.87f, 0.68f, 0.28f));
        Fill(panel, new Color(0.09f, 0.075f, 0.06f, 0.98f));

        GUI.color = new Color(0.96f, 0.77f, 0.31f);
        GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 46f), "OPCIONES", titleStyle);
        GUI.color = Color.white;

        float rowY = panel.y + 86f;
        masterVolume = DrawVolumeRow(panel, rowY, "Volumen general", masterVolume, ApplyMasterVolume);
        sfxVolume = DrawVolumeRow(panel, rowY + 62f, "Efectos", sfxVolume, ApplySfxVolume);
        ambienceVolume = DrawVolumeRow(panel, rowY + 124f, "Música y ambiente", ambienceVolume, ApplyAmbienceVolume);

        float displayY = panel.y + 272f;
        GUI.Label(new Rect(panel.x + 32f, displayY, 235f, 36f), "Pantalla completa", labelStyle);
        bool updatedFullscreen = GUI.Toggle(new Rect(panel.x + 270f, displayY + 8f, 180f, 26f), fullscreen, fullscreen ? "Activada" : "Desactivada");
        if (updatedFullscreen != fullscreen)
        {
            fullscreen = updatedFullscreen;
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt("Ayni_PantallaCompleta", fullscreen ? 1 : 0);
        }

        if (GUI.Button(new Rect(panel.x + 32f, panel.yMax - 66f, 160f, 42f), "RESTABLECER", buttonStyle))
            ResetOptions();

        if (GUI.Button(new Rect(panel.x + panel.width - 178f, panel.yMax - 66f, 146f, 42f), "VOLVER", buttonStyle))
            CloseOptions();

        GUI.matrix = previousMatrix;
    }

    private void ResetOptions()
    {
        masterVolume = BaseMaster;
        sfxVolume = BaseSfx;
        ambienceVolume = BaseAmbience;
        fullscreen = false;
        ApplyAudioSettings();
        Screen.fullScreen = false;

        PlayerPrefs.SetFloat(MasterKey, masterVolume);
        PlayerPrefs.SetFloat(SfxKey, sfxVolume);
        PlayerPrefs.SetFloat(AmbienceKey, ambienceVolume);
        PlayerPrefs.SetInt("Ayni_PantallaCompleta", 0);
        PlayerPrefs.Save();
    }
    private void ApplyAudioSettings()
    {
        AudioListener.volume = masterVolume;
        AyniAudio.Volume = sfxVolume;
        AyniAudio.AmbienceVolume = ambienceVolume;
    }

    private void ApplyMasterVolume(float value)
    {
        masterVolume = value;
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MasterKey, value);
    }

    private void ApplySfxVolume(float value)
    {
        sfxVolume = value;
        AyniAudio.Volume = value;
        PlayerPrefs.SetFloat(SfxKey, value);
    }

    private void ApplyAmbienceVolume(float value)
    {
        ambienceVolume = value;
        AyniAudio.AmbienceVolume = value;
        PlayerPrefs.SetFloat(AmbienceKey, value);
    }
    private float DrawVolumeRow(Rect panel, float rowY, string label, float currentValue, Action<float> apply)
    {
        GUI.Label(new Rect(panel.x + 32f, rowY, 235f, 36f), label, labelStyle);
        float updatedValue = GUI.HorizontalSlider(new Rect(panel.x + 270f, rowY + 11f, panel.width - 365f, 18f), currentValue, 0f, 1f);
        if (!Mathf.Approximately(updatedValue, currentValue))
            apply(updatedValue);

        GUI.Label(new Rect(panel.xMax - 84f, rowY, 58f, 36f), Mathf.RoundToInt(updatedValue * 100f) + " %", valueStyle);
        return updatedValue;
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
