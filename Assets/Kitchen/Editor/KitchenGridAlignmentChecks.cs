using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenGridAlignmentChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            KitchenGridPlacement authored = Object.FindObjectsByType<KitchenGridPlacement>(FindObjectsSortMode.None)
                .Single(grid => grid.gameObject.scene.name == "Kitchen");
            var buttons = authored.GetComponentsInChildren<KitchenGridCell>().OrderBy(button => button.index).ToArray();
            var labels = authored.GetComponentsInChildren<TextMesh>().Where(text => text.text.StartsWith("BUILD ")).ToArray();
            check(buttons.Length == 4 && labels.Length == 4 && authored.cells.Length == 4 && authored.cellIndicators.Length == 4,
                "grid-alignment/four-authored-buttons-labels-and-pads");

            foreach (KitchenGridCell button in buttons)
            {
                int number = button.index + 1;
                TextMesh nearestLabel = labels.OrderBy(text => FlatDistance(text.transform.position, button.transform.position)).First();
                Renderer nearestPad = authored.cellIndicators.OrderBy(pad => FlatDistance(pad.bounds.center, button.transform.position)).First();
                KitchenAction action = button.GetComponent<KitchenAction>();
                check(nearestLabel.text == "BUILD " + number && nearestPad == authored.cellIndicators[button.index],
                    "grid-alignment/build-" + number + "/visible-label-button-and-pad-agree");
                check(action.label == "Build in cell " + number && action.action.GetPersistentEventCount() == 1
                    && action.action.GetPersistentTarget(0) == button && action.action.GetPersistentMethodName(0) == "Place",
                    "grid-alignment/build-" + number + "/desktop-and-xr-button-use-same-cell");
            }

            // Exercise each saved appliance prefab on every numbered pad through the
            // actual button callbacks, without occupying the authored grid's slots.
            for (int module = 1; module < authored.modulePrefabs.Length; module++)
            {
                var fixture = new GameObject("Grid placement alignment check");
                var grid = fixture.AddComponent<KitchenGridPlacement>();
                grid.modulePrefabs = authored.modulePrefabs;
                grid.cells = authored.cells;
                grid.cellIndicators = authored.cellIndicators;
                grid.equipmentOffset = authored.equipmentOffset;
                grid.equipmentYawDegrees = authored.equipmentYawDegrees;
                try
                {
                    foreach (KitchenGridCell button in buttons)
                    {
                        int index = button.index;
                        Vector3 pad = authored.cellIndicators[index].bounds.center;
                        Vector3 expected = new Vector3(pad.x, authored.cells[index].position.y, pad.z);
                        KitchenAction action = button.GetComponent<KitchenAction>();
                        button.grid = grid;
                        try
                        {
                            grid.Select(0);
                            action.action.Invoke();
                            Transform foundation = fixture.transform.Find(grid.modulePrefabs[0].name + " - Tile " + (index + 1));
                            check(foundation && Vector3.Distance(foundation.position, expected) < .001f,
                                "grid-alignment/" + module + "/base-on-visible-pad-" + (index + 1));
                            grid.Select(module);
                            action.action.Invoke();
                            Transform equipment = fixture.transform.Find(grid.modulePrefabs[module].name + " - Tile " + (index + 1));
                            expected += grid.cells[index].TransformVector(grid.equipmentOffset);
                            check(equipment && Vector3.Distance(equipment.position, expected) < .001f,
                                "grid-alignment/" + grid.modulePrefabs[module].name + "/on-visible-pad-" + (index + 1));
                            check(equipment && Quaternion.Angle(equipment.rotation,
                                grid.cells[index].rotation * Quaternion.Euler(0, index < 2 ? 180 : 0, 0)) < .1f,
                                "grid-alignment/" + grid.modulePrefabs[module].name + "/keeps-facing-on-pad-" + (index + 1));
                        }
                        finally { button.grid = authored; }
                    }
                }
                finally { Object.DestroyImmediate(fixture); }
            }

            CheckFurtherMovement(check, authored.modulePrefabs[0]);
            // Fixture previews share renderers with the real grid; restore real occupancy colors.
            authored.Select(authored.Selected);
            yield return null;
        }

        static void CheckFurtherMovement(Action<bool, string> check, GameObject foundationPrefab)
        {
            var root = new GameObject("Moved expansion pad check");
            var cell = new GameObject("Original cell anchor").transform;
            var padObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.SetParent(root.transform, false);
            padObject.transform.SetParent(cell, false);
            padObject.transform.localScale = new Vector3(1.13f, .025f, 1.06f);
            var grid = root.AddComponent<KitchenGridPlacement>();
            grid.modulePrefabs = new[] { foundationPrefab };
            grid.cells = new[] { cell };
            grid.cellIndicators = new[] { padObject.GetComponent<Renderer>() };
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(80, .2f, 80), Quaternion.Euler(0, 35, 0));
                padObject.transform.localPosition = new Vector3(2.3f, .015f, -1.6f);
                grid.Place(0);
                Transform built = root.transform.Find(foundationPrefab.name + " - Tile 1");
                Vector3 expected = cell.TransformPoint(new Vector3(2.3f, 0, -1.6f));
                check(built && Vector3.Distance(built.position, expected) < .001f,
                    "grid-alignment/moved-pad-child-and-rotated-parent-determine-placement");
                Object.DestroyImmediate(built.gameObject);
                root.transform.position += new Vector3(-3, .1f, 2);
                padObject.transform.localPosition += new Vector3(.7f, 0, .4f);
                grid.Place(0);
                built = root.transform.Find(foundationPrefab.name + " - Tile 1");
                expected = cell.TransformPoint(new Vector3(3f, 0, -1.2f));
                check(built && Vector3.Distance(built.position, expected) < .001f,
                    "grid-alignment/later-pad-and-parent-edits-do-not-use-cached-coordinates");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 difference = a - b;
            return difference.x * difference.x + difference.z * difference.z;
        }
    }
}
