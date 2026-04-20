using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

public class MultiAxisPlotter : MonoBehaviour
{
    /*
        Responsabilidad general:
        Genera un gráfico de ejes paralelos a partir de un CSV.
        Cada columna del CSV = un eje vertical.
        Cada fila del CSV = una polilínea que conecta valores entre ejes.

        a) CalculateMinMax()

            Lee el CSV.

            Calcula mínimos y máximos de cada columna para normalizar valores.

            Esto permite mapear valores numéricos a alturas en los ejes.

        b) CreateAxes()

            Crea un cilindro vertical para cada columna del CSV.

            Le agrega:

            Ticks (marcas de subdivisiones).

            Labels (nombres de ejes y valores numéricos).

            El script SelectableObject → para que cada eje se pueda resaltar al apuntarlo.

        c) CreateAllConnections()

            Para cada fila de datos:

            Guarda los valores en lineData.

            Crea segmentos (LineRenderer) que conectan pares de ejes (i, j).

            Inicialmente están deshabilitados (lr.enabled = false o lr.gameObject.SetActive(false);).

            Se les agrega:

            SelectableObject (para resaltar).

            LineSelectable (para permitir selección con láser en VR).

        d) Update() / UpdateConnections()

            En cada frame:

            Calcula las posiciones entre ejes según los datos.

            Si dos ejes están a menos de connectionThreshold, activa la línea y la actualiza.

            Actualiza también el collider de la línea para que siga bien los puntos.

        e) GetWorldPoint()

            Convierte un valor normalizado (0–1 entre min y max) a una posición en el espacio del gráfico.
    */

    [Header("CSV Settings")]
    public TextAsset csvFile;
    public int subdivisions = 5;
    public float axisRadius = 0.05f;
    public float tickSize = 0.1f;
    public float labelOffset = 0.25f;
    public float maxVisualHeight = 5f;
    public GameObject tickPrefab;
    public GameObject labelPrefab;
    public float axisSpacing = 2f;

    [Header("Dynamic Connections")]
    public float connectionThreshold = 1.5f;

    private CsvDataModel _data;

    private Transform[] axisCylinders;
    private Transform[] axisParents;

    private List<Dictionary<(int, int), LineRenderer>> rowConnections = new List<Dictionary<(int, int), LineRenderer>>();
    private List<float[]> lineData = new List<float[]>();

    private bool _connectionsDirty = true;

    void Start()
    {
        if (csvFile == null)
        {
            Debug.LogError("No CSV assigned!");
            return;
        }

        _data = new CsvDataModel(csvFile);  // una sola lectura
        CreateAxes();
        CreateAllConnections();
    }

    


    void CreateAxes()
    {
        
        axisCylinders = new Transform[_data.NumColumns];
        axisParents = new Transform[_data.NumColumns];

        for (int col = 0; col < _data.NumColumns; col++)
        {
            GameObject axisGO = new GameObject("Axis_" + _data.Headers[col]); 
            axisGO.transform.SetParent(transform);

            axisGO.transform.localPosition = new Vector3(col * axisSpacing, 0, 0);

            axisGO.AddComponent<AxisSelectable>();
            axisParents[col] = axisGO.transform;

            float axisHeight = maxVisualHeight;
            GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axis.transform.SetParent(axisGO.transform);
            axis.transform.localScale = new Vector3(axisRadius, axisHeight / 2f, axisRadius);
            axis.transform.localPosition = new Vector3(0, axisHeight / 2f, 0);

            var axisCollider = axis.GetComponent<CapsuleCollider>();
            if (axisCollider != null)
            {
                axisCollider.height = axisHeight * 1.1f;
                axisCollider.center = new Vector3(0, (axisHeight / 2f) - (axisHeight * 0.5f), 0);
            }

            Renderer rend = axis.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(0.2f, 0.5f, 1f, 0.8f);

            axisCylinders[col] = axis.transform;

            GameObject axisLabel = new GameObject("AxisLabel");
            axisLabel.transform.SetParent(axisGO.transform);
            axisLabel.transform.localPosition = new Vector3(0, axisHeight + 0.2f, 0);
            TextMeshPro tmp = axisLabel.AddComponent<TextMeshPro>();
            axisLabel.AddComponent<FaceCamera>();
            tmp.text = _data.Headers[col]; // ✅ _data.Headers
            tmp.fontSize = 2;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            var brusher = axis.AddComponent<AxisBrusher>();
            brusher.axisIndex = col;

            for (int i = 0; i <= subdivisions; i++)
            {
                float t = i / (float)subdivisions;
                float yPos = t * axisHeight;
                float value = _data.MinValues[col] + t * (_data.MaxValues[col] - _data.MinValues[col]); // ✅ _data

                if (tickPrefab != null)
                {
                    GameObject tick = Instantiate(tickPrefab, axisGO.transform);
                    tick.transform.localPosition = new Vector3(axisRadius + 0.05f, yPos, 0);
                    tick.transform.localScale = new Vector3(tickSize, tickSize, tickSize);
                    tick.name = $"Tick_{col}_{i}";
                }

                GameObject lbl;
                if (labelPrefab != null)
                    lbl = Instantiate(labelPrefab, axisGO.transform);
                else
                {
                    lbl = new GameObject($"TickLabel_{col}_{i}");
                    lbl.transform.SetParent(axisGO.transform);
                }

                lbl.name = $"TickLabel_{col}_{i}";
                lbl.transform.localPosition = new Vector3(-axisRadius - labelOffset, yPos, 0);
                lbl.transform.localRotation = Quaternion.identity;

                TextMeshPro textMesh = lbl.GetComponent<TextMeshPro>();
                if (textMesh == null)
                    textMesh = lbl.AddComponent<TextMeshPro>();

                textMesh.text = value.ToString("0.0");
                textMesh.fontSize = 1.5f;
                textMesh.color = new Color(1f, 1f, 1f, 0.8f);
                textMesh.alignment = TextAlignmentOptions.Center;
                lbl.AddComponent<FaceCamera>();
            }
        }
    }

    void CreateAllConnections()
    {
        for (int row = 0; row < _data.Rows.Count; row++)
        {
            lineData.Add(_data.Rows[row]); 

            var connDict = new Dictionary<(int, int), LineRenderer>();

            for (int i = 0; i < _data.NumColumns; i++)
            {
                for (int j = i + 1; j < _data.NumColumns; j++)
                {
                    GameObject segGO = new GameObject($"Row{row}_Seg{i}_{j}");
                    segGO.transform.SetParent(transform);
                    LineRenderer lr = segGO.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.startWidth = 0.03f;
                    lr.endWidth = 0.03f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));

                    lr.colorGradient = new Gradient()
                    {
                        colorKeys = new GradientColorKey[]
                        {
                            new GradientColorKey(Color.red, 0f),
                            new GradientColorKey(Color.red, 1f)
                        },
                        alphaKeys = new GradientAlphaKey[]
                        {
                            new GradientAlphaKey(0.6f, 0f),
                            new GradientAlphaKey(0.6f, 1f)
                        }
                    };
                    lr.enabled = false;

                    var lineSel = segGO.AddComponent<LineSelectable>();
                    lineSel.rowIndex = row;
                    lineSel.SetPlotter(this);

                    connDict[(i, j)] = lr;
                }
            }

            rowConnections.Add(connDict);
        }
    }

    void Update()
    {
        if (_connectionsDirty)
        {
            UpdateConnections();
            _connectionsDirty = false;
        }
    }

    // Método público para que otros scripts marquen que algo cambió
    public void MarkConnectionsDirty()
    {
        _connectionsDirty = true;
    }

    void UpdateConnections()
    {
        for (int r = 0; r < rowConnections.Count; r++)
        {
            float[] rowData = lineData[r];
            var connDict = rowConnections[r];

            foreach (var kvp in connDict)
            {
                int i = kvp.Key.Item1;
                int j = kvp.Key.Item2;
                LineRenderer lr = kvp.Value;
                GameObject go = lr.gameObject;

                float dist = Vector3.Distance(axisParents[i].position, axisParents[j].position);

                if (dist < connectionThreshold)
                {
                    Vector3 p1 = GetWorldPoint(i, rowData[i]);
                    Vector3 p2 = GetWorldPoint(j, rowData[j]);
                    lr.SetPosition(0, p1);
                    lr.SetPosition(1, p2);
                    lr.enabled = true;
                    //lr.gameObject.SetActive(true);

                    // ✅ actualizamos collider de la línea
                    UpdateLineCollider(go, p1, p2, 0.05f);
                }
                else
                {
                    lr.enabled = false;
                    //lr.gameObject.SetActive(false);
                    var col = go.GetComponent<CapsuleCollider>();
                    if (col) col.enabled = false;
                }
            }
        }
    }

    void UpdateLineCollider(GameObject lineGO, Vector3 start, Vector3 end, float thickness)
    {
        CapsuleCollider collider = lineGO.GetComponent<CapsuleCollider>();
        if (!collider) collider = lineGO.AddComponent<CapsuleCollider>();

        collider.enabled = true;
        collider.direction = 2; // Z axis
        float length = Vector3.Distance(start, end);
        collider.height = length;
        collider.radius = thickness / 2f;

        lineGO.transform.position = (start + end) / 2f;
        lineGO.transform.rotation = Quaternion.FromToRotation(Vector3.forward, end - start);
    }

    Vector3 GetWorldPoint(int col, float val)
    {
        float t = (val - _data.MinValues[col]) / (_data.MaxValues[col] - _data.MinValues[col]);
        float localY = t * maxVisualHeight;
        Vector3 localPoint = new Vector3(0f, localY, 0f);
        return axisCylinders[col].parent.TransformPoint(localPoint);
    }


    //Used to select a line through all the axis
    public void HighlightRow(int rowIndex, Color color)
    {
        if (rowIndex < 0 || rowIndex >= rowConnections.Count) return;

        var connDict = rowConnections[rowIndex];
        foreach (var kvp in connDict)
        {
            LineRenderer lr = kvp.Value;
            if (lr != null && lr.enabled)
            {
                lr.startColor = color;
                lr.endColor = color;
            }
        }
    }

    //Functions used for brushing
    public float ValueFromHeight(int col, float localY)
    {
        float t = Mathf.Clamp01(localY / maxVisualHeight);
        return Mathf.Lerp(_data.MinValues[col], _data.MaxValues[col], t);
    }

    public void HighlightRange(int axisIndex, float minVal, float maxVal, Color color)
    {
        for (int r = 0; r < lineData.Count; r++)
        {
            float val = lineData[r][axisIndex];
            bool inRange = val >= minVal && val <= maxVal;

            foreach (var kvp in rowConnections[r])
            {
                LineRenderer lr = kvp.Value;
                if (lr == null) continue;
                lr.startColor = lr.endColor = inRange ? color : Color.red; // o restaurar color original
            }
        }
    }

    // Devuelve el transform del parent del eje (el GameObject "Axis_*")
    public Transform GetAxisParent(int index)
    {
        if (axisParents == null || index < 0 || index >= axisParents.Length) return null;
        return axisParents[index];
    }

    // Convierte un punto world sobre un eje en el valor numérico correspondiente
    public float ValueFromWorldPoint(int col, Vector3 worldPoint)
    {
        Transform axisParent = GetAxisParent(col);
        if (axisParent == null) return 0f;
        // local.y va a estar entre 0 y maxVisualHeight si el axisParent está configurado como en CreateAxes()
        Vector3 local = axisParent.InverseTransformPoint(worldPoint);
        float t = Mathf.Clamp01(local.y / maxVisualHeight);
        return Mathf.Lerp(_data.MinValues[col], _data.MaxValues[col], t);
    }

    //Modify axis height using the AxisHeightController class
    public void UpdateAxesHeight()
    {
        if (axisCylinders == null) return;

        // Scale factor relative to base height
        float baseRef = 5f;
        float scaleFactor = Mathf.Max(0.0001f, maxVisualHeight / baseRef);

        for (int i = 0; i < axisCylinders.Length; i++)
        {
            Transform axis = axisCylinders[i];
            if (axis == null) continue;

            // Update axis height (axis.localScale.y its half the axis height)
            axis.localScale = new Vector3(axisRadius * scaleFactor, maxVisualHeight / 2f, axisRadius * scaleFactor);
            axis.localPosition = new Vector3(0, maxVisualHeight / 2f, 0);

            // Actualizar collider del cilindro
            UpdateAxisCollider(axis, maxVisualHeight);


            Transform axisParent = axisParents[i];

            // Change the axis name if it exists
            Transform axisLabel = axisParent.Find("AxisLabel");
            if (axisLabel != null)
            {
                axisLabel.localPosition = new Vector3(0, maxVisualHeight + 0.2f * scaleFactor, 0);
                axisLabel.localScale = Vector3.one * scaleFactor /** 0.3f*/;
            }

            // Cahnge TickLabels (numbers) positions and size
            for (int tick = 0; tick <= subdivisions; tick++)
            {
                string tickName = $"TickLabel_{i}_{tick}";
                Transform tickLabel = axisParent.Find(tickName);
                if (tickLabel == null) continue;

                float t = tick / (float)subdivisions;
                float yPos = t * maxVisualHeight;
                Vector3 localPos = tickLabel.localPosition;
                localPos.y = yPos;
                localPos.x = - (axisRadius * scaleFactor) - labelOffset * scaleFactor; 
                tickLabel.localPosition = localPos;

                // Make the label change its size along with the axis
                tickLabel.localScale = Vector3.one * scaleFactor /** 0.25f*/;

                // Change the ticklabel value if necessary (helps precission if the height changes too much)
                TextMeshPro tmp = tickLabel.GetComponent<TextMeshPro>();
                if (tmp != null)
                {
                    
                    float value = _data.MinValues[i] + t * (_data.MaxValues[i] - _data.MinValues[i]);
                    tmp.text = value.ToString("0.0");
                }
            }
        }

        // Change lines thickness alongwith the axis height
        UpdateLineThickness(scaleFactor);
    }


    private void UpdateLineThickness(float scaleFactor)
    {
        LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
        float baseWidth = 0.02f; // With reference (baseRef) width 5
        foreach (var line in lines)
        {
            line.startWidth = baseWidth * scaleFactor;
            line.endWidth = baseWidth * scaleFactor;
        }
    }

    private void UpdateAxisCollider(Transform axis, float axisHeight)
    {
        CapsuleCollider col = axis.GetComponent<CapsuleCollider>();
        if (col == null) col = axis.gameObject.AddComponent<CapsuleCollider>();

        // El cilindro en Unity tiene altura = scale.y * 2
        float visualScaleY = axisHeight / 2f;
        axis.localScale = new Vector3(axis.localScale.x, visualScaleY, axis.localScale.z);

        // Configuración del collider
        col.direction = 1; // Y

        col.height = axisHeight;               // misma altura del cilindro
        col.radius = axis.localScale.x * 1.1f; // un poquito más grande para VR

        col.center = new Vector3(0f, 0f, 0f);  // SIEMPRE 0, porque el cilindro ya está bien posicionado
    }




}