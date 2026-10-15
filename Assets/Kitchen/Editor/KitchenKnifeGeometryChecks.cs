using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Combined visual meshes must not make the handle part of the cutting detector.</summary>
    public static class KitchenKnifeGeometryChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var combinedRoot = Root("Combined knife geometry check", new Vector3(210f, 4f, 210f));
            var legacyRoot = Root("Legacy knife geometry check", new Vector3(212f, 4f, 210f));
            try
            {
                Cube(combinedRoot.transform, "Combined blade and handle visual", Vector3.zero, new Vector3(.42f, .07f, .014f));
                var reference = new GameObject("Blade detector reference").transform;
                reference.SetParent(combinedRoot.transform, false);
                reference.localPosition = new Vector3(.025f, .01f, 0f);
                var knife = combinedRoot.AddComponent<KitchenKnife>();
                knife.useExplicitBladeRegion = true;
                knife.bladeReference = reference;
                knife.bladeLocalBounds = new Bounds(new Vector3(.08f, 0f, 0f), new Vector3(.19f, .055f, .012f));
                knife.handleLocalCenter = new Vector3(-.125f, .005f, 0f);
                combinedRoot.SetActive(true);
                Freeze(combinedRoot);
                check(Vector3.Distance(knife.BladeCenter, reference.TransformPoint(knife.bladeLocalBounds.center)) < .0001f,
                    "knife-geometry/combined-mesh-uses-explicit-blade-only-region");
                check(Vector3.Distance(knife.HandleCenter, knife.transform.TransformPoint(knife.handleLocalCenter)) < .0001f,
                    "knife-geometry/combined-mesh-uses-explicit-root-local-grip");
                check(Mathf.Abs(knife.BladeCenter.y - knife.BladeEdge.y - knife.bladeLocalBounds.extents.y) < .0001f,
                    "knife-geometry/cutting-edge-uses-region-height-not-whole-mesh");
                combinedRoot.transform.rotation = Quaternion.Euler(52f, 80f, 100f);
                knife.PrepareDesktopGrip(null);
                check(Vector3.Dot(knife.BladeDownDirection, Vector3.down) > .999f,
                    "knife-geometry/combined-mesh-still-auto-grips-edge-down");

                Cube(legacyRoot.transform, "Knife blade", new Vector3(.025f, 0f, 0f), new Vector3(.22f, .065f, .009f));
                Cube(legacyRoot.transform, "Knife handle", new Vector3(-.16f, 0f, 0f), new Vector3(.16f, .045f, .035f));
                var legacy = legacyRoot.AddComponent<KitchenKnife>();
                legacyRoot.SetActive(true);
                Freeze(legacyRoot);
                check(!legacy.useExplicitBladeRegion && Vector3.Distance(legacy.BladeCenter,
                    legacy.transform.TransformPoint(new Vector3(.025f, 0f, 0f))) < .0001f,
                    "knife-geometry/legacy-blade-extraction-unchanged");
                check(Vector3.Distance(legacy.HandleCenter, legacy.transform.TransformPoint(new Vector3(-.16f, 0f, 0f))) < .0001f,
                    "knife-geometry/legacy-handle-extraction-unchanged");
                yield return null;
            }
            finally
            {
                combinedRoot.SetActive(false); legacyRoot.SetActive(false);
                Object.Destroy(combinedRoot); Object.Destroy(legacyRoot);
            }
            yield return null;
        }

        static GameObject Root(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = position;
            return root;
        }

        static void Cube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localScale = scale;
        }

        static void Freeze(GameObject root)
        {
            var body = root.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }
    }
}
