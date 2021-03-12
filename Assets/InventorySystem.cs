using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public GameObject[] inventoryItems;
    public Transform[] spawnPositions;
    GameObject[] spawnedObjects;

    void Start()
    {
        spawnedObjects = new GameObject[inventoryItems.Length];
    }

    public void SpawnItem(int itemNumber)
    {
        if (spawnedObjects[itemNumber] != null)
            return;

        spawnedObjects[itemNumber] = GameObject.Instantiate(inventoryItems[itemNumber], spawnPositions[itemNumber].position, spawnPositions[itemNumber].rotation);
    }

    public void DespawnItem(int itemNumber)
    {
        if (spawnedObjects[itemNumber] == null)
            return;

        Destroy(spawnedObjects[itemNumber]);
    }
}
