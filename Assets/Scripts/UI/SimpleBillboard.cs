using UnityEngine;

// Billboard rotation is centrally updated by MarkerManager so marker count does not add Update calls.
public class SimpleBillboard : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
