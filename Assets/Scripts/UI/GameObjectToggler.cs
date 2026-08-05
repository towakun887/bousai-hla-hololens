using UnityEngine;

/// <summary>
/// UIボタン等からGameObjectのアクティブ状態（表示/非表示）をトグル（切り替え）するためのヘルパースクリプトです。
/// </summary>
public class GameObjectToggler : MonoBehaviour
{
    /// <summary>
    /// 指定したターゲットオブジェクトの表示状態を反転させます。
    /// OnClick() イベント等にこのメソッドを設定し、引数に対象オブジェクトを指定してください。
    /// </summary>
    public void ToggleActive(GameObject target)
    {
        if (target != null)
        {
            target.SetActive(!target.activeSelf);
        }
    }

    /// <summary>
    /// 自分自身の表示状態を反転させます。
    /// </summary>
    public void ToggleSelfActive()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
