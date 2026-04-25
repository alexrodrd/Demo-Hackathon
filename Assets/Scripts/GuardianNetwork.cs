using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class GuardianNetwork : MonoBehaviour {
    private ClientWebSocket websocket;
    
    [Header("Configuración de Red")]
    public string roomId = "sala-demo";
    public string playerId = "Player-Unity";
    public string serverIp = "192.168.1.172";
    public string serverPort = "8000";
    
    public ChatManager chatManager;
    private Queue<string> messageQueue = new Queue<string>();

    async void Start() {
        if (chatManager == null) chatManager = GetComponent<ChatManager>();
        if (chatManager == null) chatManager = FindFirstObjectByType<ChatManager>();
        
        playerId = "User_" + UnityEngine.Random.Range(100, 999);
        await Connect();
    }

    void Update() {
        lock (messageQueue) {
            while (messageQueue.Count > 0) {
                string msg = messageQueue.Dequeue();
                if (chatManager != null) {
                    chatManager.HandleNetworkMessage(msg);
                } else {
                    Debug.LogWarning("[Network] No hay ChatManager asignado.");
                }
            }
        }
    }

    async Task Connect() {
        websocket = new ClientWebSocket();
        try {
            string url = $"ws://{serverIp}:{serverPort}/ws/game/{roomId}";
            Debug.Log("[Network] Conectando a: " + url);
            await websocket.ConnectAsync(new Uri(url), CancellationToken.None);
            
            var joinMessage = $"{{\"type\": \"join\", \"room\": \"{roomId}\", \"player_id\": \"{playerId}\", \"game_id\": \"demo\"}}";
            await SendRaw(joinMessage);
            
            Debug.Log("[Network] ¡Conectado con éxito a la Mac externa!");
            _ = ReceiveLoop();
        } catch (Exception e) {
            Debug.LogError("[Network] Error de conexión: " + e.Message);
        }
    }

    async Task ReceiveLoop() {
        var buffer = new byte[1024 * 8];
        try {
            while (websocket.State == WebSocketState.Open) {
                var result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                lock (messageQueue) {
                    messageQueue.Enqueue(json);
                }
            }
        } catch (Exception) {}
    }

    public async Task SendChatMessage(string text) {
        if (websocket?.State != WebSocketState.Open) return;
        var jsonMessage = $"{{\"type\": \"message\", \"text\": \"{text}\"}}";
        await SendRaw(jsonMessage);
    }

    private async Task SendRaw(string data) {
        var buffer = Encoding.UTF8.GetBytes(data);
        await websocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void OnDestroy() {
        if (websocket != null) websocket.Dispose();
    }
}