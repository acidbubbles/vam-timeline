using UnityEngine;

namespace VamTimeline
{
    public class LineDrawer : MonoBehaviour
    {
        private bool _dirty = false;
        private Material _material;
        private Gradient _colorGradient;
        private Vector3[] _points;
        public Material material { get { return _material; } set { _material = value; _dirty = true; } }
        public Gradient colorGradient { get { return _colorGradient; } set { _colorGradient = value; _dirty = true; } }
        public Vector3[] points { get { return _points; } set { _points = value; _dirty = true; } }

        private readonly Mesh _mesh;

        public LineDrawer()
        {
            _mesh = new Mesh();
        }

        public void Recalculate()
        {
            var verticesCount = (points.Length) * 2;
            var vertices = new Vector3[verticesCount];
            var colors = new Color[verticesCount];
            var p = 0;
            for (var i = 0; i < verticesCount - 2; i += 2)
            {
                colors[i] = colorGradient.Evaluate(p / (float)points.Length);
                colors[i + 1] = colorGradient.Evaluate((p + 1) / (float)points.Length);
                vertices[i] = points[p];
                vertices[i + 1] = points[p + 1];
                p++;
            }
            var indices = new int[verticesCount];
            for (var i = 0; i < verticesCount; i++)
            {
                indices[i] = i;
            }
            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.uv = new Vector2[verticesCount];
            _mesh.normals = new Vector3[verticesCount];
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
        }

        public void Update()
        {
            if (_dirty)
            {
                if (points != null) Recalculate();
                _dirty = false;
            }
            Graphics.DrawMesh(_mesh, transform.parent.localToWorldMatrix, material, 0);
        }
    }
}
