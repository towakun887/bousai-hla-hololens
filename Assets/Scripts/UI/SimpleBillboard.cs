using UnityEngine;

public class SimpleBillboard : MonoBehaviour
{
    private void Update()
    {
        if (Camera.main != null)
        {
            // カメラの方を向くように回転
            // UnityのUI（Canvas）やQuadは通常Z軸の負の方向（後ろ）から見るのが正面となるため、
            // -direction をターゲットにして Z軸をカメラの反対側に向けることで、文字が反転せずに正しく読めるようにする
            Vector3 direction = Camera.main.transform.position - transform.position;
            direction.y = 0; // 水平方向のみ回転（Y軸回転のみ）
            
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-direction);
            }
        }
    }
}
