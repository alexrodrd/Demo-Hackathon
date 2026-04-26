using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[System.Serializable]
public class NetMessage {
    public string type;
    public string from;
    public string text;
    public bool blocked;
}

public class ChatManager : MonoBehaviour {
    [Header("Referencias UI")]
    public TMP_InputField inputField;
    public Transform chatContent;
    public GameObject textPrefab;
    public ScrollRect scrollRect;

    [Header("Referencias Red")]
    public GuardianNetwork network;

    // Colors
    static readonly Color32 BubbleOwn    = new Color32(37, 211, 102, 255);  // WhatsApp green
    static readonly Color32 BubbleOther  = new Color32(50,  50,  60, 230);  // dark slate
    static readonly Color32 BubbleBlock  = new Color32(180, 30,  30, 220);  // red
    static readonly Color32 TextOwn      = new Color32(10,  30,  10, 255);
    static readonly Color32 TextOther    = new Color32(230, 230, 235, 255);
    static readonly Color32 TextBlock    = new Color32(255, 200, 200, 255);
    static readonly Color32 TimestampCol = new Color32(160, 160, 170, 200);

    string localPlayerId = "";

    void Start() {
        SetupLayoutGroup();
    }

    void SetupLayoutGroup() {
        if (chatContent == null) return;
        var vlg = chatContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(10, 10, 8, 8);
        vlg.childAlignment = TextAnchor.LowerLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = chatContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = chatContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public void SetLocalPlayerId(string id) {
        localPlayerId = id;
    }

    public async void SendMessage(string text) {
        if (string.IsNullOrEmpty(text)) return;
        if (network != null) await network.SendChatMessage(text);
        if (inputField != null) inputField.text = "";
    }

    public void HandleNetworkMessage(string json) {
        Debug.Log("[Chat] Procesando JSON: " + json);
        try {
            NetMessage msg = JsonUtility.FromJson<NetMessage>(json);
            
            // Si el servidor usa player_id en vez de from, lo corregimos
            string sender = string.IsNullOrEmpty(msg.from) ? "Desconocido" : msg.from;

            if (msg.type == "message") {
                if (msg.blocked) {
                    SpawnBubble("[CONTENIDO BLOQUEADO]", "Sistema", isOwn: false, blocked: true);
                } else {
                    bool isOwn = !string.IsNullOrEmpty(localPlayerId) && sender == localPlayerId;
                    SpawnBubble(msg.text, sender, isOwn: isOwn, blocked: false);
                }
            } else if (msg.type == "joined" || msg.type == "player_joined") {
                SpawnSystemLabel(sender + " se unió a la sala");
            }
        } catch (Exception e) {
            Debug.LogWarning("[Chat] Error al procesar: " + e.Message);
        }
    }

    void SpawnBubble(string text, string from, bool isOwn, bool blocked) {
        if (textPrefab == null || chatContent == null) return;

        // Prefab root may already have TextMeshProUGUI (a Graphic) — can't add Image to same GO.
        // Create a fresh wrapper GO for the bubble instead.
        GameObject bubbleGO = new GameObject("Bubble_" + from, typeof(RectTransform));
        bubbleGO.transform.SetParent(chatContent, false);

        // --- Configuración del Fondo (Burbuja) ---
        var img = bubbleGO.AddComponent<Image>();
        img.color = blocked ? BubbleBlock : (isOwn ? BubbleOwn : BubbleOther);
        img.type = Image.Type.Sliced;

        // --- Layout de la burbuja ---
        // No HorizontalLayoutGroup needed — bubble is a simple vertical stack with padding.
        var vlgBubble = bubbleGO.AddComponent<VerticalLayoutGroup>();
        vlgBubble.padding = new RectOffset(12, 12, 8, 8);
        vlgBubble.spacing = 2;
        vlgBubble.childControlWidth = true;
        vlgBubble.childControlHeight = true;
        vlgBubble.childForceExpandWidth = true;
        vlgBubble.childForceExpandHeight = false;

        // Only vertical fit — horizontal width is capped by LayoutElement below.
        var csf = bubbleGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Cap bubble width at 75% of chat panel width so text wraps instead of overflows.
        var le = bubbleGO.AddComponent<LayoutElement>();
        float panelWidth = (chatContent as RectTransform)?.rect.width ?? 400f;
        if (panelWidth < 10f) panelWidth = 400f;
        le.preferredWidth  = panelWidth * 0.75f;
        le.flexibleWidth   = 0f;

        var rt = bubbleGO.GetComponent<RectTransform>();
        rt.pivot = isOwn ? new Vector2(1, 1) : new Vector2(0, 1);

        // --- Contenido Interno (Vertical) ---
        GameObject inner = bubbleGO;  // bubble IS the vertical container now

        // 1. Nombre del remitente (si no es propio)
        if (!isOwn && !blocked) {
            var nameTmp = CreateText("SenderLabel", inner.transform);
            nameTmp.text = from;
            nameTmp.fontSize = 10;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.color = new Color32(100, 200, 255, 255);
        }

        // 2. El mensaje real
        var messageTmp = CreateText("MessageText", inner.transform);
        messageTmp.text = text;
        messageTmp.fontSize = 14;
        messageTmp.color = blocked ? TextBlock : (isOwn ? TextOwn : TextOther);
        messageTmp.enableWordWrapping = true;
        
        // Sombra suave para legibilidad
        ApplyShadow(messageTmp);

        // 3. Hora (Timestamp)
        var timeTmp = CreateText("Timestamp", inner.transform);
        timeTmp.text = DateTime.Now.ToString("HH:mm");
        timeTmp.fontSize = 9;
        timeTmp.color = TimestampCol;
        timeTmp.alignment = isOwn ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;

        ScrollToBottom();
    }

    TextMeshProUGUI CreateText(string name, Transform parent) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();

        if (textPrefab != null) {
            var prefabTmp = textPrefab.GetComponentInChildren<TextMeshProUGUI>();
            if (prefabTmp != null) tmp.font = prefabTmp.font;
        }

        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;

        // Let child expand to full bubble width so wrapping kicks in.
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;

        return tmp;
    }

    void SpawnSystemLabel(string text) {
        if (chatContent == null) return;
        var tmp = CreateText("SystemLabel", chatContent);
        tmp.text = "— " + text + " —";
        tmp.fontSize = 11;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        tmp.fontStyle = FontStyles.Italic;
        ScrollToBottom();
    }

    void ApplyShadow(TextMeshProUGUI tmp) {
        tmp.fontSharedMaterial = new Material(tmp.fontSharedMaterial);
        tmp.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        tmp.fontSharedMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.5f));
        tmp.fontSharedMaterial.SetFloat("_UnderlaySoftness", 0.1f);
    }

    void ScrollToBottom() {
        if (scrollRect != null) {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
