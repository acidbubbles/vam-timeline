using UnityEngine;

namespace VamTimeline
{
    public class LineDrawer : MonoBehaviour
    {
        private bool _dirty = false;
        private Material _material;
        private Gradient _colorGradient;
        private Vector3[] _points;
        private Vector3[] _vertices;
        private Color[] _colors;
        private int[] _indices;
        private Vector2[] _uv;
        private Vector3[] _normals;
        private int _previousIndicesCount = -1;

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
            var verticesCount = points.Length * 2;
            if (_previousIndicesCount != verticesCount)
            {
                _colors = new Color[verticesCount];
                _vertices = new Vector3[verticesCount];
                _indices = new int[verticesCount];
                _uv = new Vector2[verticesCount];
                _normals = new Vector3[verticesCount];
            }
            var p = 0;
            for (var i = 0; i < verticesCount - 2; i += 2)
            {
                if (_previousIndicesCount != verticesCount)
                {
                    _colors[i] = colorGradient.Evaluate(p / (float)points.Length);
                    _colors[i + 1] = colorGradient.Evaluate((p + 1) / (float)points.Length);
                }
                _vertices[i] = points[p];
                _vertices[i + 1] = points[p + 1];
                p++;
            }
            for (var i = 0; i < verticesCount; i++)
            {
                _indices[i] = i;
            }
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.uv = _uv;
            _mesh.normals = _normals;
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
            _previousIndicesCount = verticesCount;
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
