using System;

[Serializable]
public class LocationData
{
    public bool isTracking;
    public double latitude;
    public double longitude;
    public double altitude;
    public float eunRotationX;
    public float eunRotationY;
    public float eunRotationZ;
    public float eunRotationW;
    public double accuracy;
    public double altitudeAccuracy;
    public double headingAccuracy;
}
