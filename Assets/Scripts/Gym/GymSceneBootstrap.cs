using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseSim.Gym
{
    public class GymSceneBootstrap : MonoBehaviour
    {
        private static readonly Color ChalkColor = new Color(0.85f, 0.92f, 0.82f);
        private static readonly Color BoardColor = new Color(0.07f, 0.12f, 0.09f);
        private static readonly Color[] MachineColors =
        {
            new Color(0.28f, 0.35f, 0.42f),
            new Color(0.48f, 0.27f, 0.18f),
            new Color(0.22f, 0.38f, 0.29f),
            new Color(0.20f, 0.22f, 0.24f),
            new Color(0.36f, 0.27f, 0.44f),
            new Color(0.24f, 0.36f, 0.42f),
            new Color(0.43f, 0.34f, 0.20f),
            new Color(0.42f, 0.24f, 0.24f)
        };

        private GymContentCatalog catalog;
        private PlayerResourceWallet wallet;
        private GymGridBuilder gridBuilder;
        private GymQuestTracker questTracker;
        private Vector3 groundOrigin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapGymScene()
        {
            if (SceneManager.GetActiveScene().name != "GymRoomScene" ||
                FindFirstObjectByType<GymSceneBootstrap>() != null)
            {
                return;
            }

            new GameObject("Gym Runtime").AddComponent<GymSceneBootstrap>();
        }

        private void Awake()
        {
            catalog = Resources.Load<GymContentCatalog>("GymContentCatalog");
            if (catalog == null)
            {
                Debug.LogError("Missing Assets/Resources/GymContentCatalog.asset; gym content was not created.", this);
                return;
            }

            PrepareFloor();
            CreateGymWalls();
            InitializePlayer();
            CreateGymBoards();
            CreateUtilityStations();
            CreateWildDumbbells();
            CreateTutorialZones();
            CreateSupplySpawner();
        }

        private void PrepareFloor()
        {
            GameObject floor = GameObject.Find("floor");
            if (floor == null)
            {
                Debug.LogError("GymRoomScene has no floor object.", this);
                groundOrigin = Vector3.zero;
                return;
            }

            floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
            groundOrigin = floor.transform.position + Vector3.up * 0.1f;
        }

        private void CreateGymWalls()
        {
            Color wallColor = new Color(0.22f, 0.24f, 0.25f);
            CreateUtilityObject("Back Gym Wall", new Vector3(0f, 2.6f, 10.1f),
                new Vector3(20f, 5.2f, 0.2f), wallColor);
            CreateUtilityObject("Left Gym Wall", new Vector3(-10.1f, 2.6f, 0f),
                new Vector3(0.2f, 5.2f, 20f), wallColor);
            CreateUtilityObject("Right Gym Wall", new Vector3(10.1f, 2.6f, 0f),
                new Vector3(0.2f, 5.2f, 20f), wallColor);
            CreateUtilityObject("Entrance Wall Left", new Vector3(-6f, 2.6f, -10.1f),
                new Vector3(8f, 5.2f, 0.2f), wallColor);
            CreateUtilityObject("Entrance Wall Right", new Vector3(6f, 2.6f, -10.1f),
                new Vector3(8f, 5.2f, 0.2f), wallColor);
        }

        private void InitializePlayer()
        {
            wallet = gameObject.AddComponent<PlayerResourceWallet>();
            wallet.Initialize(catalog);
            gameObject.AddComponent<GymInventory>();

            questTracker = gameObject.AddComponent<GymQuestTracker>();
            questTracker.wallet = wallet;
            questTracker.gymCoinsResource = catalog.GetResource("GymCoins");
            questTracker.BindWallet(wallet);

            gridBuilder = gameObject.AddComponent<GymGridBuilder>();
            gridBuilder.catalog = catalog;
            gridBuilder.gymCoinsResource = catalog.GetResource("GymCoins");
            gridBuilder.gridOrigin = groundOrigin;
            gridBuilder.machineFactory = CreateWorkoutMachine;
        }

        private void CreateGymBoards()
        {
            CreateBoard("Gym Wall Chalkboard", GymWorldBoard.BoardType.Chalkboard,
                new Vector3(0f, 2.2f, 9.91f), new Vector3(6f, 3.8f, 0.18f));
            CreateBoard("Locker Clipboard", GymWorldBoard.BoardType.LockerClipboard,
                new Vector3(9.65f, 1.65f, -7.5f), new Vector3(1.6f, 2.2f, 0.12f));

            CreateButton("Build Rubber Mat (10 GymCoins)", new Vector3(-1.8f, 0.65f, 9.75f),
                new Vector3(1.4f, 0.35f, 0.12f), new Color(0.17f, 0.34f, 0.22f),
                () => gridBuilder.TryBuildMatAtView());

            if (catalog.workoutMachines == null) return;
            for (int i = 0; i < catalog.workoutMachines.Length; i++)
            {
                GymMachineDefinition definition = catalog.workoutMachines[i];
                if (definition == null) continue;

                int column = i % 4;
                int row = i / 4;
                Vector3 position = new Vector3(-0.9f + column * 1.1f, 0.45f - row * 0.55f, 9.75f);
                CreateButton($"Select {definition.machineName}", position,
                    new Vector3(0.95f, 0.32f, 0.12f), MachineColors[i % MachineColors.Length],
                    () => gridBuilder.SelectMachine(definition));
            }
        }

        private void CreateBoard(string boardName, GymWorldBoard.BoardType type,
            Vector3 position, Vector3 scale)
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = boardName;
            board.transform.position = position;
            board.transform.localScale = scale;
            if (type == GymWorldBoard.BoardType.LockerClipboard)
                board.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            board.GetComponent<Renderer>().material.color =
                type == GymWorldBoard.BoardType.Chalkboard
                    ? BoardColor
                    : new Color(0.38f, 0.24f, 0.13f);

            TextMesh text = CreateWorldText($"{boardName} Text", board.transform,
                new Vector3(-0.46f, 0.46f, -0.53f), 0.085f, ChalkColor);
            text.alignment = TextAlignment.Left;
            text.anchor = TextAnchor.UpperLeft;
            GymWorldBoard worldBoard = board.AddComponent<GymWorldBoard>();
            worldBoard.boardType = type;
            worldBoard.boardText = text;
            worldBoard.BindWallet(wallet);
            worldBoard.BindQuestTracker(questTracker);
        }

        private void CreateButton(string label, Vector3 position, Vector3 scale, Color color, Action action)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = label;
            button.transform.position = position;
            button.transform.localScale = scale;
            button.GetComponent<Renderer>().material.color = color;

            TextMesh text = CreateWorldText($"{label} Label", button.transform,
                new Vector3(-0.47f, 0f, -0.55f), 0.075f, Color.white);
            text.text = label;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;

            GymActionInteractable interactable = button.AddComponent<GymActionInteractable>();
            interactable.Configure(action, allowGrab: false);
        }

        private void CreateUtilityStations()
        {
            CreateWaterDispenser();
            CreateProteinBlender();
            CreateWrench();
        }

        private void CreateWaterDispenser()
        {
            StatData reservoir = catalog.GetStat("WaterReservoir");
            GameObject dispenser = CreateUtilityPrefab(1, "Water Dispenser",
                new Vector3(-4f, groundOrigin.y + 0.9f, 3f),
                new Vector3(0.9f, 1.8f, 0.8f), new Color(0.19f, 0.48f, 0.68f));
            MachineStats stats = GetOrAdd<MachineStats>(dispenser);
            stats.Configure("Water Dispenser", reservoir, null);

            WaterDispenser water = GetOrAdd<WaterDispenser>(dispenser);
            water.dispenserStats = stats;
            if (dispenser.GetComponent<MachineDialGauge>() == null) AddDial(dispenser, stats);
            GymActionInteractable interactable = GetOrAdd<GymActionInteractable>(dispenser);
            interactable.Configure(water.HarvestWater, allowGrab: false);
        }

        private void CreateProteinBlender()
        {
            GameObject blender = CreateUtilityPrefab(3, "Protein Blender",
                new Vector3(4f, groundOrigin.y + 0.75f, 3f),
                new Vector3(1f, 1.5f, 0.8f), new Color(0.55f, 0.35f, 0.2f));
            CreateBlenderJar(blender.transform);
            MachineStats stats = GetOrAdd<MachineStats>(blender);
            stats.Configure("Protein Blender", catalog.GetStat("MixSpeed"), null);

            ProteinBlenderCrafting crafting = GetOrAdd<ProteinBlenderCrafting>(blender);
            crafting.hydrationResource = catalog.GetResource("Hydration");
            crafting.stressResource = catalog.GetResource("Stress");
            if (blender.GetComponent<MachineDialGauge>() == null) AddDial(blender, stats);

            GymRecipe[] recipes = (GymRecipe[])Enum.GetValues(typeof(GymRecipe));
            for (int i = 0; i < recipes.Length; i++)
            {
                GymRecipe recipe = recipes[i];
                Vector3 buttonPosition = blender.transform.position +
                    new Vector3(-0.8f + i * 0.4f, 1.15f, -0.6f);
                CreateButton($"Craft {recipe}", buttonPosition, new Vector3(0.34f, 0.25f, 0.1f),
                    new Color(0.24f, 0.39f, 0.3f), () => crafting.Craft(recipe));
            }

            GymActionInteractable interactable = GetOrAdd<GymActionInteractable>(blender);
            interactable.Configure(crafting.ConsumeProteinShake, allowGrab: false);
        }

        private void CreateWrench()
        {
            GameObject wrench = CreateUtilityPrefab(4, "Wrench Tool",
                new Vector3(0f, groundOrigin.y + 0.7f, 2f),
                new Vector3(0.8f, 0.12f, 0.12f), new Color(0.58f, 0.6f, 0.62f));
            if (wrench.transform.Find("Wrench Handle") == null)
                CreateWrenchHead(wrench.transform);
            MachineStats stats = GetOrAdd<MachineStats>(wrench);
            stats.Configure("Wrench Tool", catalog.GetStat("RepairPower"), null);
            WrenchRepairAndShatter repair = GetOrAdd<WrenchRepairAndShatter>(wrench);
            repair.gymCoinsResource = catalog.GetResource("GymCoins");
            repair.repairPowerStat = catalog.GetStat("RepairPower");

            Rigidbody body = GetOrAdd<Rigidbody>(wrench);
            body.isKinematic = true;
            SphereCollider trigger = GetOrAdd<SphereCollider>(wrench);
            trigger.isTrigger = true;
            trigger.radius = 0.45f;
            GymActionInteractable interactable = GetOrAdd<GymActionInteractable>(wrench);
            interactable.Configure(null);
        }

        private GameObject CreateUtilityObject(string objectName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject utility = GameObject.CreatePrimitive(PrimitiveType.Cube);
            utility.name = objectName;
            utility.transform.position = position;
            utility.transform.localScale = scale;
            utility.GetComponent<Renderer>().material.color = color;
            return utility;
        }

        private GameObject CreateWorkoutMachine(GymMachineDefinition definition, Vector3 position)
        {
            if (definition.primaryStat == null || definition.secondaryStat == null ||
                definition.gainResource == null)
            {
                Debug.LogError($"Machine definition '{definition.machineName}' is incomplete.", this);
                return null;
            }

            int machineIndex = Array.IndexOf(catalog.workoutMachines, definition);
            bool hasGeneratedPrefab = definition.prefab != null;
            GameObject machine = !hasGeneratedPrefab
                ? CreateUtilityObject(definition.machineName, position, definition.dimensions,
                    MachineColors[Mathf.Max(0, machineIndex) % MachineColors.Length])
                : Instantiate(definition.prefab, position, Quaternion.identity);
            machine.name = definition.machineName;
            machine.transform.position = position;
            machine.transform.localScale = hasGeneratedPrefab ? Vector3.one : definition.dimensions;
            MachineStats stats = GetOrAdd<MachineStats>(machine);
            stats.Configure(definition.machineName, definition.primaryStat, definition.secondaryStat);

            WorkoutMachine workout = GetOrAdd<WorkoutMachine>(machine);
            workout.targetGainResource = definition.gainResource;
            workout.energyResource = catalog.GetResource("Energy");
            workout.hydrationResource = catalog.GetResource("Hydration");
            workout.stressResource = catalog.GetResource("Stress");
            workout.machineStats = stats;

            if (machine.GetComponent<MachineDialGauge>() == null) AddDial(machine, stats);
            else EnsureDialVisual(machine.transform);
            GymActionInteractable interactable = GetOrAdd<GymActionInteractable>(machine);
            TreadmillDistance treadmill = machine.GetComponent<TreadmillDistance>();
            if (definition.machineName == "Treadmill" && treadmill == null)
                treadmill = AddTreadmill(machine, stats, workout, definition.secondaryStat);
            else if (treadmill != null && treadmill.beltRenderer != null &&
                     treadmill.beltRenderer.material.mainTexture == null)
            {
                Material beltMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                beltMaterial.mainTexture = CreateBeltTexture();
                beltMaterial.mainTextureScale = new Vector2(1f, 3f);
                treadmill.beltRenderer.material = beltMaterial;
            }
            interactable.Configure(treadmill == null
                ? (Action)(() => workout.PerformExerciseRep())
                : treadmill.ToggleRunning, allowGrab: false);
            return machine;
        }

        private TreadmillDistance AddTreadmill(GameObject machine, MachineStats stats,
            WorkoutMachine workout, StatData distanceStat)
        {
            GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            belt.name = "Scrolling Treadmill Belt";
            belt.transform.SetParent(machine.transform, false);
            belt.transform.localPosition = new Vector3(0f, -0.15f, -0.15f);
            belt.transform.localScale = new Vector3(0.9f, 0.08f, 0.62f);
            Destroy(belt.GetComponent<Collider>());
            Renderer beltRenderer = belt.GetComponent<Renderer>();
            Material beltMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            beltMaterial.mainTexture = CreateBeltTexture();
            beltMaterial.mainTextureScale = new Vector2(1f, 3f);
            beltRenderer.material = beltMaterial;

            TreadmillDistance treadmill = GetOrAdd<TreadmillDistance>(machine);
            treadmill.machineStats = stats;
            treadmill.distanceStat = distanceStat;
            treadmill.speedMPH = stats.primaryStatValue;
            treadmill.beltRenderer = beltRenderer;
            treadmill.workoutMachine = workout;
            return treadmill;
        }

        private static Texture2D CreateBeltTexture()
        {
            Texture2D texture = new Texture2D(16, 32, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < texture.height; y++)
            {
                Color stripe = y % 8 < 2 ? new Color(0.45f, 0.45f, 0.45f) :
                    new Color(0.1f, 0.1f, 0.1f);
                for (int x = 0; x < texture.width; x++) texture.SetPixel(x, y, stripe);
            }
            texture.Apply();
            return texture;
        }

        private void AddDial(GameObject machine, MachineStats stats)
        {
            GameObject dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dial.name = "Machine Dial Gauge";
            dial.transform.SetParent(machine.transform, false);
            dial.transform.localPosition = new Vector3(0f, 0.65f, -0.57f);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dial.transform.localScale = new Vector3(0.3f, 0.035f, 0.3f);
            Destroy(dial.GetComponent<Collider>());

            TextMesh gaugeText = CreateWorldText("Machine Dial Readout", dial.transform,
                new Vector3(-1.1f, 0.6f, -0.4f), 0.09f, ChalkColor);
            gaugeText.alignment = TextAlignment.Left;
            MachineDialGauge gauge = machine.AddComponent<MachineDialGauge>();
            gauge.machineStats = stats;
            gauge.gaugeText = gaugeText;
        }

        private void CreateWildDumbbells()
        {
            StatData mass = catalog.GetStat("DumbbellMass");
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = groundOrigin + new Vector3(
                    UnityEngine.Random.Range(-7f, 7f), 0.07f,
                    UnityEngine.Random.Range(-6f, 6f));
                GameObject dumbbell = CreateUtilityPrefab(2, "Rusty Dumbbells", position,
                    new Vector3(0.46f, 0.13f, 0.13f), new Color(0.3f, 0.24f, 0.19f));
                Rigidbody body = GetOrAdd<Rigidbody>(dumbbell);
                body.mass = mass == null ? 25f : mass.defaultValue;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                MachineStats stats = GetOrAdd<MachineStats>(dumbbell);
                stats.Configure("Rusty Dumbbells", mass, null);
                GetOrAdd<DumbbellFloorCollision>(dumbbell);
                RustyDumbbells instance = GetOrAdd<RustyDumbbells>(dumbbell);
                instance.massStat = mass;
                AddDumbbellWeights(dumbbell.transform);

                GymActionInteractable interactable = GetOrAdd<GymActionInteractable>(dumbbell);
                interactable.Configure(null, instance.ReleaseFromHand);
            }
        }

        private void CreateTutorialZones()
        {
            string[] messages =
            {
                "Welcome to the Gym!\nUse the wall chalkboard to build.",
                "Build a Rubber Mat first.\nMachines snap to its grid tile.",
                "Exercise uses Energy and Hydration.\nThe Wrench repairs or salvages."
            };

            Vector3[] positions =
            {
                new Vector3(-6f, groundOrigin.y + 1f, 0f),
                new Vector3(0f, groundOrigin.y + 1f, -6f),
                new Vector3(6f, groundOrigin.y + 1f, 0f)
            };

            for (int i = 0; i < messages.Length; i++)
            {
                GameObject zone = new GameObject($"Tutorial Trigger {i + 1}");
                zone.transform.position = positions[i];
                SphereCollider trigger = zone.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.25f;
                Rigidbody triggerBody = zone.AddComponent<Rigidbody>();
                triggerBody.isKinematic = true;
                triggerBody.useGravity = false;

                GameObject popup = GameObject.CreatePrimitive(PrimitiveType.Cube);
                popup.name = "Spatial Tutorial Pop-up";
                popup.transform.position = positions[i] + Vector3.up * 1.2f;
                popup.transform.localScale = new Vector3(2.4f, 0.8f, 0.08f);
                popup.GetComponent<Renderer>().material.color = new Color(0.12f, 0.18f, 0.2f);
                Destroy(popup.GetComponent<Collider>());
                TextMesh text = CreateWorldText("Tutorial Message", popup.transform,
                    new Vector3(-0.46f, 0.35f, -0.55f), 0.11f, Color.white);
                text.text = messages[i];
                text.alignment = TextAlignment.Center;
                text.anchor = TextAnchor.MiddleCenter;
                popup.SetActive(false);

                TutorialPopup tutorial = zone.AddComponent<TutorialPopup>();
                tutorial.textCanvas = popup;
            }

            Transform xrOrigin = FindXROrigin();
            if (xrOrigin != null)
            {
                xrOrigin.gameObject.tag = "Player";
                if (xrOrigin.GetComponent<Collider>() == null)
                {
                    CapsuleCollider playerCollider = xrOrigin.gameObject.AddComponent<CapsuleCollider>();
                    playerCollider.isTrigger = true;
                    playerCollider.height = 1.7f;
                    playerCollider.radius = 0.35f;
                    playerCollider.center = new Vector3(0f, 0.85f, 0f);
                }
                CreateTrackedHandVisuals(xrOrigin);
            }
        }

        private void CreateSupplySpawner()
        {
            SupplyDropSpawner spawner = gameObject.AddComponent<SupplyDropSpawner>();
            spawner.spawnInterval = 45f;
            spawner.roomHalfExtents = new Vector2(8f, 8f);
            spawner.spawnAction = SpawnCoinCrate;
        }

        private void SpawnCoinCrate(Vector3 position)
        {
            GameObject crate = CreateUtilityObject("Coin Crate", position,
                new Vector3(0.7f, 0.65f, 0.7f), new Color(0.72f, 0.53f, 0.18f));
            CoinCrate content = crate.AddComponent<CoinCrate>();
            content.gymCoinsResource = catalog.GetResource("GymCoins");
            crate.AddComponent<Rigidbody>();
            GymActionInteractable interactable = crate.AddComponent<GymActionInteractable>();
            interactable.Configure(content.Collect, allowGrab: false);
        }

        private GameObject CreateUtilityPrefab(int index, string objectName, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject prefab = catalog.utilityPrefabs != null &&
                                index < catalog.utilityPrefabs.Length
                ? catalog.utilityPrefabs[index]
                : null;
            GameObject utility = prefab == null
                ? CreateUtilityObject(objectName, position, scale, color)
                : Instantiate(prefab, position, Quaternion.identity);
            utility.name = objectName;
            utility.transform.position = position;
            utility.transform.localScale =
                (objectName == "Rusty Dumbbells" || objectName == "Wrench Tool") && prefab != null
                ? Vector3.one
                : scale;
            Renderer renderer = utility.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
            return utility;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component == null ? target.AddComponent<T>() : component;
        }

        private static void EnsureDialVisual(Transform machine)
        {
            if (machine.Find("Machine Dial Face") != null) return;
            GameObject dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dial.name = "Machine Dial Face";
            dial.transform.SetParent(machine, false);
            dial.transform.localPosition = new Vector3(0f, 0.65f, -0.57f);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dial.transform.localScale = new Vector3(0.3f, 0.035f, 0.3f);
            Destroy(dial.GetComponent<Collider>());
        }

        private static void AddDumbbellWeights(Transform dumbbell)
        {
            if (dumbbell.Find("Imported Dumbbell Mesh") != null ||
                dumbbell.Find("Dumbbell Grip Bar") != null ||
                dumbbell.Find("Dumbbell Weight Left") != null) return;
            CreateDumbbellWeight(dumbbell, "Dumbbell Weight Left", -0.55f);
            CreateDumbbellWeight(dumbbell, "Dumbbell Weight Right", 0.55f);
        }

        private static void CreateDumbbellWeight(Transform dumbbell, string weightName, float x)
        {
            GameObject weight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            weight.name = weightName;
            weight.transform.SetParent(dumbbell, false);
            weight.transform.localPosition = new Vector3(x, 0f, 0f);
            weight.transform.localScale = new Vector3(0.45f, 0.8f, 0.8f);
            weight.GetComponent<Renderer>().material.color = new Color(0.23f, 0.19f, 0.16f);
            Destroy(weight.GetComponent<Collider>());
        }

        private static void CreateWrenchHead(Transform wrench)
        {
            if (wrench.Find("Wrench Head") != null || wrench.Find("Wrench Jaw") != null) return;
            CreateWrenchJawPart(wrench, "Wrench Jaw", new Vector3(0.45f, 0f, 0f),
                new Vector3(0.3f, 1.5f, 1.1f));
            CreateWrenchJawPart(wrench, "Wrench Jaw Tip", new Vector3(0.5f, 0.75f, 0f),
                new Vector3(0.55f, 0.3f, 1.1f));
        }

        private static void CreateWrenchJawPart(Transform wrench, string partName,
            Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(wrench, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().material.color = new Color(0.58f, 0.6f, 0.62f);
            Destroy(part.GetComponent<Collider>());
        }

        private static void CreateBlenderJar(Transform blender)
        {
            if (blender.Find("Blender Jar") != null ||
                blender.Find("Blender Mixing Jar") != null) return;
            GameObject jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            jar.name = "Blender Mixing Jar";
            jar.transform.SetParent(blender, false);
            jar.transform.localPosition = new Vector3(0f, 0.58f, -0.12f);
            jar.transform.localScale = new Vector3(0.45f, 0.38f, 0.45f);
            jar.GetComponent<Renderer>().material.color = new Color(0.55f, 0.76f, 0.83f, 0.8f);
            Destroy(jar.GetComponent<Collider>());
        }

        private static TextMesh CreateWorldText(string textName, Transform parent,
            Vector3 localPosition, float characterSize, Color color)
        {
            GameObject textObject = new GameObject(textName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Vector3 scale = parent.lossyScale;
            textObject.transform.localScale = new Vector3(
                1f / Mathf.Max(0.001f, scale.x),
                1f / Mathf.Max(0.001f, scale.y),
                1f / Mathf.Max(0.001f, scale.z));
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = string.Empty;
            text.characterSize = characterSize;
            text.fontSize = 48;
            text.color = color;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            return text;
        }

        private static Transform FindXROrigin()
        {
            GameObject namedOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (namedOrigin != null) return namedOrigin.transform;

            Camera mainCamera = Camera.main;
            return mainCamera == null ? null : mainCamera.transform.parent;
        }

        private static void CreateTrackedHandVisuals(Transform xrOrigin)
        {
            foreach (Transform trackedTransform in xrOrigin.GetComponentsInChildren<Transform>(true))
            {
                bool isLeft = trackedTransform.name.IndexOf("Left Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              trackedTransform.name.IndexOf("Left Hand", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isRight = trackedTransform.name.IndexOf("Right Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               trackedTransform.name.IndexOf("Right Hand", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((!isLeft && !isRight) || trackedTransform.Find("Tracked VR Hand") != null) continue;

                GameObject hand = new GameObject("Tracked VR Hand");
                hand.transform.SetParent(trackedTransform, false);
                hand.transform.localPosition = new Vector3(0f, -0.025f, 0.055f);
                Material skin = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                skin.color = new Color(0.78f, 0.61f, 0.49f);

                CreateHandPart("Palm", PrimitiveType.Cube, hand.transform,
                    new Vector3(0f, 0f, 0f), new Vector3(0.095f, 0.035f, 0.105f), skin);
                float[] fingerX = { -0.035f, -0.012f, 0.012f, 0.035f };
                for (int finger = 0; finger < fingerX.Length; finger++)
                {
                    float length = 0.085f - Mathf.Abs(finger - 1.5f) * 0.008f;
                    CreateHandPart($"Finger {finger + 1}", PrimitiveType.Capsule, hand.transform,
                        new Vector3(fingerX[finger], 0f, 0.085f),
                        new Vector3(0.014f, length * 0.5f, 0.014f), skin);
                }

                CreateHandPart("Thumb", PrimitiveType.Capsule, hand.transform,
                    new Vector3(isLeft ? 0.065f : -0.065f, 0f, 0.025f),
                    new Vector3(0.016f, 0.045f, 0.016f), skin);
            }
        }

        private static void CreateHandPart(string partName, PrimitiveType shape, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().material = material;
            Destroy(part.GetComponent<Collider>());
        }
    }
}
