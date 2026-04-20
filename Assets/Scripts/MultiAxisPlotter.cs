using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

namespace IARVR.Visualization
{
    /// <summary>
    /// Generates an interactive parallel-coordinates chart from a CSV file.
    ///
    /// Responsibilities:
    ///   - Parse CSV data via <see cref="CsvDataModel"/>.
    ///   - Build one vertical cylinder per column (axis) with tick labels.
    ///   - Render polyline segments connecting axis values for each data row.
    ///   - Show/hide segments dynamically based on axis proximity.
    ///   - Expose highlight and brushing API consumed by child components.
    /// </summary>
    public class MultiAxisPlotter : MonoBehaviour
    {
        // --- Serialized fields ----------------------------------------------

        [Header("Data Source")]
        [Tooltip("CSV file where the first row is headers and each subsequent row is a data record.")]
        [SerializeField] private TextAsset _csvFile;

        [Header("Axis Appearance")]
        [Tooltip("Number of tick subdivisions per axis.")]
        [SerializeField] private int   _subdivisions  = 5;
        private float _axisRadius    = 0.05f;
        private float _tickSize      = 0.1f;
        private float _labelOffset   = 0.25f;
        private float _axisSpacing   = 1f;
        [SerializeField] private GameObject _tickPrefab;
        [SerializeField] private GameObject _labelPrefab;

        [Header("Connections")]
        [Tooltip("Maximum world-space distance between two axes for their connecting lines to be visible.")]
        private float _connectionThreshold = 2.5f;

        // --- Public properties ----------------------------------------------

        /// <summary>Current visual height of all axes in world units.</summary>
        public float maxVisualHeight = 5f;

        // Legacy public accessors kept for Inspector-assigned components
        public int   subdivisions         => _subdivisions;
        public float axisRadius           => _axisRadius;
        public float tickSize             => _tickSize;
        public float labelOffset          => _labelOffset;
        public float axisSpacing          => _axisSpacing;
        public float connectionThreshold  => _connectionThreshold;
        public GameObject tickPrefab      => _tickPrefab;
        public GameObject labelPrefab     => _labelPrefab;

        // --- Constants ------------------------------------------------------

        private const float BaseReferenceHeight  = 5f;
        private const float AxisColorR           = 0.2f;
        private const float AxisColorG           = 0.5f;
        private const float AxisColorB           = 1.0f;
        private const float AxisColorA           = 0.8f;
        private const float LabelAlpha           = 0.8f;
        private const float AxisLabelFontSize    = 2f;
        private const float TickLabelFontSize    = 1.5f;
        private const float LineDefaultWidth     = 0.03f;
        private const float LineBaseWidth        = 0.02f;
        private const float LineAlpha            = 0.6f;
        private const float LineColliderThickness = 0.05f;
        private const float ColliderHeightMargin = 1.1f;

        // --- Private state --------------------------------------------------

        private CsvDataModel _data;
        private Transform[]  _axisCylinders;
        private Transform[]  _axisParents;

        private List<Dictionary<(int col1, int col2), LineRenderer>> _rowConnections
            = new List<Dictionary<(int, int), LineRenderer>>();

        private List<float[]> _lineData    = new List<float[]>();
        private bool          _dirty;

        // --- Unity lifecycle ------------------------------------------------

        private void Start()
        {
            if (_csvFile == null)
            {
                Debug.LogError("[MultiAxisPlotter] No CSV file assigned.");
                return;
            }

            _data = new CsvDataModel(_csvFile);
            CreateAxes();
            CreateAllConnections();
            _dirty = true;
        }

        private void Update()
        {
            if (!_dirty) return;

            UpdateConnections();
            _dirty = false;
        }

        // --- Public API -----------------------------------------------------

        /// <summary>Schedules a connection position refresh on the next frame.</summary>
        public void MarkConnectionsDirty()
        {
            Debug.Log("[Plotter] MarkConnectionsDirty called", this);
            _dirty = true;
        }

        /// <summary>Returns the parent transform of the axis at <paramref name="index"/>.</summary>
        public Transform GetAxisParent(int index)
        {
            if (_axisParents == null || index < 0 || index >= _axisParents.Length)
                return null;

            return _axisParents[index];
        }

        /// <summary>Converts a world point on an axis into its corresponding data value.</summary>
        public float ValueFromWorldPoint(int col, Vector3 worldPoint)
        {
            Transform parent = GetAxisParent(col);
            if (parent == null) return 0f;

            Vector3 local = parent.InverseTransformPoint(worldPoint);
            float t = Mathf.Clamp01(local.y / maxVisualHeight);
            return Mathf.Lerp(_data.MinValues[col], _data.MaxValues[col], t);
        }

        /// <summary>Converts a local Y height on an axis into its corresponding data value.</summary>
        public float ValueFromHeight(int col, float localY)
        {
            float t = Mathf.Clamp01(localY / maxVisualHeight);
            return Mathf.Lerp(_data.MinValues[col], _data.MaxValues[col], t);
        }

        /// <summary>
        /// Highlights all segments of the given row with <paramref name="color"/>.
        /// Only affects currently visible (enabled) segments.
        /// </summary>
        public void HighlightRow(int rowIndex, Color color)
        {
            if (rowIndex < 0 || rowIndex >= _rowConnections.Count) return;

            foreach (var kvp in _rowConnections[rowIndex])
            {
                LineRenderer lr = kvp.Value;
                if (lr != null && lr.enabled)
                    lr.startColor = lr.endColor = color;
            }
        }

        /// <summary>
        /// Highlights rows whose value on <paramref name="axisIndex"/> falls within
        /// [<paramref name="minVal"/>, <paramref name="maxVal"/>] using <paramref name="color"/>.
        /// All other rows revert to their default color.
        /// </summary>
        public void HighlightRange(int axisIndex, float minVal, float maxVal, Color color)
        {
            for (int r = 0; r < _lineData.Count; r++)
            {
                float val    = _lineData[r][axisIndex];
                bool inRange = val >= minVal && val <= maxVal;
                Color target = inRange ? color : Color.red;

                foreach (var kvp in _rowConnections[r])
                {
                    LineRenderer lr = kvp.Value;
                    if (lr != null)
                        lr.startColor = lr.endColor = target;
                }
            }
        }

        /// <summary>
        /// Updates axis cylinder scale, label positions, and line thickness
        /// to match the current <see cref="maxVisualHeight"/>.
        /// </summary>
        public void UpdateAxesHeight()
        {
            if (_axisCylinders == null) return;

            float scaleFactor = Mathf.Max(0.0001f, maxVisualHeight / BaseReferenceHeight);

            for (int i = 0; i < _axisCylinders.Length; i++)
            {
                if (_axisCylinders[i] == null) continue;

                ResizeAxisCylinder(i, scaleFactor);
                RepositionAxisLabel(i, scaleFactor);
                RepositionTickLabels(i, scaleFactor);
            }

            UpdateLineThickness(scaleFactor);
        }

        // --- Initialization -------------------------------------------------

        private void CreateAxes()
        {
            _axisCylinders = new Transform[_data.NumColumns];
            _axisParents   = new Transform[_data.NumColumns];

            for (int col = 0; col < _data.NumColumns; col++)
            {
                GameObject axisRoot = CreateAxisRoot(col);
                _axisParents[col]   = axisRoot.transform;
                _axisCylinders[col] = CreateAxisCylinder(axisRoot, col);

                CreateAxisLabel(axisRoot, _data.Headers[col]);
                CreateTickLabels(axisRoot, col);

                // Attach brusher to the cylinder itself so raycasts target it
                var brusher = _axisCylinders[col].gameObject.AddComponent<AxisBrusher>();
                brusher.AxisIndex = col;
            }
        }

        private GameObject CreateAxisRoot(int col)
        {
            var axisRoot = new GameObject($"Axis_{_data.Headers[col]}");
            axisRoot.transform.SetParent(transform);
            axisRoot.transform.localPosition = new Vector3(col * _axisSpacing, 0f, 0f);
            axisRoot.AddComponent<AxisSelectable>();
            return axisRoot;
        }

        private Transform CreateAxisCylinder(GameObject parent, int col)
        {
            float h    = maxVisualHeight;
            var   axis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axis.transform.SetParent(parent.transform);
            axis.transform.localScale    = new Vector3(_axisRadius, h / 2f, _axisRadius);
            axis.transform.localPosition = new Vector3(0f, h / 2f, 0f);

            // Slightly enlarge the collider so rays can hit the very edges
            if (axis.TryGetComponent<CapsuleCollider>(out var col2))
            {
                col2.height = h * ColliderHeightMargin;
                col2.center = new Vector3(0f, (h / 2f) - (h * 0.5f), 0f);
            }

            var rend = axis.GetComponent<Renderer>();
            rend.material       = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(AxisColorR, AxisColorG, AxisColorB, AxisColorA);

            return axis.transform;
        }

        private void CreateAxisLabel(GameObject parent, string header)
        {
            var label = new GameObject("AxisLabel");
            label.transform.SetParent(parent.transform);
            label.transform.localPosition = new Vector3(0f, maxVisualHeight + 0.2f, 0f);

            var tmp        = label.AddComponent<TextMeshPro>();
            tmp.text       = header;
            tmp.fontSize   = AxisLabelFontSize;
            tmp.color      = Color.white;
            tmp.alignment  = TextAlignmentOptions.Center;

            label.AddComponent<FaceCamera>();
        }

        private void CreateTickLabels(GameObject parent, int col)
        {
            for (int i = 0; i <= _subdivisions; i++)
            {
                float t     = i / (float)_subdivisions;
                float yPos  = t * maxVisualHeight;
                float value = Mathf.Lerp(_data.MinValues[col], _data.MaxValues[col], t);

                CreateTick(parent, col, i, yPos);
                CreateTickLabel(parent, col, i, yPos, value);
            }
        }

        private void CreateTick(GameObject parent, int col, int index, float yPos)
        {
            if (_tickPrefab == null) return;

            var tick = Instantiate(_tickPrefab, parent.transform);
            tick.transform.localPosition = new Vector3(_axisRadius + 0.05f, yPos, 0f);
            tick.transform.localScale    = Vector3.one * _tickSize;
            tick.name = $"Tick_{col}_{index}";
        }

        private void CreateTickLabel(GameObject parent, int col, int index, float yPos, float value)
        {
            GameObject lbl = _labelPrefab != null
                ? Instantiate(_labelPrefab, parent.transform)
                : new GameObject();

            lbl.name = $"TickLabel_{col}_{index}";
            lbl.transform.SetParent(parent.transform);
            lbl.transform.localPosition = new Vector3(-_axisRadius - _labelOffset, yPos, 0f);
            lbl.transform.localRotation = Quaternion.identity;

            var textMesh       = lbl.GetComponent<TextMeshPro>() ?? lbl.AddComponent<TextMeshPro>();
            textMesh.text      = value.ToString("0.0");
            textMesh.fontSize  = TickLabelFontSize;
            textMesh.color     = new Color(1f, 1f, 1f, LabelAlpha);
            textMesh.alignment = TextAlignmentOptions.Center;

            lbl.AddComponent<FaceCamera>();
        }

        private void CreateAllConnections()
        {
            for (int row = 0; row < _data.Rows.Count; row++)
            {
                _lineData.Add(_data.Rows[row]);
                _rowConnections.Add(CreateConnectionsForRow(row));
            }
        }

        private Dictionary<(int, int), LineRenderer> CreateConnectionsForRow(int row)
        {
            var dict = new Dictionary<(int, int), LineRenderer>();

            for (int i = 0; i < _data.NumColumns; i++)
            {
                for (int j = i + 1; j < _data.NumColumns; j++)
                {
                    var lr = CreateSegment(row, i, j);
                    dict[(i, j)] = lr;
                }
            }

            return dict;
        }

        private LineRenderer CreateSegment(int row, int colA, int colB)
        {
            var segGO = new GameObject($"Row{row}_Seg{colA}_{colB}");
            segGO.transform.SetParent(transform);

            var lr             = segGO.AddComponent<LineRenderer>();
            lr.positionCount   = 2;
            lr.startWidth      = lr.endWidth = LineDefaultWidth;
            lr.material        = new Material(Shader.Find("Sprites/Default"));
            lr.colorGradient   = BuildUniformGradient(Color.red, LineAlpha);
            lr.enabled         = false;

            var lineSel      = segGO.AddComponent<LineSelectable>();
            lineSel.RowIndex = row;
            lineSel.SetPlotter(this);

            return lr;
        }

        // --- Per-frame update -----------------------------------------------

        private void UpdateConnections()
        {
            for (int r = 0; r < _rowConnections.Count; r++)
            {
                float[] rowData = _lineData[r];

                foreach (var kvp in _rowConnections[r])
                {
                    int          colA = kvp.Key.Item1;
                    int          colB = kvp.Key.Item2;
                    LineRenderer lr   = kvp.Value;

                    float dist = Vector3.Distance(
                        _axisParents[colA].position,
                        _axisParents[colB].position);

                    

                    if (dist < _connectionThreshold)
                    {
                        Vector3 p1 = GetWorldPoint(colA, rowData[colA]);
                        Vector3 p2 = GetWorldPoint(colB, rowData[colB]);

                        lr.SetPosition(0, p1);
                        lr.SetPosition(1, p2);
                        lr.enabled = true;

                        UpdateLineCollider(lr.gameObject, p1, p2, LineColliderThickness);
                    }
                    else
                    {
                        lr.enabled = false;

                        if (lr.TryGetComponent<CapsuleCollider>(out var col))
                            col.enabled = false;
                    }
                }
            }
        }

        // --- Height update helpers ------------------------------------------

        private void ResizeAxisCylinder(int index, float scaleFactor)
        {
            Transform axis = _axisCylinders[index];
            axis.localScale    = new Vector3(
                _axisRadius * scaleFactor,
                maxVisualHeight / 2f,
                _axisRadius * scaleFactor);
            axis.localPosition = new Vector3(0f, maxVisualHeight / 2f, 0f);

            if (axis.TryGetComponent<CapsuleCollider>(out var col))
            {
                col.direction = 1;
                col.height    = maxVisualHeight;
                col.radius    = axis.localScale.x * ColliderHeightMargin;
                col.center    = Vector3.zero;
            }
        }

        private void RepositionAxisLabel(int index, float scaleFactor)
        {
            Transform label = _axisParents[index].Find("AxisLabel");
            if (label == null) return;

            label.localPosition = new Vector3(0f, maxVisualHeight + 0.2f * scaleFactor, 0f);
            label.localScale    = Vector3.one * scaleFactor;
        }

        private void RepositionTickLabels(int axisIndex, float scaleFactor)
        {
            for (int tick = 0; tick <= _subdivisions; tick++)
            {
                Transform tickLabel = _axisParents[axisIndex].Find($"TickLabel_{axisIndex}_{tick}");
                if (tickLabel == null) continue;

                float t    = tick / (float)_subdivisions;
                float yPos = t * maxVisualHeight;

                tickLabel.localPosition = new Vector3(
                    -(_axisRadius * scaleFactor) - _labelOffset * scaleFactor,
                    yPos,
                    0f);
                tickLabel.localScale = Vector3.one * scaleFactor;

                if (tickLabel.TryGetComponent<TextMeshPro>(out var tmp))
                {
                    float value = Mathf.Lerp(_data.MinValues[axisIndex], _data.MaxValues[axisIndex], t);
                    tmp.text = value.ToString("0.0");
                }
            }
        }

        private void UpdateLineThickness(float scaleFactor)
        {
            foreach (var lr in GetComponentsInChildren<LineRenderer>(includeInactive: true))
                lr.startWidth = lr.endWidth = LineBaseWidth * scaleFactor;
        }

        // --- Utility --------------------------------------------------------

        private Vector3 GetWorldPoint(int col, float val)
        {
            float t      = (val - _data.MinValues[col]) / (_data.MaxValues[col] - _data.MinValues[col]);
            float localY = t * maxVisualHeight;
            return _axisCylinders[col].parent.TransformPoint(new Vector3(0f, localY, 0f));
        }

        private static void UpdateLineCollider(
            GameObject lineGO, Vector3 start, Vector3 end, float thickness)
        {
            var col = lineGO.GetComponent<CapsuleCollider>()
                      ?? lineGO.AddComponent<CapsuleCollider>();

            col.enabled   = true;
            col.direction = 2; // Z-axis
            col.height    = Vector3.Distance(start, end);
            col.radius    = thickness / 2f;

            lineGO.transform.position = (start + end) * 0.5f;
            lineGO.transform.rotation = Quaternion.FromToRotation(Vector3.forward, end - start);
        }

        private static Gradient BuildUniformGradient(Color color, float alpha)
        {
            return new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(alpha, 0f),
                    new GradientAlphaKey(alpha, 1f)
                }
            };
        }
    }
}