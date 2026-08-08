using System;
using Microsoft.MixedReality.OpenXR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class QRCodeTracker : MonoBehaviour
{
    public static QRCodeTracker Instance { get; private set; }

    public Action<string, int, string, Vector3, Quaternion> OnQRCodeDetected;
    public Action OnQRCodeLost;
    public Action OnStatusChanged;

    [Header("Tracking Timing")]
    [SerializeField, Min(0.02f)] private float notificationInterval = 0.1f;
    [SerializeField, Min(0f)] private float lostGracePeriod = 0.5f;

    [Header("Mock Settings")]
    [SerializeField] private bool useMockInEditor;
    [SerializeField] private string mockIp = "127.0.0.1";
    [SerializeField] private int mockPort = 5000;
    [SerializeField] private string mockDeviceId = "MockDevice";

    private enum TrackingPhase
    {
        Searching,
        Tracking,
        TemporarilyLost,
        Lost
    }

    private TrackingPhase phase = TrackingPhase.Searching;
    private ARMarker currentActiveMarker;
    private Camera mainCamera;
    private float lastNotificationTime = float.NegativeInfinity;
    private float temporarilyLostAt;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation = Quaternion.identity;
    private string currentIp = string.Empty;
    private int currentPort = 5000;
    private string currentDeviceId = string.Empty;
    private string debugStatus = "Initializing...";

    public bool IsMockMode => useMockInEditor;
    public bool IsQRCodeLocated => phase == TrackingPhase.Tracking;
    public bool IsQRCodeDetected => currentActiveMarker != null || phase == TrackingPhase.Tracking || phase == TrackingPhase.TemporarilyLost;
    public string DebugStatus => debugStatus;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            mainCamera = Camera.main;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (useMockInEditor)
        {
            SetDebugStatus("Mock Mode (Editor)");
            Invoke(nameof(SimulateQRDetect), 1f);
            return;
        }
#endif

        if (ARMarkerManager.Instance == null)
        {
            SetDebugStatus("Error: ARMarkerManager not found in scene!");
            Debug.LogError("QRCodeTracker: ARMarkerManager instance is null. Please add ARMarkerManager to your XR Origin.");
            return;
        }

        ARMarkerManager.Instance.markersChanged += OnMarkersChanged;
        SetDebugStatus("Started. Waiting for QR...");
    }

    private void OnDestroy()
    {
        CancelInvoke();
        if (ARMarkerManager.Instance != null)
        {
            ARMarkerManager.Instance.markersChanged -= OnMarkersChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (useMockInEditor)
        {
            if (phase == TrackingPhase.Tracking)
            {
                NotifyTrackingIfDue(false);
            }
            else if (phase == TrackingPhase.TemporarilyLost)
            {
                UpdateTemporaryLoss();
            }
            return;
        }
#endif

        if (currentActiveMarker == null)
        {
            return;
        }

        if (currentActiveMarker.trackingState == TrackingState.Tracking)
        {
            lastValidPosition = currentActiveMarker.transform.position;
            lastValidRotation = currentActiveMarker.transform.rotation;
            bool recovered = phase != TrackingPhase.Tracking;
            if (recovered)
            {
                phase = TrackingPhase.Tracking;
                SetDebugStatus("Successfully located QR code in 3D space.");
            }
            NotifyTrackingIfDue(recovered);
        }
        else if (phase == TrackingPhase.Tracking)
        {
            phase = TrackingPhase.TemporarilyLost;
            temporarilyLostAt = Time.unscaledTime;
            SetDebugStatus("QR tracking temporarily unavailable.");
        }
        else if (phase == TrackingPhase.TemporarilyLost)
        {
            UpdateTemporaryLoss();
        }
    }

    public void SimulateQRDetect()
    {
#if UNITY_EDITOR
        if (!useMockInEditor)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        lastValidPosition = mainCamera != null
            ? mainCamera.transform.position + mainCamera.transform.forward
            : Vector3.forward;
        lastValidRotation = Quaternion.identity;
        phase = TrackingPhase.Tracking;
        SetDebugStatus("Mock QR tracking");
        NotifyTrackingIfDue(true);
#endif
    }

    public void SimulateTemporaryTrackingLoss()
    {
#if UNITY_EDITOR
        if (useMockInEditor && phase == TrackingPhase.Tracking)
        {
            phase = TrackingPhase.TemporarilyLost;
            temporarilyLostAt = Time.unscaledTime;
            SetDebugStatus("Mock QR temporarily lost");
        }
#endif
    }

    public void SimulateQRLost()
    {
#if UNITY_EDITOR
        if (useMockInEditor)
        {
            EnterLost("Mock QR lost");
        }
#endif
    }

    private void NotifyTrackingIfDue(bool force)
    {
        float now = Time.unscaledTime;
        if (!force && now - lastNotificationTime < notificationInterval)
        {
            return;
        }

        lastNotificationTime = now;
        OnQRCodeDetected?.Invoke(currentIp.Length > 0 ? currentIp : mockIp,
            currentIp.Length > 0 ? currentPort : mockPort,
            currentDeviceId.Length > 0 ? currentDeviceId : mockDeviceId,
            lastValidPosition,
            lastValidRotation);
    }

    private void UpdateTemporaryLoss()
    {
        if (Time.unscaledTime - temporarilyLostAt >= lostGracePeriod)
        {
            EnterLost("QR Code Lost.");
        }
    }

    private void EnterLost(string status)
    {
        if (phase == TrackingPhase.Lost || phase == TrackingPhase.Searching)
        {
            return;
        }

        phase = TrackingPhase.Lost;
        SetDebugStatus(status);
        OnQRCodeLost?.Invoke();
    }

    private void OnMarkersChanged(ARMarkersChangedEventArgs args)
    {
        foreach (ARMarker marker in args.added)
        {
            ProcessMarker(marker);
        }
        foreach (ARMarker marker in args.updated)
        {
            ProcessMarker(marker);
        }
        foreach (ARMarker marker in args.removed)
        {
            if (currentActiveMarker != null && marker.trackableId == currentActiveMarker.trackableId)
            {
                currentActiveMarker = null;
                EnterLost("QR Code Lost (Removed).");
            }
        }
    }

    private void ProcessMarker(ARMarker marker)
    {
        if (marker.markerType != ARMarkerType.QRCode)
        {
            return;
        }

        bool isNewTrackable = currentActiveMarker == null || currentActiveMarker.trackableId != marker.trackableId;
        if (isNewTrackable)
        {
            string decodedString = ARMarkerManager.Instance.GetDecodedString(marker.trackableId);
            if (string.IsNullOrEmpty(decodedString) || !ParseQRData(decodedString))
            {
                return;
            }
            currentActiveMarker = marker;
            SetDebugStatus("Parsed QR code data.");
        }

        if (currentActiveMarker != null && marker.trackableId == currentActiveMarker.trackableId &&
            marker.trackingState == TrackingState.Tracking)
        {
            lastValidPosition = marker.transform.position;
            lastValidRotation = marker.transform.rotation;
            bool recovered = phase != TrackingPhase.Tracking;
            phase = TrackingPhase.Tracking;
            NotifyTrackingIfDue(isNewTrackable || recovered);
        }
    }

    private bool ParseQRData(string data)
    {
        try
        {
            string ip = string.Empty;
            int port = 5000;
            string deviceId = string.Empty;
            string[] parts = data.Split(',');

            if (parts.Length >= 1)
            {
                string[] ipPort = parts[0].Split(':');
                if (ipPort.Length == 2)
                {
                    ip = ipPort[0];
                    if (!int.TryParse(ipPort[1], out port))
                    {
                        port = 5000;
                    }
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

            if (string.IsNullOrEmpty(ip))
            {
                return false;
            }

            currentIp = ip;
            currentPort = port;
            currentDeviceId = deviceId;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to parse QR code: " + exception.Message);
            return false;
        }
    }

    private void SetDebugStatus(string value)
    {
        if (debugStatus == value)
        {
            return;
        }
        debugStatus = value;
        OnStatusChanged?.Invoke();
    }
}
