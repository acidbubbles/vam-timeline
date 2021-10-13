using UnityEngine;

namespace VamTimeline
{
    public static class TimelinePrefabs
    {
        public static readonly Transform cube = InitCube();

        private static Transform InitCube()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            var cs = go.GetComponent<Collider>();
            Object.DestroyImmediate(cs);
            return go.transform;
        }

        public static void Destroy()
        {
            Object.Destroy(cube.gameObject);
        }
    }
}
