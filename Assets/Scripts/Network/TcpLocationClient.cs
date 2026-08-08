using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class TcpLocationClient : MonoBehaviour
{
    public static TcpLocationClient Instance { get; private set; }

    public Action<LocationData> OnLocationReceived;
    public Action<string> OnError;

    [SerializeField, Min(1)] private int maxMainThreadActionsPerFrame = 8;
    [SerializeField, Min(0.1f)] private float requestIntervalSeconds = 0.5f;
    [SerializeField, Min(0.1f)] private float maxReconnectDelaySeconds = 8f;
    [SerializeField] private bool verboseDevelopmentLogs;

    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private readonly object lifecycleLock = new object();
    private Thread clientThread;
    private CancellationTokenSource cancellation;
    private TcpClient activeClient;
    private int connectionGeneration;
    private string serverIp;
    private int serverPort;
    private string targetDeviceId;

    public bool IsRunning => cancellation != null && !cancellation.IsCancellationRequested;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        int processed = 0;
        while (processed < maxMainThreadActionsPerFrame && mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
            processed++;
        }
    }

    public void Connect(string ip, int port, string deviceId)
    {
        lock (lifecycleLock)
        {
            if (IsRunning && serverIp == ip && serverPort == port && targetDeviceId == deviceId)
                return;

            StopClientLocked();
            serverIp = ip;
            serverPort = port;
            targetDeviceId = deviceId;
            CancellationTokenSource source = new CancellationTokenSource();
            cancellation = source;
            int generation = ++connectionGeneration;
            clientThread = new Thread(() =>
            {
                try { ClientLoop(generation, source.Token); }
                finally { source.Dispose(); }
            })
            {
                IsBackground = true,
                Name = "Location TCP Client"
            };
            clientThread.Start();
        }
    }

    public void Disconnect()
    {
        lock (lifecycleLock)
        {
            StopClientLocked();
        }
    }

    private void StopClientLocked()
    {
        connectionGeneration++;
        cancellation?.Cancel();
        cancellation = null;
        try { activeClient?.Close(); }
        catch (Exception) { }
        activeClient = null;
        clientThread = null;
        while (mainThreadActions.TryDequeue(out _)) { }
    }

    private void ClientLoop(int generation, CancellationToken token)
    {
        byte[] receiveBuffer = new byte[4096];
        float reconnectDelay = 0.5f;
        string lastError = null;

        while (!token.IsCancellationRequested && generation == connectionGeneration)
        {
            bool receivedResponse = false;
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    if (!SetActiveClient(generation, client)) return;
                    client.Connect(serverIp, serverPort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        reconnectDelay = 0.5f;
                        while (!token.IsCancellationRequested && generation == connectionGeneration && client.Connected)
                        {
                            string requestText = $"GET_LOCATION ID:{targetDeviceId}\n";
                            byte[] request = Encoding.ASCII.GetBytes(requestText);
                            stream.Write(request, 0, request.Length);
                            LogVerbose($"[TCP] Sent request to {serverIp}:{serverPort}");

                            int bytesRead = stream.Read(receiveBuffer, 0, receiveBuffer.Length);
                            if (bytesRead <= 0) break;

                            string response = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
                            LocationData data = JsonUtility.FromJson<LocationData>(response);
                            mainThreadActions.Enqueue(() => OnLocationReceived?.Invoke(data));
                            receivedResponse = true;
                            lastError = null;

                            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(requestIntervalSeconds)))
                                break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested && exception.Message != lastError)
                {
                    lastError = exception.Message;
                    string capturedError = exception.Message;
                    mainThreadActions.Enqueue(() => OnError?.Invoke(capturedError));
                }
            }
            finally
            {
                ClearActiveClient(generation);
            }

            float waitSeconds = receivedResponse ? 0f : reconnectDelay;
            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(waitSeconds))) break;
            reconnectDelay = receivedResponse ? 0.5f : Mathf.Min(reconnectDelay * 2f, maxReconnectDelaySeconds);
        }
    }

    private bool SetActiveClient(int generation, TcpClient client)
    {
        lock (lifecycleLock)
        {
            if (generation != connectionGeneration || !IsRunning) return false;
            activeClient = client;
            return true;
        }
    }

    private void ClearActiveClient(int generation)
    {
        lock (lifecycleLock)
        {
            if (generation == connectionGeneration) activeClient = null;
        }
    }

    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogVerbose(string message)
    {
        if (verboseDevelopmentLogs && Debug.isDebugBuild) Debug.Log(message);
    }

    private void OnDestroy()
    {
        Disconnect();
        if (Instance == this) Instance = null;
    }
}
