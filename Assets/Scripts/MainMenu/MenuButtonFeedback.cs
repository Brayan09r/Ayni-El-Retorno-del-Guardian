using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScaleMultiplier = 1.06f;
    [SerializeField] private float animationSpeed = 14f;
    [SerializeField] private Color hoverColor = new Color(0.94f, 0.72f, 0.36f, 1f);
    [SerializeField] private float hoverVolume = 0.45f;
    [SerializeField] private float confirmVolume = 0.6f;

    private Button button;
    private TMP_Text label;
    private Vector3 baseScale;
    private Color baseColor;
    private bool isHovered;
    private AudioClip hoverClip;
    private AudioClip confirmClip;

    private void Awake()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>(true);
        baseScale = transform.localScale;

        if (label != null)
            baseColor = label.color;

        hoverClip = Resources.Load<AudioClip>("AyniAudio/ui_mover");
        confirmClip = Resources.Load<AudioClip>("AyniAudio/ui_confirmar");

        if (button != null)
            button.onClick.AddListener(PlayConfirmSound);
    }

    private void Update()
    {
        float blend = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        Vector3 targetScale = isHovered ? baseScale * hoverScaleMultiplier : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, blend);

        if (label != null)
        {
            Color targetColor = isHovered ? hoverColor : baseColor;
            label.color = Color.Lerp(label.color, targetColor, blend);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.interactable)
            return;

        isHovered = true;
        PlaySound(hoverClip, hoverVolume);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void PlayConfirmSound()
    {
        PlaySound(confirmClip, confirmVolume);
    }

    private static void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        Vector3 position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}
