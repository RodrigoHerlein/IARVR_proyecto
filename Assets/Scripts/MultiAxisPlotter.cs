using UnityEngine;
using System.Globalization;
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

            Inicialmente están deshabilitados (lr.enabled = false).

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

    private float[] minValues;
    private float[] maxValues;
    private int numColumns;

    private Transform[] axisCylinders;
    private Transform[] axisParents;

    private List<Dictionary<(int, int), LineRenderer>> rowConnections = new List<Dictionary<(int, int), LineRenderer>>();
    private List<float[]> lineData = new List<float[]>();

    void Start()
    {
        if (csvFile != null)
        {
            CalculateMinMax();
            CreateAxes();
            CreateAllConnections();
        }
        else
        {
            Debug.LogError("No CSV assigned!");
        }
    }

    void CalculateMinMax()
    {
        string[] lines = csvFile.text.Split('\n');
        string[] headers = lines[0].Split(',');
        numColumns = headers.Length;

        minValues = new float[numColumns];
        maxValues = new float[numColumns];

        for (int col = 0; col < numColumns; col++)
        {
            minValues[col] = float.MaxValue;
            maxValues[col] = float.MinValue;

            for (int row = 1; row < lines.Length; row++)
            {
                if (string.IsNullOrWhiteSpace(lines[row])) continue;
                string[] values = lines[row].Split(',');
                if (values.Length <= col) continue;

                if (float.TryParse(values[col], NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    if (val < minValues[col]) minValues[col] = val;
                    if (val > maxValues[col]) maxValues[col] = val;
                }
            }
        }
    }

    void CreateAxes()
    {
        string[] lines = csvFile.text.Split('\n');
        string[] headers = lines[0].Split(',');

        axisCylinders = new Transform[numColumns];
        axisParents = new Transform[numColumns];

        for (int col = 0; col < numColumns; col++)
        {
            GameObject axisGO = new GameObject("Axis_" + headers[col]);
            axisGO.transform.SetParent(transform);
            axisGO.transform.localPosition = new Vector3(col * axisSpacing, 0, 0);
            axisGO.AddComponent<AxisSelectable>();
            axisParents[col] = axisGO.transform;

            float axisHeight = maxVisualHeight;
            GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axis.transform.SetParent(axisGO.transform);
            axis.transform.localScale = new Vector3(axisRadius, axisHeight / 2f, axisRadius);
            axis.transform.localPosition = new Vector3(0, axisHeight / 2f, 0);

            // ✅ agregar script de selección
            //axis.AddComponent<SelectableObject>();


            axisCylinders[col] = axis.transform;

            /*GameObject axisLabel = new GameObject("AxisLabel");
            axisLabel.transform.SetParent(axisGO.transform);
            axisLabel.transform.localPosition = new Vector3(0, axisHeight + 0.2f, 0);
            TextMesh labelMesh = axisLabel.AddComponent<TextMesh>();
            labelMesh.text = headers[col];
            labelMesh.characterSize = 0.3f;
            labelMesh.anchor = TextAnchor.LowerCenter;
            labelMesh.alignment = TextAlignment.Center;*/
            GameObject axisLabel = new GameObject("AxisLabel");
            axisLabel.transform.SetParent(axisGO.transform);
            axisLabel.transform.localPosition = new Vector3(0, axisHeight + 0.2f, 0);
            TextMeshPro tmp = axisLabel.AddComponent<TextMeshPro>();
            axisLabel.AddComponent<FaceCamera>();
            tmp.text = headers[col];
            tmp.fontSize = 2;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            for (int i = 0; i <= subdivisions; i++)
            {
                float t = i / (float)subdivisions;
                float yPos = t * axisHeight;
                float value = minValues[col] + t * (maxValues[col] - minValues[col]);

                if (tickPrefab != null)
                {
                    GameObject tick = Instantiate(tickPrefab, axisGO.transform);
                    tick.transform.localPosition = new Vector3(axisRadius + 0.05f, yPos, 0);
                    tick.transform.localScale = new Vector3(tickSize, tickSize, tickSize);
                }

                GameObject lbl;
                if (labelPrefab != null)
                {
                    lbl = Instantiate(labelPrefab, axisGO.transform);
                }
                else
                {
                    lbl = new GameObject("Label_" + i);
                    lbl.transform.SetParent(axisGO.transform);
                    //lbl.AddComponent<TextMesh>();
                    
                }
                lbl.transform.localPosition = new Vector3(-axisRadius - labelOffset, yPos, 0);
                lbl.transform.localRotation = Quaternion.identity;

                /*TextMesh textMesh = lbl.GetComponent<TextMesh>();
                textMesh.text = value.ToString("0.0");
                textMesh.characterSize = 0.2f;
                textMesh.anchor = TextAnchor.MiddleRight;*/

                TextMeshPro textMesh = lbl.AddComponent<TextMeshPro>();
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
        string[] lines = csvFile.text.Split('\n');

        for (int row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            string[] values = lines[row].Split(',');

            float[] rowData = new float[numColumns];
            for (int col = 0; col < numColumns; col++)
            {
                if (float.TryParse(values[col], NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    rowData[col] = val;
                }
            }
            lineData.Add(rowData);

            var connDict = new Dictionary<(int, int), LineRenderer>();

            for (int i = 0; i < numColumns; i++)
            {
                for (int j = i + 1; j < numColumns; j++)
                {
                    GameObject segGO = new GameObject($"Row{row - 1}_Seg{i}_{j}");
                    segGO.transform.SetParent(transform);
                    LineRenderer lr = segGO.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.startWidth = 0.05f;
                    lr.endWidth = 0.05f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = Color.red;
                    lr.endColor = Color.red;
                    lr.enabled = false;

                    // ✅ Agregar script seleccionable
                    //segGO.AddComponent<SelectableObject>();
                    segGO.AddComponent<LineSelectable>();
                    

                    connDict[(i, j)] = lr;
                }
            }

            rowConnections.Add(connDict);
        }
    }

    void Update()
    {
        UpdateConnections();
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

                    // ✅ actualizamos collider de la línea
                    UpdateLineCollider(go, p1, p2, 0.05f);
                }
                else
                {
                    lr.enabled = false;
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
        float t = (val - minValues[col]) / (maxValues[col] - minValues[col]);
        float localY = t * maxVisualHeight;
        Vector3 localPoint = new Vector3(0f, localY, 0f);
        return axisCylinders[col].parent.TransformPoint(localPoint);
    }
}