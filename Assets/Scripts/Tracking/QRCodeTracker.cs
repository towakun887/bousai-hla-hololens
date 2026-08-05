using UnityEngine;
using System;
using System.Linq;
using Microsoft.MixedReality.OpenXR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class QRCodeTracker : MonoBehaviour
{
    public static QRCodeTracker Instance { get; private set; }

    public Action<string, int, string, Vector3, Quaternion> OnQRCodeDetected;
    public Action OnQRCodeLost;

    [Header("Mock Settings")]
    [SerializeField] private bool useMockInEditor = false;
    [SerializeField] private string mockIp = "127.0.0.1";
    [SerializeField] private int mockPort = 5000;
    [SerializeField] private string mockDeviceId = "MockDevice";

    public bool IsMockMode => useMockInEditor;

    private bool isTracking = false;
    private ARMarker currentActiveMarker;

    private string currentIp = "";
    private int currentPort = 5000;
    private string currentDeviceId = "";

    public bool IsQRCodeLocated => isTracking;
    public bool IsQRCodeDetected => currentActiveMarker != null || isTracking;

    public string DebugStatus { get; private set; } = "Initializing...";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (useMockInEditor)
        {
            Debug.Log("QRCodeTracker: Using Mock in Editor.");
            DebugStatus = "Mock Mode (Editor)";
            Invoke(nameof(SimulateQRDetect), 1.0f); // Auto-trigger mock detection after 1 second
            return;
        }
#endif

        if (ARMarkerManager.Instance == null)
        {
            DebugStatus = "Error: ARMarkerManager not found in scene!";
            Debug.LogError("QRCodeTracker: ARMarkerManager instance is null. Please add ARMarkerManager to your XR Origin.");
            return;
        }

        ARMarkerManager.Instance.markersChanged += OnMarkersChanged;
        DebugStatus = "Started. Waiting for QR...";
    }

    private void OnDestroy()
    {
        if (ARMarkerManager.Instance != null)
        {
            ARMarkerManager.Instance.markersChanged -= OnMarkersChanged;
        }
    }

    public void SimulateQRDetect()
    {
#if UNITY_EDITOR
        if (useMockInEditor)
        {
            isTracking = true;
            Vector3 pos = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 1.0f : Vector3.forward;
            Quaternion rot = Quaternion.identity;
            
            OnQRCodeDetected?.Invoke(mockIp, mockPort, mockDeviceId, pos, rot);
        }
#endif
    }
    
    public void SimulateQRLost()
    {
#if UNITY_EDITOR
        isTracking = false;
        OnQRCodeLost?.Invoke();
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (useMockInEditor) return;
#endif

        if (currentActiveMarker != null)
        {
            // ARMarkerManager automatically updates the Transform of the ARMarker GameObject.
            // If the tracking state is tracking, we can use its position.
            if (currentActiveMarker.trackingState == TrackingState.Tracking)
            {
                if (!isTracking)
                {
                    DebugStatus = "Successfully located QR code in 3D space.";
                    Debug.Log("Successfully located QR code in 3D space.");
                }
                isTracking = true;

                // Fire the event continuously so that the UI can stick to the QR code perfectly.
                OnQRCodeDetected?.Invoke(currentIp, currentPort, currentDeviceId, currentActiveMarker.transform.position, currentActiveMarker.transform.rotation);
            }
            else
            {
                if (isTracking)
                {
                    DebugStatus = "Lost 3D position of QR code.";
                }
                isTracking = false;
            }
        }
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        // Handle added or updated markers
        foreach (var marker in args.added.Concat(args.updated))
        {
            // ARMarkerManager might track different types of markers, ensure it's a QR code
            if (marker.markerType == ARMarkerType.QRCode)
            {
                if (currentActiveMarker != marker)
                {
                    string decodedString = ARMarkerManager.Instance.GetDecodedString(marker.trackableId);
                    if (!string.IsNullOrEmpty(decodedString) && ParseQRData(decodedString))
                    {
                        currentActiveMarker = marker;
                        DebugStatus = $"Parsed QR: {decodedString}";
                        Debug.Log("QRCodeTracker: Parsed new QR code data.");
                    }
                }
            }
        }

        // Handle removed markers
        foreach (var marker in args.removed)
        {
            if (currentActiveMarker != null && marker.trackableId == currentActiveMarker.trackableId)
            {
                currentActiveMarker = null;
                isTracking = false;
                OnQRCodeLost?.Invoke();
                DebugStatus = "QR Code Lost (Removed).";
            }
        }
    }

    private bool ParseQRData(string data)
    {
        try 
        {
            string ip = "";
            int port = 5000;
            string deviceId = "";

            string[] parts = data.Split(',');
            if (parts.Length >= 1)
            {
                string[] ipPort = parts[0].Split(':');
                if (ipPort.Length == 2)
                {
                    ip = ipPort[0];
                    int.TryParse(ipPort[1], out port);
                }
            }
            if (parts.Length >= 2)
            {
                string[] idPart = parts[1].Split(':');
                if (idPart.Length == 2 && idPart[0] == "ID")
                {
                    deviceId = idPart[1];
                }
            }

            // Only update if we successfully parsed an IP
            if (!string.IsNullOrEmpty(ip))
            {
                currentIp = ip;
                currentPort = port;
                currentDeviceId = deviceId;
                return true;
            }
            return false;
        }
        catch(Exception e)
        {
            Debug.LogError("Failed to parse QR code: " + e.Message);
            return false;
        }
    }
}
