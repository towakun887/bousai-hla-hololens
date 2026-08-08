using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleDriveImageLoader : MonoBehaviour
{
    public static GoogleDriveImageLoader Instance { get; private set; }

    [SerializeField, Min(1)] private int maxConcurrentDownloads = 2;
    [SerializeField, Min(64)] private int maxTextureDimension = 1024;
    [SerializeField, Min(1)] private int cacheSizeMegabytes = 96;
    [SerializeField, Range(0, 5)] private int maxRetries = 2;
    [SerializeField, Min(0.1f)] private float retryBaseDelay = 0.5f;

    public sealed class ImageRequest : IDisposable
    {
        internal GoogleDriveImageLoader Owner;
        internal string FileId;
        internal Action<Texture2D> Success;
        internal Action<string> Error;
        internal bool Active = true;
        internal bool Retained;

        public void Dispose()
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            Owner?.Release(this);
            Owner = null;
            Success = null;
            Error = null;
        }
    }

    private sealed class CacheEntry
    {
        public Texture2D Texture;
        public long EstimatedBytes;
        public int ReferenceCount;
        public LinkedListNode<string> LruNode;
    }

    private sealed class PendingDownload
    {
        public string FileId;
        public readonly List<ImageRequest> Subscribers = new List<ImageRequest>();
    }

    private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
    private readonly Dictionary<string, PendingDownload> inFlight = new Dictionary<string, PendingDownload>();
    private readonly Queue<PendingDownload> queue = new Queue<PendingDownload>();
    private readonly LinkedList<string> lru = new LinkedList<string>();
    private readonly HashSet<string> loggedFailures = new HashSet<string>();
    private long cacheBytes;
    private int activeDownloads;

    public int ActiveDownloadCount => activeDownloads;
    public long CacheBytes => cacheBytes;

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

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }
        Instance = null;
        foreach (CacheEntry entry in cache.Values)
        {
            if (entry.Texture != null)
            {
                Destroy(entry.Texture);
            }
        }
        cache.Clear();
        lru.Clear();
        inFlight.Clear();
        queue.Clear();
        cacheBytes = 0;
    }

    public ImageRequest LoadImage(string fileId, Action<Texture2D> onSuccess, Action<string> onError)
    {
        ImageRequest request = new ImageRequest
        {
            Owner = this,
            FileId = fileId,
            Success = onSuccess,
            Error = onError
        };

        if (string.IsNullOrEmpty(fileId))
        {
            request.Active = false;
            request.Error?.Invoke("File ID is empty");
            return request;
        }

        if (cache.TryGetValue(fileId, out CacheEntry cached) && cached.Texture != null)
        {
            Touch(fileId, cached);
            cached.ReferenceCount++;
            request.Retained = true;
            request.Success?.Invoke(cached.Texture);
            return request;
        }

        if (!inFlight.TryGetValue(fileId, out PendingDownload pending))
        {
            pending = new PendingDownload { FileId = fileId };
            inFlight.Add(fileId, pending);
            queue.Enqueue(pending);
        }
        pending.Subscribers.Add(request);
        PumpQueue();
        return request;
    }

    public void ClearUnusedCache()
    {
        LinkedListNode<string> node = lru.Last;
        while (node != null)
        {
            LinkedListNode<string> previous = node.Previous;
            if (cache.TryGetValue(node.Value, out CacheEntry entry) && entry.ReferenceCount == 0)
            {
                Evict(node.Value, entry);
            }
            node = previous;
        }
    }

    private void PumpQueue()
    {
        while (activeDownloads < maxConcurrentDownloads && queue.Count > 0)
        {
            PendingDownload pending = queue.Dequeue();
            RemoveInactiveSubscribers(pending);
            if (pending.Subscribers.Count == 0)
            {
                inFlight.Remove(pending.FileId);
                continue;
            }
            activeDownloads++;
            StartCoroutine(DownloadCoroutine(pending));
        }
    }

    private IEnumerator DownloadCoroutine(PendingDownload pending)
    {
        Texture2D texture = null;
        string error = "Image download failed";

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            string url = $"https://drive.google.com/uc?export=download&id={pending.FileId}";
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                    break;
                }

                error = request.responseCode == 403
                    ? "403 Forbidden: Access denied."
                    : request.error;
            }

            if (attempt < maxRetries)
            {
                yield return new WaitForSecondsRealtime(retryBaseDelay * Mathf.Pow(2f, attempt));
            }
        }

        if (texture != null)
        {
            if (texture.width > maxTextureDimension || texture.height > maxTextureDimension)
            {
                Texture2D resized = ResizeTexture(texture, maxTextureDimension);
                Destroy(texture);
                texture = resized;
            }
            AddToCache(pending.FileId, texture);
            loggedFailures.Remove(pending.FileId);
            DeliverSuccess(pending, texture);
        }
        else
        {
            if (loggedFailures.Add(pending.FileId))
            {
                Debug.LogWarning($"Failed to load image {pending.FileId}: {error}");
            }
            DeliverError(pending, error);
        }

        inFlight.Remove(pending.FileId);
        activeDownloads--;
        PumpQueue();
    }

    private void DeliverSuccess(PendingDownload pending, Texture2D texture)
    {
        cache.TryGetValue(pending.FileId, out CacheEntry entry);
        for (int i = 0; i < pending.Subscribers.Count; i++)
        {
            ImageRequest subscriber = pending.Subscribers[i];
            if (!subscriber.Active)
            {
                continue;
            }
            subscriber.Retained = true;
            if (entry != null)
            {
                entry.ReferenceCount++;
            }
            subscriber.Success?.Invoke(texture);
        }
        pending.Subscribers.Clear();
        TrimCache();
    }

    private static void DeliverError(PendingDownload pending, string error)
    {
        for (int i = 0; i < pending.Subscribers.Count; i++)
        {
            ImageRequest subscriber = pending.Subscribers[i];
            if (subscriber.Active)
            {
                subscriber.Error?.Invoke(error);
            }
        }
        pending.Subscribers.Clear();
    }

    private void Release(ImageRequest request)
    {
        if (request.Retained && cache.TryGetValue(request.FileId, out CacheEntry entry))
        {
            entry.ReferenceCount = Mathf.Max(0, entry.ReferenceCount - 1);
            request.Retained = false;
            TrimCache();
        }
    }

    private void AddToCache(string fileId, Texture2D texture)
    {
        long estimatedBytes = (long)texture.width * texture.height * 4L;
        CacheEntry entry = new CacheEntry
        {
            Texture = texture,
            EstimatedBytes = estimatedBytes,
            LruNode = lru.AddFirst(fileId)
        };
        cache[fileId] = entry;
        cacheBytes += estimatedBytes;
    }

    private void Touch(string fileId, CacheEntry entry)
    {
        if (entry.LruNode != null)
        {
            lru.Remove(entry.LruNode);
        }
        entry.LruNode = lru.AddFirst(fileId);
    }

    private void TrimCache()
    {
        long limit = (long)cacheSizeMegabytes * 1024L * 1024L;
        LinkedListNode<string> node = lru.Last;
        while (cacheBytes > limit && node != null)
        {
            LinkedListNode<string> previous = node.Previous;
            if (cache.TryGetValue(node.Value, out CacheEntry entry) && entry.ReferenceCount == 0)
            {
                Evict(node.Value, entry);
            }
            node = previous;
        }
    }

    private void Evict(string fileId, CacheEntry entry)
    {
        cache.Remove(fileId);
        if (entry.LruNode != null)
        {
            lru.Remove(entry.LruNode);
        }
        cacheBytes -= entry.EstimatedBytes;
        if (entry.Texture != null)
        {
            Destroy(entry.Texture);
        }
    }

    private static void RemoveInactiveSubscribers(PendingDownload pending)
    {
        for (int i = pending.Subscribers.Count - 1; i >= 0; i--)
        {
            if (!pending.Subscribers[i].Active)
            {
                pending.Subscribers.RemoveAt(i);
            }
        }
    }

    private static Texture2D ResizeTexture(Texture2D source, int maxDimension)
    {
        float scale = Mathf.Min((float)maxDimension / source.width, (float)maxDimension / source.height);
        int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, false);
        resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        resized.Apply(false, true);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return resized;
    }
}
