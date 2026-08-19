using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public GameObject objectPrefab;
    public GameObject mainCamera;

    // ���̂���ǂꂭ�炢�O�ɏo����
    public float distance = 2.0f;
    public float distanceY = 5f;
    public float spawnRadius = 0.3f;
    public float dropCooldown = 0.2f;
    public float minRandomScale = 0.1f;
    public float maxRandomScale = 1.0f;
    [Tooltip("各岩が自身のスポーン地点からXZ方向にこの距離を超えたら削除します。Y方向は無視します")]
    [Min(0f)] public float despawnDistanceFromSpawn = 10f;

    [Header("QTY投下設定")]
    [Tooltip("1回のQTYで投下する個数")]
    [Min(1)] public int objectsPerQty = 10;
    [Tooltip("投下するQTYの回数。総投下数 = QTYあたり個数 × QTY数")]
    [Min(1)] public int qtyCount = 3;
    [Tooltip("QTYを投下してから次のQTYを投下するまでの秒数")]
    [Min(0f)] public float qtyIntervalSeconds = 0.5f;

    private float lastDropTime = float.NegativeInfinity;
    private Coroutine activeQtyDrop;
    private readonly List<SpawnedObject> spawnedObjects = new List<SpawnedObject>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Drop();
        }

        RemoveDistantSpawnedObjects();
    }

    public void Drop(float? scale = 1.0f)
    {
        if (Time.unscaledTime - lastDropTime < dropCooldown)
        {
            return;
        }

        lastDropTime = Time.unscaledTime;
        Spawn(scale ?? 1.0f);
    }

    public void DropRandomScale30()
    {
        if (activeQtyDrop != null || Time.unscaledTime - lastDropTime < dropCooldown)
        {
            return;
        }

        lastDropTime = Time.unscaledTime;
        activeQtyDrop = StartCoroutine(DropRandomScaleByQty());
    }

    public void doubleDrop(){
        StartCoroutine(DropRandomScaleByQty());
        StartCoroutine(DropRandomScaleByQty());
    }

    private IEnumerator DropRandomScaleByQty()
    {
        int countPerQty = Mathf.Max(1, objectsPerQty);
        int numberOfQty = Mathf.Max(1, qtyCount);

        float minScale = Mathf.Min(minRandomScale, maxRandomScale);
        float maxScale = Mathf.Max(minRandomScale, maxRandomScale);

        for (int qtyIndex = 0; qtyIndex < numberOfQty; qtyIndex++)
        {
            for (int objectIndex = 0; objectIndex < countPerQty; objectIndex++)
            {
                float weightedRandom = Random.value;
                weightedRandom *= weightedRandom;
                float randomScale = Mathf.Lerp(minScale, maxScale, weightedRandom);
                Spawn(randomScale);
            }

            if (qtyIndex < numberOfQty - 1 && qtyIntervalSeconds > 0f)
                yield return new WaitForSeconds(qtyIntervalSeconds);
        }

        // 間隔0で同一フレーム中に完了した場合も、StartCoroutineの戻り値で
        // activeQtyDropが再設定されないよう、次フレームで完了扱いにする。
        yield return null;
        activeQtyDrop = null;
    }

    private void OnDisable()
    {
        if (activeQtyDrop == null)
            return;

        StopCoroutine(activeQtyDrop);
        activeQtyDrop = null;
    }

    private void Spawn(float scale)
    {

        // ���̕��̂������Ă�������̑O��
        Transform cameraTransform = mainCamera.transform;
        Vector3 horizontalForward = Quaternion.Euler(
            0f,
            cameraTransform.eulerAngles.y,
            0f
        ) * Vector3.forward;

        Vector3 spawnPosition = cameraTransform.position + horizontalForward * distance;
        spawnPosition.y = cameraTransform.position.y + distanceY;

        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        Vector3 randomizedSpawnPosition = spawnPosition + new Vector3(
            randomOffset.x,
            0f,
            randomOffset.y
        );

        GameObject obj = Instantiate(
            objectPrefab,
            randomizedSpawnPosition,
            Quaternion.identity
        );

        obj.transform.localScale *= scale;
        spawnedObjects.Add(new SpawnedObject(obj, randomizedSpawnPosition));
    }

    private void RemoveDistantSpawnedObjects()
    {
        float distance = Mathf.Max(0f, despawnDistanceFromSpawn);
        float maxDistanceSquared = distance * distance;

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            SpawnedObject spawned = spawnedObjects[i];
            if (spawned.Instance == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }

            Vector3 offset = spawned.Instance.transform.position - spawned.SpawnPosition;
            float horizontalDistanceSquared = offset.x * offset.x + offset.z * offset.z;
            if (horizontalDistanceSquared <= maxDistanceSquared)
                continue;

            spawnedObjects.RemoveAt(i);
            Destroy(spawned.Instance);
        }
    }

    private readonly struct SpawnedObject
    {
        public SpawnedObject(GameObject instance, Vector3 spawnPosition)
        {
            Instance = instance;
            SpawnPosition = spawnPosition;
        }

        public GameObject Instance { get; }
        public Vector3 SpawnPosition { get; }
    }
}
