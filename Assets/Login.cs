using System;
using System.Collections;
using System.Collections.Generic;
using Ricimi;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Login : MonoBehaviour
{
    [SerializeField]
    public TMP_InputField username;
    [SerializeField]
    public TMP_InputField password;
    [SerializeField]
    private PopupOpener popupOpener; // PopupOpener脚本的引用
    // Start is called before the first frame update
    public void LoginFunc()
    {
        User user = new User();
        user.username = username.text;
        user.password = password.text;
        StartCoroutine(PostRequest("https://inteplatform.daruigene.cn:8088/api/Account/Login", JsonUtility.ToJson(user)));
    }
    
    IEnumerator PostRequest(string uri, string json)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(uri, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            webRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(webRequest.error);
            }
            else
            {
                var response = JsonUtility.FromJson<LoginResponse>(webRequest.downloadHandler.text);
                if (response != null && response.success)
                {
                    // 登录成功，调用PopupOpener的OpenPopup方法
                    popupOpener.OpenPopup();
                }
            }
        }
    }
    
}

[Serializable]
public class User
{
    public string username;
    public string password;
}

[Serializable]
public class LoginResponse
{
    public string message;
    public bool success;
    public int? errorCode;
    public LoginData data;
}

[Serializable]
public class LoginData
{
    public int userId;
    public string userName;
    public bool isDr;
    public List<int> roleIds; // Assuming role IDs are integers
    public Token token;
}

[Serializable]
public class Token
{
    public string token;
    public string refreshToken;
}