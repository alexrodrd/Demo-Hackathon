using UnityEngine;
using TMPro;

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
    
    [Header("Referencias Red")]
    public GuardianNetwork network;

    public async void SendMessage(string text) {
        if (string.IsNullOrEmpty(text)) return;
        if (network != null) await network.SendChatMessage(text);
        if (inputField != null) inputField.text = ""; 
    }

    public void HandleNetworkMessage(string json) {
        Debug.Log("[Chat] Procesando JSON: " + json);
        try {
            // Intentamos parsear con el sistema de Unity
            NetMessage msg = JsonUtility.FromJson<NetMessage>(json);
            
            if (msg.type == "message") {
                if (!msg.blocked) {
                    AddMessageToUI($"<b>{msg.from}:</b> {msg.text}");
                } else {
                    AddMessageToUI("<color=red>[BLOQUEADO]</color>");
                }
            }
            else if (msg.type == "joined") {
                AddMessageToUI("<color=grey><i>Te has unido a la sala</i></color>");
            }
            else if (msg.type == "player_joined") {
                AddMessageToUI($"<color=grey><i>{msg.from} entró</i></color>");
            }
        } catch (System.Exception e) {
            Debug.LogWarning("[Chat] No se pudo procesar este mensaje: " + e.Message);
        }
    }

    private void AddMessageToUI(string msg) {
        if (textPrefab == null || chatContent == null) return;
        // IMPORTANTE: Esto debe correr en el hilo principal (GuardianNetwork ya lo asegura con la cola)
        GameObject newText = Instantiate(textPrefab, chatContent);
        newText.GetComponent<TextMeshProUGUI>().text = msg;
        
        // Auto-scroll al final (opcional)
        Canvas.ForceUpdateCanvases();
    }
}