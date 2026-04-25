using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class GuardianNetwork : MonoBehaviour {
    private ClientWebSocket websocket;
    public string roomId = "sala-demo-unity";
    public string playerId = "Player-01";
    
    private ChatManager chatManager;
    private Queue<string> messageQueue = new Queue<string>();

    async void Start() {
        chatManager = GetComponent<ChatManager>();
        playerId = "User_" + UnityEngine.Random.Range(100, 999);
        await Connect();
    }

    void Update() {
        // Procesar mensajes en el hilo principal de Unity
        lock (messageQueue) {
            while (messageQueue.Count > 0) {
                string msg = messageQueue.Dequeue();
                chatManager.HandleNetworkMessage(msg);
            }
        }
    }

    async Task Connect() {
        websocket = new ClientWebSocket();
        try {
            await websocket.ConnectAsync(new Uri("ws://localhost:8888/ws/game/" + roomId), CancellationToken.None);
            var joinMessage = $"{{\"type\": \"join\", \"room\": \"{roomId}\", \"player_id\": \"{playerId}\", \"game_id\": \"demo\"}}";
            await SendRaw(joinMessage);
            _ = ReceiveLoop();
        } catch (Exception e) {
            Debug.LogError("[Network] Error: " + e.Message);
        }
    }

    async Task ReceiveLoop() {
        var buffer = new byte[1024 * 8];
        try {
            while (websocket.State == WebSocketState.Open) {
                var result = await websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Meter a la cola para procesar en Update()
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
}