using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class TcpLocationClient : MonoBehaviour
{
    public static TcpLocationClient Instance { get; private set; }

    public Action<LocationData> OnLocationReceived;
    public Action<string> OnError;

    private string serverIp;
    private int serverPort;
    private string targetDeviceId;
    private bool isRunning = false;
    private Thread clientThread;
    
    private Queue<Action> mainThreadActions = new Queue<Action>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }
    }

    public void Connect(string ip, int port, string deviceId)
    {
        if (isRunning) return;
        
        serverIp = ip;
        serverPort = port;
        targetDeviceId = deviceId;
        isRunning = true;
        
        clientThread = new Thread(ClientLoop);
        clientThread.IsBackground = true;
        clientThread.Start();
    }

    public void Disconnect()
    {
        isRunning = false;
        // 意図的にJoinしません。メインスレッド（Unity）をブロックして深刻なラグ（カクつき）を発生させるためです。
        // スレッドは次のループで isRunning == false を検知して自然消滅します。
    }

    private void ClientLoop()
    {
        while (isRunning)
        {
            try
            {
                using (TcpClient client = new TcpClient(serverIp, serverPort))
                using (NetworkStream stream = client.GetStream())
                {
                    // Send the request in the exact format the server expects (with ID and newline)
                    string requestString = $"GET_LOCATION ID:{targetDeviceId}\n";
                    byte[] requestBytes = Encoding.ASCII.GetBytes(requestString);
                    stream.Write(requestBytes, 0, requestBytes.Length);
                    Debug.Log($"[TCP] Sent Request: {requestString.TrimEnd()} to {serverIp}:{serverPort}");

                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Debug.Log($"[TCP] Received Raw Response: \n{response}");
                        
                        LocationData data = JsonUtility.FromJson<LocationData>(response);
                        Debug.Log($"[TCP] Parsed LocationData: Lat={data.latitude}, Lon={data.longitude}, Alt={data.altitude}, Acc={data.accuracy}");
                        
                        lock (mainThreadActions)
                        {
                            mainThreadActions.Enqueue(() => {
                                OnLocationReceived?.Invoke(data);
                            });
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[TCP] Connection closed by server without response.");
                    }
                }
            }
            catch (Exception e)
            {
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() => {
                        OnError?.Invoke(e.Message);
                    });
                }
            }
            
            Thread.Sleep(500);
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
