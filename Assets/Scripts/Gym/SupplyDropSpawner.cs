using UnityEngine;
using System;

namespace HouseSim.Gym
{
    public class SupplyDropSpawner : MonoBehaviour
    {
        public GameObject supplyCratePrefab;
        public Vector2 roomHalfExtents = new Vector2(8f, 8f);
        public float spawnInterval = 45.0f;
        public Action<Vector3> spawnAction;
        private float timer = 0f;

        private void Update()
        {
            if ((supplyCratePrefab == null && spawnAction == null) || spawnInterval <= 0f)
            {
                return;
            }

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                timer -= spawnInterval;
                Vector3 location = transform.position + new Vector3(
                    UnityEngine.Random.Range(-roomHalfExtents.x, roomHalfExtents.x), 0.5f,
                    UnityEngine.Random.Range(-roomHalfExtents.y, roomHalfExtents.y));
                if (spawnAction != null)
                    spawnAction(location);
                else
                    Instantiate(supplyCratePrefab, location, Quaternion.identity);
            }
        }
    }
}