using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MmorpgClient.Net
{
    /// <summary>
    /// Result of POST /api/assign-gate.
    /// Mirrors the Java gateway response shape.
    /// </summary>
    [Serializable]
    public class AssignGateResult
    {
        public string gate_ip;
        public uint gate_port;
        // base64-encoded by Jackson when these come from byte[] fields.
        public string token_payload;
        public string token_signature;
        public string error;
    }

    [Serializable]
    public class ServerListZone
    {
        public uint zone_id;
        public string name;
        public string status;
        public string load_level;
        public string maintenance_msg;
        public long open_time;
        public bool is_new;
        public bool recommended;
    }

    [Serializable]
    public class ServerListResponse
    {
        public ServerListZone[] zones;
    }

    [Serializable]
    public class AssignGateRequest
    {
        public uint zone_id;
    }

    [Serializable]
    public class AnnouncementItem
    {
        public long id;
        public string title;
        public string content;
        public string type;
        public long start_time;
        public long end_time;
    }

    [Serializable]
    public class AnnouncementResponse
    {
        public AnnouncementItem[] items;
    }

    /// <summary>
    /// Talks to the Java gateway HTTP API. Used once before the TCP connection
    /// to obtain (a) which zone to use and (b) the gate address + signed token.
    /// </summary>
    public sealed class GatewayHttpClient
    {
        private readonly string _baseUrl;
        public GatewayHttpClient(string baseUrl) { _baseUrl = baseUrl.TrimEnd('/'); }

        public IEnumerator GetServerList(Action<ServerListResponse> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get(_baseUrl + "/api/server-list");
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError($"GET /api/server-list failed: {req.error}");
                yield break;
            }
            try
            {
                var parsed = JsonUtility.FromJson<ServerListResponse>(req.downloadHandler.text);
                onSuccess(parsed);
            }
            catch (Exception ex) { onError($"parse server-list: {ex.Message}"); }
        }

        public IEnumerator GetAnnouncements(Action<AnnouncementResponse> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get(_baseUrl + "/api/announcement");
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError($"GET /api/announcement failed: {req.error}");
                yield break;
            }

            try
            {
                var parsed = JsonUtility.FromJson<AnnouncementResponse>(req.downloadHandler.text);
                onSuccess(parsed);
            }
            catch (Exception ex) { onError($"parse announcement: {ex.Message}"); }
        }

        public IEnumerator AssignGate(uint zoneId, Action<AssignGateResult> onSuccess, Action<string> onError)
        {
            string body = JsonUtility.ToJson(new AssignGateRequest { zone_id = zoneId });
            using var req = new UnityWebRequest(_baseUrl + "/api/assign-gate", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;

            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError($"POST /api/assign-gate failed: {req.error}");
                yield break;
            }

            AssignGateResult parsed;
            try { parsed = JsonUtility.FromJson<AssignGateResult>(req.downloadHandler.text); }
            catch (Exception ex) { onError($"parse assign-gate: {ex.Message}"); yield break; }

            if (parsed == null) { onError("assign-gate: empty response"); yield break; }
            if (!string.IsNullOrEmpty(parsed.error)) { onError($"assign-gate: {parsed.error}"); yield break; }
            if (string.IsNullOrEmpty(parsed.gate_ip) || parsed.gate_port == 0)
            {
                onError("assign-gate: empty gate address");
                yield break;
            }
            onSuccess(parsed);
        }
    }
}
