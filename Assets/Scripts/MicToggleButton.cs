using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MicToggleButton : MonoBehaviour
{
    [Header("Referencias")]
    public GuardianAudioModerator audioModerator;
    public Button button;
    public TextMeshProUGUI label;

    [Header("Textos")]
    public string textOn  = "MIC ACTIVO";
    public string textOff = "SILENCIADO";

    [Header("Colores")]
    public Color colorOn  = new Color32(30, 100, 220, 255);  // azul
    public Color colorOff = new Color32(180, 30, 30, 255);   // rojo

    void Start()
    {
        if (button == null) button = GetComponent<Button>();
        if (button == null) button = GetComponentInParent<Button>();
        if (button == null) { Debug.LogError("[MicToggle] No se encontró Button. Asígnalo en el Inspector."); return; }
        if (audioModerator == null) audioModerator = FindFirstObjectByType<GuardianAudioModerator>();
        // Strip any emoji from serialized scene values — LiberationSans SDF has no emoji glyphs.
        textOn  = System.Text.RegularExpressions.Regex.Replace(textOn,  @"\p{Cs}|\p{So}", "").Trim();
        textOff = System.Text.RegularExpressions.Regex.Replace(textOff, @"\p{Cs}|\p{So}", "").Trim();
        button.onClick.AddListener(Toggle);
        Refresh();
    }

    void Toggle()
    {
        if (audioModerator == null) return;
        audioModerator.muted = !audioModerator.muted;
        Debug.Log(audioModerator.muted ? "[Mic] Silenciado — no escuchando." : "[Mic] Activo — escuchando.");
        Refresh();
    }

    void Refresh()
    {
        bool on = audioModerator != null && !audioModerator.muted;
        if (label != null) label.text = on ? textOn : textOff;

        var colors = button.colors;
        colors.normalColor = on ? colorOn : colorOff;
        button.colors = colors;
    }
}
