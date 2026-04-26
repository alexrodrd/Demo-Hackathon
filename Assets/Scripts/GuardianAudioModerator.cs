using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// EXPERIMENTAL — quitar: elimina este script del GO y listo, sin efectos secundarios.
public class GuardianAudioModerator : MonoBehaviour
{
    [Header("STT — Deepgram")]
    public string sttApiKey = "";   // pegar key en Inspector, no en código
    public int recordDurationSec = 5;

    [Header("Referencias")]
    public GuardianNetwork network;  // asignar en Inspector

    [HideInInspector] public bool muted = false;

    private string micName;
    private AudioClip recordingClip;
    private bool active = false;

    void Start()
    {
        if (string.IsNullOrEmpty(sttApiKey)) {
            Debug.LogWarning("[Audio] sttApiKey vacío — módulo desactivado.");
            return;
        }
        if (Microphone.devices.Length == 0) {
            Debug.LogError("[Audio] Sin micrófono detectado.");
            return;
        }
        if (network == null) network = FindFirstObjectByType<GuardianNetwork>();
        micName = Microphone.devices[0];
        active = true;
        Debug.Log($"[Audio] Usando micrófono: {micName}");
        StartRecording();
    }

    void StartRecording()
    {
        if (!active) return;
        recordingClip = Microphone.Start(micName, false, recordDurationSec, 16000);
        StartCoroutine(ProcessAfterDelay());
    }

    IEnumerator ProcessAfterDelay()
    {
        yield return new WaitForSeconds(recordDurationSec);
        Microphone.End(micName);

        byte[] wav = WavUtility.FromAudioClip(recordingClip);
        if (!muted && wav != null && wav.Length > 44)
            _ = TranscribeAsync(wav);

        StartRecording();
    }

    async Task TranscribeAsync(byte[] wav)
    {
        string url = "https://api.deepgram.com/v1/listen?model=nova-2&language=es";
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

        StartCoroutine(PostWav(url, wav, tcs));
        string json = await tcs.Task;
        if (json == null) return;

        string transcript = ParseDeepgram(json);
        if (string.IsNullOrWhiteSpace(transcript)) return;

        Debug.Log($"[Audio] Transcript: {transcript}");
        if (network != null)
            await network.SendAudioTranscript(transcript);
    }

    IEnumerator PostWav(string url, byte[] wav, System.Threading.Tasks.TaskCompletionSource<string> tcs)
    {
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(wav);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "audio/wav");
        req.SetRequestHeader("Authorization", $"Token {sttApiKey}");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) {
            Debug.LogWarning($"[Audio] STT error: {req.error}");
            tcs.SetResult(null);
        } else {
            tcs.SetResult(req.downloadHandler.text);
        }
    }

    [Serializable]
    class DGAlternative { public string transcript; }
    [Serializable]
    class DGChannel    { public DGAlternative[] alternatives; }
    [Serializable]
    class DGResults    { public DGChannel[] channels; }
    [Serializable]
    class DGResponse   { public DGResults results; }

    static string ParseDeepgram(string json)
    {
        // Deepgram: {"results":{"channels":[{"alternatives":[{"transcript":"..."}]}]}}
        try {
            var r = JsonUtility.FromJson<DGResponse>(json);
            return r?.results?.channels?[0]?.alternatives?[0]?.transcript?.Trim() ?? "";
        } catch { return ""; }
    }

    void OnDestroy() { active = false; }
}

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null) return null;
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        var stream = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(stream);
        int dataLen = samples.Length * 2;

        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLen);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16); w.Write((short)1);
        w.Write((short)clip.channels);
        w.Write(clip.frequency);
        w.Write(clip.frequency * clip.channels * 2);
        w.Write((short)(clip.channels * 2));
        w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataLen);

        foreach (float s in samples) {
            short v = (short)Mathf.Clamp(s * 32767f, -32768f, 32767f);
            w.Write(v);
        }
        return stream.ToArray();
    }
}
