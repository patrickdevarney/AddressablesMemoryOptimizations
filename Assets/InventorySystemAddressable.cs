using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class InventorySystemAddressable : MonoBehaviour
{
    public AssetReferenceGameObject[] inventoryItems;
    public Transform[] spawnPositions;
    Dictionary<int, List<GameObject>> spawnedObjects = new Dictionary<int, List<GameObject>>();
    public void SpawnItem(int itemNumber)
    {
        Debug.Log("Spawning item " + itemNumber);
        if (!spawnedObjects.ContainsKey(itemNumber))
        {
            spawnedObjects.Add(itemNumber, new List<GameObject>());
        }

        if (spawnedObjects[itemNumber].Count > 0)
        {
            Vector3 randomPos = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-1.5f, 1.5f), 0);
            StartCoroutine(WaitForSpawnComplete(Addressables.InstantiateAsync(inventoryItems[itemNumber], spawnPositions[itemNumber].position + randomPos, spawnPositions[itemNumber].rotation), itemNumber));
        }
        else
        {
            StartCoroutine(WaitForSpawnComplete(Addressables.InstantiateAsync(inventoryItems[itemNumber], spawnPositions[itemNumber].position, spawnPositions[itemNumber].rotation), itemNumber));
        }
    }

    IEnumerator WaitForSpawnComplete(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> op, int itemNumber)
    {
        while(op.IsDone == false)
        {
            yield return op;
        }

        OnSpawnComplete(op, itemNumber);
    }

    public void DespawnItem(int itemNumber)
    {
        if (spawnedObjects.TryGetValue(itemNumber, out var value))
        {
            foreach(var entry in value)
            {
                Addressables.ReleaseInstance(entry);
            }
            value.Clear();
        }
        else
        {
            return;
        }
    }

    void OnSpawnComplete(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> handle, int itemNumber)
    {
        if (spawnedObjects.TryGetValue(itemNumber, out var value))
        {
            value.Add(handle.Result);
        }
        else
        {
            spawnedObjects.Add(itemNumber, new List<GameObject>() { handle.Result });
        }
    }

    public void SpawnAll(int amount)
    {
        for (int i = 0; i < inventoryItems.Length; i++)
        {
            for (int j = 0; j < amount; j++)
            {
                SpawnItem(i);
            }
        }
    }
}
