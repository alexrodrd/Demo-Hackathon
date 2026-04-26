using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
class JoinPayload {
    public string type = "join";
    public string room;
    public string player_id;
    public string game_id;
}

[Serializable]
class TextPayload {
    public string type = "message";
    public string text;
}

public class GuardianNetwork : MonoBehaviour {
    private ClientWebSocket websocket;

    [Header("Configuración de Red")]
    [Tooltip("ws para LAN, wss para ngrok/HTTPS")]
    public string protocol = "wss";
    [Tooltip("Host sin puerto. Ej: astride-graded-paralegal.ngrok-free.dev")]
    public string serverIp = "astride-graded-paralegal.ngrok-free.dev";
    [Tooltip("Vacío o 443 para wss en ngrok. 8888 para LAN.")]
    public string serverPort = "";
    public string roomId = "sala-demo-unity";
    public string role = "";
    public string gameId = "unity-demo";
    public string playerId = "Player-Unity";

    [Header("Referencias")]
    public ChatManager chatManager;

    private readonly Queue<string> messageQueue = new Queue<string>();
    private CancellationTokenSource cts;
    private const float HeartbeatInterval = 20f;
    private float heartbeatTimer = 0f;

    async void Start() {
        if (chatManager == null) chatManager = GetComponent<ChatManager>();
        if (chatManager == null) chatManager = FindFirstObjectByType<ChatManager>();

        playerId = "User_" + UnityEngine.Random.Range(100, 999);
        if (chatManager != null) chatManager.SetLocalPlayerId(playerId);
        cts = new CancellationTokenSource();
        await Connect();
    }

    void Update() {
        lock (messageQueue) {
            while (messageQueue.Count > 0) {
                string msg = messageQueue.Dequeue();
                if (chatManager != null) chatManager.HandleNetworkMessage(msg);
            }
        }

        if (websocket != null && websocket.State == WebSocketState.Open) {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= HeartbeatInterval) {
                heartbeatTimer = 0f;
                _ = SendRaw("{\"type\":\"ping\"}");
            }
        }
    }

    async Task Connect() {
        websocket = new ClientWebSocket();
        try {
            string host;
            if (string.IsNullOrEmpty(serverPort) || serverPort == "80" || serverPort == "443") {
                host = $"{protocol}://{serverIp}";
            } else {
                host = $"{protocol}://{serverIp}:{serverPort}";
            }
            string suffix = string.IsNullOrEmpty(role) ? "" : "/" + role;
            string url = $"{host}/ws/game/{roomId}{suffix}";

            Debug.Log("[Network] Conectando a: " + url);
            await websocket.ConnectAsync(new Uri(url), cts.Token);

            var join = new JoinPayload { room = roomId, player_id = playerId, game_id = gameId };
            await SendRaw(JsonUtility.ToJson(join));

            Debug.Log("[Network] ¡CONEXIÓN ESTABLECIDA!");
            _ = ReceiveLoop();
        } catch (Exception e) {
            Debug.LogError("[Network] Error: " + e.Message);
        }
    }

    async Task ReceiveLoop() {
        var buffer = new byte[1024 * 8];
        var ms = new MemoryStream();
        try {
            while (websocket.State == WebSocketState.Open) {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do {
                    result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string json = Encoding.UTF8.GetString(ms.ToArray());
                lock (messageQueue) { messageQueue.Enqueue(json); }
            }
        } catch (Exception e) {
            Debug.LogWarning("[Network] Receive ended: " + e.Message);
        }
    }

    public async Task SendChatMessage(string text) {
        if (websocket == null || websocket.State != WebSocketState.Open) return;
        var payload = new TextPayload { text = text };
        await SendRaw(JsonUtility.ToJson(payload));
    }

    private async Task SendRaw(string data) {
        var buffer = Encoding.UTF8.GetBytes(data);
        await websocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
    }

    async void OnDestroy() {
        cts?.Cancel();
        if (websocket != null) {
            if (websocket.State == WebSocketState.Open) {
                try { await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
                catch { }
            }
            websocket.Dispose();
        }
    }
}
