using System;
using Microsoft.MixedReality.OpenXR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class QRCodeTracker : MonoBehaviour
{
    public const string RelativePoseBypassPreferenceKey = "QrRelativePoseBypass";

    public static QRCodeTracker Instance { get; private set; }

    public Action<string, int, string, Vector3, Quaternion> OnQRCodeDetected;
    public Action OnQRCodeLost;
    public Action OnStatusChanged;

    [Header("Tracking Timing")]
    [SerializeField, Min(0.02f)] private float notificationInterval = 0.1f;
    [SerializeField, Min(0f)] private float lostGracePeriod = 0.5f;

    [Header("QR Pose")]
    [Tooltip("ONの場合、QRコードの相対位置・回転の取得を待たず、デコード完了時点で接続処理へ進みます。SLAM原点はワールド原点、北方向は+Zとして扱われます。")]
    [SerializeField] private bool bypassRelativePoseCalculation;

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

    public bool IsMockMode => MockModeSettings.Instance != null && MockModeSettings.Instance.HasAnyLocationMock;
    public bool IsDirectConnectionMock => MockModeSettings.Instance != null && MockModeSettings.Instance.UseDirectConnectionMock;
    public bool IsQRCodeLocated => phase == TrackingPhase.Tracking;
    public bool IsQRCodeDetected => currentActiveMarker != null || phase == TrackingPhase.Tracking || phase == TrackingPhase.TemporarilyLost;
    public bool BypassRelativePoseCalculation => bypassRelativePoseCalculation;
    public string DebugStatus => debugStatus;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            mainCamera = Camera.main;
            bypassRelativePoseCalculation = PlayerPrefs.GetInt(
                RelativePoseBypassPreferenceKey,
                bypassRelativePoseCalculation ? 1 : 0) == 1;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        MockModeSettings mockSettings = MockModeSettings.Instance;
        if (mockSettings != null && mockSettings.UseFixedLocationMock)
        {
            SetDebugStatus("Fixed location Mock: QR/TCP disabled");
            return;
        }

        if (mockSettings != null && mockSettings.UseDirectConnectionMock)
        {
            SetDebugStatus("Direct connection Mock: QR bypassed");
            Invoke(nameof(SimulateQRDetect), 0.5f);
            return;
        }

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
        if (IsMockMode)
            return;

        // バイパス時はQRの空間姿勢を追跡・更新しない。
        if (bypassRelativePoseCalculation)
            return;

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
        MockModeSettings mockSettings = MockModeSettings.Instance;
        if (mockSettings == null || !mockSettings.UseDirectConnectionMock)
            return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        lastValidPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        lastValidRotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
        currentIp = mockSettings.ConnectionIp;
        currentPort = mockSettings.ConnectionPort;
        currentDeviceId = mockSettings.ConnectionDeviceId;
        phase = TrackingPhase.Tracking;
        SetDebugStatus("Direct connection Mock active");
        NotifyTrackingIfDue(true);
    }

    public void SimulateTemporaryTrackingLoss()
    {
        if (IsDirectConnectionMock && phase == TrackingPhase.Tracking)
        {
            phase = TrackingPhase.TemporarilyLost;
            temporarilyLostAt = Time.unscaledTime;
            SetDebugStatus("Direct connection Mock temporarily lost");
        }
    }

    public void SimulateQRLost()
    {
        if (IsDirectConnectionMock)
            EnterLost("Direct connection Mock lost");
    }

    /// <summary>
    /// QRコードの相対位置・回転を使う処理を切り替えます。
    /// 設定UIのToggle等から直接呼び出せます。
    /// </summary>
    public void SetRelativePoseCalculationBypassed(bool bypass)
    {
        bypassRelativePoseCalculation = bypass;
        PlayerPrefs.SetInt(RelativePoseBypassPreferenceKey, bypass ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetRelativePoseCalculationEnabled(bool enabled)
    {
        SetRelativePoseCalculationBypassed(!enabled);
    }

    private void NotifyTrackingIfDue(bool force)
    {
        float now = Time.unscaledTime;
        if (!force && now - lastNotificationTime < notificationInterval)
        {
            return;
        }

        lastNotificationTime = now;
        if (string.IsNullOrWhiteSpace(currentIp))
            return;

        OnQRCodeDetected?.Invoke(
            currentIp, currentPort, currentDeviceId,
            lastValidPosition, lastValidRotation);
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

        // 空間姿勢を使う場合のみ、QRコード中央をTransform原点にする。
        // バイパス時はTransformの再計算を待たずにデコード結果を使用する。
        if (!bypassRelativePoseCalculation && marker.transformMode != TransformMode.Center)
        {
            marker.transformMode = TransformMode.Center;
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

        if (bypassRelativePoseCalculation)
        {
            // 呼び出し側が誤ってQR姿勢を利用しないよう、明示的な既定姿勢を渡す。
            lastValidPosition = Vector3.zero;
            lastValidRotation = Quaternion.identity;
            bool recovered = phase != TrackingPhase.Tracking;
            phase = TrackingPhase.Tracking;
            SetDebugStatus("QR decoded. Relative pose calculation bypassed.");
            NotifyTrackingIfDue(isNewTrackable || recovered);
            return;
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
