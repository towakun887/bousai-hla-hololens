using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

public class GoogleDriveImageLoader : MonoBehaviour
{
    public static GoogleDriveImageLoader Instance { get; private set; }

    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadImage(string fileId, Action<Texture2D> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            onError?.Invoke("File ID is empty");
            return;
        }

        if (textureCache.TryGetValue(fileId, out Texture2D tex))
        {
            onSuccess?.Invoke(tex);
            return;
        }

        StartCoroutine(DownloadImageCoroutine(fileId, onSuccess, onError));
    }

    private IEnumerator DownloadImageCoroutine(string fileId, Action<Texture2D> onSuccess, Action<string> onError)
    {
        string url = $"https://drive.google.com/uc?export=download&id={fileId}";
        
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = www.error;
                if (www.responseCode == 403)
                {
                    errorMsg = "403 Forbidden: Access denied.";
                }
                Debug.LogWarning($"Failed to load image {fileId}: {errorMsg}");
                onError?.Invoke(errorMsg);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(www);
                textureCache[fileId] = texture;
                onSuccess?.Invoke(texture);
            }
        }
    }
}
