using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// アプリを最初からやり直す（シーンをリロードする）ためのスクリプトです。
/// HandMenuのやり直しボタンの OnClick イベントに、このスクリプトの ResetApp() メソッドを割り当ててください。
/// </summary>
public class AppResetManager : MonoBehaviour
{
    public void ResetApp()
    {
        Debug.Log("ResetApp called. Reloading the current scene...");
        // 現在アクティブなシーンを再読み込みすることで、すべての状態（ARセッション、キャリブレーション、UIなど）を初期化します。
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
