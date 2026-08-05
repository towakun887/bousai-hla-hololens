using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LocationResolver : MonoBehaviour
{
    public static LocationResolver Instance { get; private set; }

    private bool isCalibrated = false;
    private LocationData originGps;
    private Vector3 originUnity;
    
    // 北へのヨー角オフセット（度）
    private float northYRotationOffset = 0f;
    
    // 緯度1度あたりのメートル (簡易)
    private const double MetersPerDegreeLat = 111320.0;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void CalibrateOrigin(List<LocationData> gpsSamples, Vector3 qrUnityPosition, Quaternion qrUnityRotation)
    {
        if (gpsSamples == null || gpsSamples.Count == 0) return;

        // メジアンを取得
        originGps = new LocationData();
        
        var lats = gpsSamples.Select(s => s.latitude).OrderBy(v => v).ToList();
        var lons = gpsSamples.Select(s => s.longitude).OrderBy(v => v).ToList();
        var alts = gpsSamples.Select(s => s.altitude).OrderBy(v => v).ToList();

        originGps.latitude = GetMedian(lats);
        originGps.longitude = GetMedian(lons);
        originGps.altitude = GetMedian(alts);

        // Unity原点はスマホの位置
        originUnity = qrUnityPosition;
        
        // --- 北のオフセット計算（ヨー角のみ使用・ピッチ/ロール影響を排除）---
        var latestSample = gpsSamples.LastOrDefault();
        if (latestSample != null)
        {
            // スマホの地球に対する姿勢（EUN: X=東, Y=上, Z=北）
            Quaternion phoneEarthRot = new Quaternion(
                latestSample.eunRotationX, latestSample.eunRotationY,
                latestSample.eunRotationZ, latestSample.eunRotationW);

            // phoneEarthRot のヨー成分のみ抽出
            // 地球空間でのZ軸（北）をスマホのローカル前方として水平投影
            Vector3 phoneNorthInEarth = phoneEarthRot * Vector3.forward;
            phoneNorthInEarth.y = 0;

            // qrUnityRotation のヨー成分のみ抽出
            // HoloLens空間でのスマホ前方を水平投影
            Vector3 qrForwardInUnity = qrUnityRotation * Vector3.forward;
            qrForwardInUnity.y = 0;

            if (phoneNorthInEarth.sqrMagnitude > 0.001f && qrForwardInUnity.sqrMagnitude > 0.001f)
            {
                phoneNorthInEarth.Normalize();
                qrForwardInUnity.Normalize();

                // スマホが地球空間でどれだけヨーしているか（北=0°として）
                float phoneYaw = Mathf.Atan2(phoneNorthInEarth.x, phoneNorthInEarth.z) * Mathf.Rad2Deg;
                // HoloLens空間でのスマホ前方のヨー角
                float qrYaw = Mathf.Atan2(qrForwardInUnity.x, qrForwardInUnity.z) * Mathf.Rad2Deg;

                // 真北 = QRのヨー角 - スマホが地球空間で北から離れているヨー角
                northYRotationOffset = qrYaw - phoneYaw;
                Debug.Log($"[LocationResolver] phoneYaw={phoneYaw:F1}°, qrYaw={qrYaw:F1}°, northOffset={northYRotationOffset:F1}°");
            }
            else
            {
                Debug.LogWarning("[LocationResolver] 水平投影後のベクトルが短すぎるため北方向オフセットを更新できませんでした");
            }
        }
        
        isCalibrated = true;
        
        Debug.Log($"Calibrated Origin GPS: {originGps.latitude}, {originGps.longitude}, {originGps.altitude}");
    }
    
    public void CalibrateOrigin(LocationData originGps, Vector3 originUnity)
    {
        this.originGps = originGps;
        this.originUnity = originUnity;
        isCalibrated = true;
    }

    private double GetMedian(List<double> sortedList)
    {
        int count = sortedList.Count;
        if (count == 0) return 0;
        if (count % 2 == 0)
        {
            return (sortedList[count / 2 - 1] + sortedList[count / 2]) / 2.0;
        }
        return sortedList[count / 2];
    }

    public Vector3 GpsToUnityPosition(double latitude, double longitude, double altitude)
    {
        if (!isCalibrated) return Vector3.zero;

        double deltaLat = latitude - originGps.latitude;
        double deltaLon = longitude - originGps.longitude;
        double deltaAlt = altitude - originGps.altitude;

        double metersPerDegreeLon = MetersPerDegreeLat * Mathf.Cos((float)(originGps.latitude * Mathf.Deg2Rad));

        float offsetNorth = (float)(deltaLat * MetersPerDegreeLat);
        float offsetEast = (float)(deltaLon * metersPerDegreeLon);
        float offsetUp = (float)deltaAlt;

        // GPSの差分から計算されたローカルオフセット (Zが北, Xが東, Yが上)
        Vector3 localOffset = new Vector3(offsetEast, offsetUp, offsetNorth);
        
        // 算出された真北へのヨー角オフセットを適用して回転させる
        Vector3 rotatedOffset = Quaternion.Euler(0, northYRotationOffset, 0) * localOffset;
        
        return originUnity + rotatedOffset;
    }

    public bool IsCalibrated()
    {
        return isCalibrated;
    }
    
    public LocationData GetOriginGps()
    {
        return originGps;
    }
    
    public Vector3 GetOriginUnity()
    {
        return originUnity;
    }

    public float GetNorthYRotationOffset()
    {
        return northYRotationOffset;
    }
}
