using System;
using UnityEngine;

[Serializable]
public class MarkerData
{
    public string serializedTime;
    public string markerKind;
    public string title;
    public string detail;
    public double latitude;
    public double longitude;
    public double altitude;
    public Quaternion eunRotation;
    public string driveUrl;
    public string fileId;
    public string mediaType;
}
