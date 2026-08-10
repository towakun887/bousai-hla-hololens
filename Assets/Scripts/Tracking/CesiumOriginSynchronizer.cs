using System;
using CesiumForUnity;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Edit Mode:
/// MockModeSettingsの固定座標をCesiumGeoreferenceへ同期。
///
/// Play Mode:
/// LocationResolverで確定したSLAM原点をCesiumGeoreferenceへ同期。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CesiumOriginSynchronizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CesiumGeoreference georeference;

    [SerializeField]
    private MockModeSettings mockModeSettings;

    [Header("Runtime SLAM Alignment")]
    [Tooltip(
        "ONの場合、CesiumGeoreferenceのTransformもSLAM原点位置・真北方向へ合わせます。")]
    [SerializeField]
    private bool syncSlamPoseAtRuntime = true;

    private LocationResolver subscribedResolver;

    private void Reset()
    {
        georeference = GetComponent<CesiumGeoreference>();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            BindLocationResolver();
            SyncRuntimeOrigin();
        }
        else
        {
            SyncEditorMockOrigin();
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            // Script実行順やScene再読み込みにも対応するため再確認する。
            if (subscribedResolver != LocationResolver.Instance)
                BindLocationResolver();

            // Eventを取り逃した場合にも次フレームで同期できる。
            SyncRuntimeOrigin();
        }
        else
        {
            // Mock Mode SettingsのInspector変更をEdit Mode中に即時反映する。
            SyncEditorMockOrigin();
        }
    }

    private void OnDisable()
    {
        UnbindLocationResolver();
    }

    private void BindLocationResolver()
    {
        if (subscribedResolver == LocationResolver.Instance)
            return;

        UnbindLocationResolver();

        subscribedResolver = LocationResolver.Instance;
        if (subscribedResolver != null)
            subscribedResolver.CalibrationChanged += HandleCalibrationChanged;
    }

    private void UnbindLocationResolver()
    {
        if (subscribedResolver != null)
            subscribedResolver.CalibrationChanged -= HandleCalibrationChanged;

        subscribedResolver = null;
    }

    private void HandleCalibrationChanged()
    {
        SyncRuntimeOrigin();
    }

    private void SyncEditorMockOrigin()
    {
        if (georeference == null || mockModeSettings == null)
            return;

        ApplyCoordinates(
            mockModeSettings.FixedLatitude,
            mockModeSettings.FixedLongitude,
            mockModeSettings.FixedAltitude,
            markDirty: true);
    }

    private void SyncRuntimeOrigin()
    {
        LocationResolver resolver = subscribedResolver;
        if (georeference == null ||
            resolver == null ||
            !resolver.IsCalibrated())
        {
            return;
        }

        LocationData origin = resolver.GetOriginGps();
        if (origin == null)
            return;

        if (syncSlamPoseAtRuntime)
        {
            // LocationResolverでは、originGpsがこのSLAM座標に対応する。
            georeference.transform.SetPositionAndRotation(
                resolver.GetOriginUnity(),
                Quaternion.Euler(
                    0f,
                    resolver.GetNorthYRotationOffset(),
                    0f));
        }

        ApplyCoordinates(
            origin.latitude,
            origin.longitude,
            origin.altitude,
            markDirty: false);
    }

    private void ApplyCoordinates(
        double latitude,
        double longitude,
        double altitude,
        bool markDirty)
    {
        if (!IsValidCoordinate(latitude, longitude, altitude))
        {
            Debug.LogWarning(
                $"[CesiumOriginSynchronizer] 不正な座標のため同期しません。" +
                $" lat={latitude}, lon={longitude}, alt={altitude}",
                this);
            return;
        }

        if (AreSame(
                georeference.latitude, latitude,
                georeference.longitude, longitude,
                georeference.height, altitude))
        {
            return;
        }

        // SynchronizerのOnEnableが先に呼ばれても安全にする。
        georeference.Initialize();

        // Cesium APIは Longitude, Latitude, Height の順。
        georeference.SetOriginLongitudeLatitudeHeight(
            longitude,
            latitude,
            altitude);

#if UNITY_EDITOR
        if (!Application.isPlaying && markDirty)
            EditorUtility.SetDirty(georeference);
#endif

    }

    private static bool IsValidCoordinate(
        double latitude,
        double longitude,
        double altitude)
    {
        return IsFinite(latitude) &&
               IsFinite(longitude) &&
               IsFinite(altitude) &&
               latitude >= -90d &&
               latitude <= 90d &&
               longitude >= -180d &&
               longitude <= 180d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool AreSame(
        double currentLatitude,
        double latitude,
        double currentLongitude,
        double longitude,
        double currentAltitude,
        double altitude)
    {
        const double coordinateTolerance = 0.000000001d;
        const double altitudeTolerance = 0.0001d;

        return Math.Abs(currentLatitude - latitude) <= coordinateTolerance &&
               Math.Abs(currentLongitude - longitude) <= coordinateTolerance &&
               Math.Abs(currentAltitude - altitude) <= altitudeTolerance;
    }
}
