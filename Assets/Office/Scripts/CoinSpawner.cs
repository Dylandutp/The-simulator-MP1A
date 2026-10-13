using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OfficeSim
{
    public sealed class CoinSpawner : MonoBehaviour
    {
        public CoinPickup coinPrefab;
        public Transform[] spawnPoints;
        public float minimumSpawnInterval = 30f;
        public float maximumSpawnInterval = 45f;

        private CoinPickup[] coins;

        IEnumerator Start()
        {
            coins = new CoinPickup[spawnPoints.Length];
            for (int i = 0; i < 4; i++)
            {
                SpawnCoin();
            }

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minimumSpawnInterval, maximumSpawnInterval));
                SpawnCoin();
            }
        }

        void SpawnCoin()
        {
            var emptySlots = new List<int>();
            for (int i = 0; i < coins.Length; i++)
            {
                if (!coins[i])
                {
                    emptySlots.Add(i);
                }
            }

            if (coins.Length - emptySlots.Count >= 4)
            {
                return;
            }

            int slot = emptySlots[Random.Range(0, emptySlots.Count)];
            Transform point = spawnPoints[slot];
            coins[slot] = Instantiate(coinPrefab, point.position, point.rotation, transform);
        }
    }
}
