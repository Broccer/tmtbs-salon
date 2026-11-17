using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Trade of MTBS sunucusu (/tmtbs/*). Jeton cihazda (WebGL'de tarayıcı deposu) saklanır.
/// </summary>
public class Api : MonoBehaviour
{
    public const string BaseUrl = "https://mutesebbis.tr/api/v1/game";
    const string TokenKey = "tmtbs_salon_token";

    public static Api I { get; private set; }

    public string Token { get; private set; }
    public bool HasToken => !string.IsNullOrEmpty(Token);

    void Awake()
    {
        I = this;
        try { Token = PlayerPrefs.GetString(TokenKey, ""); } catch { Token = ""; }
    }

    public void SetToken(string t)
    {
        Token = t ?? "";
        try { PlayerPrefs.SetString(TokenKey, Token); PlayerPrefs.Save(); } catch { }
    }

    public void Get(string path, Action<Dictionary<string, object>> ok, Action<string, long> fail)
    {
        StartCoroutine(Send(UnityWebRequest.Get(BaseUrl + path), ok, fail));
    }

    public void Post(string path, Dictionary<string, object> body, Action<Dictionary<string, object>> ok, Action<string, long> fail)
    {
        var req = new UnityWebRequest(BaseUrl + path, "POST");
        byte[] bytes = Encoding.UTF8.GetBytes(Json.Write(body ?? new Dictionary<string, object>()));
        req.uploadHandler = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        StartCoroutine(Send(req, ok, fail));
    }

    IEnumerator Send(UnityWebRequest req, Action<Dictionary<string, object>> ok, Action<string, long> fail)
    {
        using (req)
        {
            if (HasToken) req.SetRequestHeader("Authorization", "Bearer " + Token);
            req.SetRequestHeader("Accept-Language", "tr");
            req.timeout = 20;
            yield return req.SendWebRequest();

            var data = Json.Parse(req.downloadHandler != null ? req.downloadHandler.text : null) as Dictionary<string, object>;
            bool okFlag = data != null && J.Get(data, "ok") is bool b && b;
            if (req.result == UnityWebRequest.Result.Success && okFlag)
            {
                ok?.Invoke(data);
                yield break;
            }

            string msg = data != null ? J.S(data, "message") : "";
            if (string.IsNullOrEmpty(msg))
                msg = req.responseCode == 0 ? "Sunucuya ulaşılamadı. İnternet bağlantını kontrol et." : "Beklenmeyen bir hata oluştu.";
            fail?.Invoke(msg, req.responseCode);
        }
    }
}
