// Main script - many parts adapted from Pxc: // https://github.com/keijiro/Pcx

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System;

public class PointCloudContainer : MonoBehaviour {

    PointCloudData _sourceData = null;
    Color _pointTint = new Color(0.5f, 0.5f, 0.5f, 1);
    Material _pointMaterial;
    ComputeBuffer pointBuffer;
    int currFrame = 0;
    int fileNum = 0;
    bool done = false;
    float timer;
    private const string BASE_URL = "http://10.0.0.94:38080/sampleFrames/";

    #region Internal data structures

    enum DataProperty {
        Invalid,
        R8, G8, B8, A8,
        R16, G16, B16, A16,
        SingleX, SingleY, SingleZ,
        DoubleX, DoubleY, DoubleZ,
        Data8, Data16, Data32, Data64
    }

    static int GetPropertySize(DataProperty p)
    {
        switch (p)
        {
            case DataProperty.R8: return 1;
            case DataProperty.G8: return 1;
            case DataProperty.B8: return 1;
            case DataProperty.A8: return 1;
            case DataProperty.R16: return 2;
            case DataProperty.G16: return 2;
            case DataProperty.B16: return 2;
            case DataProperty.A16: return 2;
            case DataProperty.SingleX: return 4;
            case DataProperty.SingleY: return 4;
            case DataProperty.SingleZ: return 4;
            case DataProperty.DoubleX: return 8;
            case DataProperty.DoubleY: return 8;
            case DataProperty.DoubleZ: return 8;
            case DataProperty.Data8: return 1;
            case DataProperty.Data16: return 2;
            case DataProperty.Data32: return 4;
            case DataProperty.Data64: return 8;
        }
        return 0;
    }

    class DataHeader
    {
        public List<DataProperty> properties = new List<DataProperty>();
        public int vertexCount = -1;
    }

    class DataBody
    {
        public List<Vector3> vertices;
        public List<Color32> colors;

        public DataBody(int vertexCount)
        {
            vertices = new List<Vector3>(vertexCount);
            colors = new List<Color32>(vertexCount);
        }

        public void AddPoint(
            float x, float y, float z,
            byte r, byte g, byte b, byte a
        )
        {
            vertices.Add(new Vector3(x, y, z));
            colors.Add(new Color32(r, g, b, a));
        }
    }

    #endregion

    void Start() {
        // target framerate needs to be double the actual target, because the
        // coroutine takes at least 2 frames to execute... fix this later
        Application.targetFrameRate = 60;
        _pointMaterial = new Material(Shader.Find("Point Cloud/Point-Maaz"));
        _pointMaterial.hideFlags = HideFlags.DontSave;
        _pointMaterial.EnableKeyword("_COMPUTE_BUFFER");

        // Empty for the first frame, needs to be defined otherwise the shader is unhappy
        pointBuffer = new ComputeBuffer(1, 4);
        timer = Time.time;
    }

    void OnRenderObject() {
        _pointMaterial.SetPass(0);
        _pointMaterial.SetColor("_Tint", _pointTint);
        _pointMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
        _pointMaterial.SetBuffer("_PointBuffer", pointBuffer);
        Graphics.DrawProceduralNow(MeshTopology.Points, pointBuffer.count, 1);
    }

    void OnDestroy(){
        if (_pointMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_pointMaterial);
            }
            else
            {
                DestroyImmediate(_pointMaterial);
            }
        }
    }

    void Update() {
        // Grab the next PLY file and render it using the shader
        if (!done) {
            StartCoroutine(DownloadAndRenderPLYFrame($"frame_{currFrame++}.ply"));
        }
    }

    IEnumerator DownloadAndRenderPLYFrame(string filename) {
        string uri = BASE_URL + filename;
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            yield return webRequest.SendWebRequest();

            string savePath = $"Assets/Resources/frame_{fileNum}.ply";
            fileNum = (fileNum + 1) % 30;
            File.WriteAllBytes(savePath, webRequest.downloadHandler.data);

            _sourceData = ParseAsPointCloudData(savePath);
            pointBuffer = _sourceData.computeBuffer;

            _pointMaterial.SetPass(0);
            _pointMaterial.SetColor("_Tint", _pointTint);
            _pointMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
            _pointMaterial.SetBuffer("_PointBuffer", pointBuffer);
        }
    }

    PointCloudData ParseAsPointCloudData(string path) {
        try
        {
            var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = ReadDataHeader(new StreamReader(stream));
            var body = ReadDataBody(header, new BinaryReader(stream));
            var data = ScriptableObject.CreateInstance<PointCloudData>();
            data.Initialize(body.vertices, body.colors);
            data.name = Path.GetFileNameWithoutExtension(path);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed importing " + path + ". " + e.Message);
            done = true;
            float tt = Time.time;
            Debug.Log($"Time taken: {tt - timer}s");
            return null;
        }
    }

    DataHeader ReadDataHeader(StreamReader reader) {
        var data = new DataHeader();
        var readCount = 0;

        // Magic number line ("ply")
        var line = reader.ReadLine();
        readCount += line.Length + 1;
        if (line != "ply")
            throw new ArgumentException("Magic number ('ply') mismatch.");

        // Data format: check if it's binary/little endian.
        line = reader.ReadLine();
        readCount += line.Length + 1;
        if (line != "format binary_little_endian 1.0")
            throw new ArgumentException(
                "Invalid data format ('" + line + "'). " +
                "Should be binary/little endian.");

        // Read header contents.
        for (var skip = false;;)
        {
            // Read a line and split it with white space.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line == "end_header") break;
            var col = line.Split();

            // Element declaration (unskippable)
            if (col[0] == "element")
            {
                if (col[1] == "vertex")
                {
                    data.vertexCount = Convert.ToInt32(col[2]);
                    skip = false;
                }
                else
                {
                    // Don't read elements other than vertices.
                    skip = true;
                }
            }

            if (skip) continue;

            // Property declaration line
            if (col[0] == "property")
            {
                var prop = DataProperty.Invalid;

                // Parse the property name entry.
                switch (col[2])
                {
                    case "red"  : prop = DataProperty.R8; break;
                    case "green": prop = DataProperty.G8; break;
                    case "blue" : prop = DataProperty.B8; break;
                    case "alpha": prop = DataProperty.A8; break;
                    case "x"    : prop = DataProperty.SingleX; break;
                    case "y"    : prop = DataProperty.SingleY; break;
                    case "z"    : prop = DataProperty.SingleZ; break;
                }

                // Check the property type.
                if (col[1] == "char" || col[1] == "uchar" ||
                    col[1] == "int8" || col[1] == "uint8")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data8;
                    else if (GetPropertySize(prop) != 1)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "short" || col[1] == "ushort" ||
                            col[1] == "int16" || col[1] == "uint16")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data16; break;
                        case DataProperty.R8: prop = DataProperty.R16; break;
                        case DataProperty.G8: prop = DataProperty.G16; break;
                        case DataProperty.B8: prop = DataProperty.B16; break;
                        case DataProperty.A8: prop = DataProperty.A16; break;
                    }
                    if (GetPropertySize(prop) != 2)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int"   || col[1] == "uint"   || col[1] == "float" ||
                            col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data32;
                    else if (GetPropertySize(prop) != 4)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int64"  || col[1] == "uint64" ||
                            col[1] == "double" || col[1] == "float64")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data64; break;
                        case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                        case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                        case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                    }
                    if (GetPropertySize(prop) != 8)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else
                {
                    throw new ArgumentException("Unsupported property type ('" + line + "').");
                }

                data.properties.Add(prop);
            }
        }

        // Rewind the stream back to the exact position of the reader.
        reader.BaseStream.Position = readCount;

        return data;
    }

    DataBody ReadDataBody(DataHeader header, BinaryReader reader) {
        var data = new DataBody(header.vertexCount);

        float x = 0, y = 0, z = 0;
        Byte r = 255, g = 255, b = 255, a = 255;

        for (var i = 0; i < header.vertexCount; i++)
        {
            foreach (var prop in header.properties)
            {
                switch (prop)
                {
                    case DataProperty.R8: r = reader.ReadByte(); break;
                    case DataProperty.G8: g = reader.ReadByte(); break;
                    case DataProperty.B8: b = reader.ReadByte(); break;
                    case DataProperty.A8: a = reader.ReadByte(); break;

                    case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                    case DataProperty.SingleX: x = reader.ReadSingle(); break;
                    case DataProperty.SingleY: y = reader.ReadSingle(); break;
                    case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                    case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                    case DataProperty.Data8: reader.ReadByte(); break;
                    case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                    case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                    case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                }
            }

            data.AddPoint(x, y, z, r, g, b, a);
        }

        return data;
    }
}
