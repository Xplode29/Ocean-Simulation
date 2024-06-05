using System;
using System.Collections;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using UnityEditor;
using static UnityEngine.UI.Image;
using System.Data;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Water : MonoBehaviour
{
    private Wave[] waves = new Wave[64];
    private ComputeBuffer waveBuffer;

    private Material waterMaterial;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] displacedVertices;
    private Vector3[] normals;
    private Vector3[] displacedNormals;

    // Procedural Settings
    public Vector3 color = new Vector4(1.0f, 1.0f, 1.0f);
    public GameObject sun;
    public int waveCount = 4;

    public float medianWavelength = 1.0f;
    public float wavelengthRange = 1.0f;

    public float medianDirection = 0.0f;
    public float directionalRange = 30.0f;

    public float medianAmplitude = 1.0f;
    public float amplitudeRange = 1.0f;

    public float medianSpeed = 1.0f;
    public float speedRange = 0.1f;
    public Shader waterShader;

    public int planeLength = 10;
    public int quadRes = 10;

    public struct Wave {
        public Vector2 direction;
        public Vector2 origin;
        public float frequency;
        public float amplitude;
        public float phase;

        public Wave(float wavelength, float amplitude, float speed, float direction, Vector2 origin)
        {
            this.frequency = 2.0f / wavelength;
            this.amplitude = amplitude;
            this.phase = speed * Mathf.Sqrt(9.8f * 2.0f * Mathf.PI / wavelength);
            this.origin = origin;

            this.direction = new Vector2(Mathf.Cos(Mathf.Deg2Rad * direction), Mathf.Sin(Mathf.Deg2Rad * direction));
            this.direction.Normalize();
        }

        public Vector2 GetDirection(Vector3 v)
        {
            return this.direction;
        }

        public float GetWaveCoord(Vector3 v, Vector2 d)
        {
            return v.x * d.x + v.z * d.y;
        }

        public float GetTime()
        {
            return Time.time * this.phase;
        }

        public float Sine(Vector3 v)
        {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            return Mathf.Sin(this.frequency * xz + GetTime()) * this.amplitude;
        }

        public Vector3 SineNormal(Vector3 v)
        {
            Vector2 d = GetDirection(v);
            float xz = GetWaveCoord(v, d);

            float dx = this.frequency * this.amplitude * d.x * Mathf.Cos(xz * this.frequency + GetTime());
            float dy = this.frequency * this.amplitude * d.y * Mathf.Cos(xz * this.frequency + GetTime());

            return new Vector3(dx, dy, 0.0f);
        }
    }

    public void GenerateNewWaves()
    {
        float wavelengthMin = medianWavelength - wavelengthRange;
        float wavelengthMax = medianWavelength + wavelengthRange;

        float directionMin = medianDirection - directionalRange;
        float directionMax = medianDirection + directionalRange;

        float amplitudeMin = medianAmplitude - amplitudeRange;
        float amplitudeMax = medianAmplitude + amplitudeRange;

        float speedMin = Mathf.Max(0.01f, medianSpeed - speedRange);
        float speedMax = medianSpeed + speedRange;

        float halfPlaneWidth = planeLength * 0.5f;
        Vector3 minPoint = transform.TransformPoint(new Vector3(-halfPlaneWidth, 0.0f, -halfPlaneWidth));
        Vector3 maxPoint = transform.TransformPoint(new Vector3(halfPlaneWidth, 0.0f, halfPlaneWidth));

        for (int wi = 0; wi < waveCount; ++wi)
        {
            float wavelength = UnityEngine.Random.Range(wavelengthMin, wavelengthMax);
            float direction = UnityEngine.Random.Range(directionMin, directionMax);
            float amplitude = UnityEngine.Random.Range(amplitudeMin, amplitudeMax);
            float speed = UnityEngine.Random.Range(speedMin, speedMax);
            Vector2 origin = new Vector2(UnityEngine.Random.Range(minPoint.x * 2, maxPoint.x * 2), UnityEngine.Random.Range(minPoint.x * 2, maxPoint.x * 2));

            waves[wi] = new Wave(wavelength, amplitude, speed, direction, origin);
        }

        waveBuffer.SetData(waves);
        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }

    private void CreateWaterPlane()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Water";
        mesh.indexFormat = IndexFormat.UInt32;

        float halfLength = planeLength * 0.5f;
        int sideVertCount = planeLength * quadRes;

        vertices = new Vector3[(sideVertCount + 1) * (sideVertCount + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for (int i = 0, x = 0; x <= sideVertCount; ++x)
        {
            for (int z = 0; z <= sideVertCount; ++z, ++i)
            {
                vertices[i] = new Vector3(((float)x / sideVertCount * planeLength) - halfLength, 0, ((float)z / sideVertCount * planeLength) - halfLength);
                uv[i] = new Vector2((float)x / sideVertCount, (float)z / sideVertCount);
                tangents[i] = tangent;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.tangents = tangents;

        int[] triangles = new int[sideVertCount * sideVertCount * 6];

        for (int ti = 0, vi = 0, x = 0; x < sideVertCount; ++vi, ++x)
        {
            for (int z = 0; z < sideVertCount; ti += 6, ++vi, ++z)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + sideVertCount + 2;
                triangles[ti + 3] = vi;
                triangles[ti + 4] = vi + sideVertCount + 2;
                triangles[ti + 5] = vi + sideVertCount + 1;
            }
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        normals = mesh.normals;

        displacedVertices = new Vector3[vertices.Length];
        Array.Copy(vertices, 0, displacedVertices, 0, vertices.Length);
        displacedNormals = new Vector3[normals.Length];
        Array.Copy(normals, 0, displacedNormals, 0, normals.Length);
    }

    void CreateMaterial()
    {
        if (waterShader == null) return;
        if (waterMaterial != null) return;

        waterMaterial = new Material(waterShader);
        waterMaterial.SetBuffer("_Waves", waveBuffer);

        MeshRenderer renderer = GetComponent<MeshRenderer>();

        renderer.material = waterMaterial;
    }

    void CreateWaveBuffer()
    {
        if (waveBuffer != null) return;

        waveBuffer = new ComputeBuffer(64, SizeOf(typeof(Wave)));

        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }

    void OnEnable()
    {
        CreateWaterPlane();
        CreateMaterial();
        CreateWaveBuffer();
        GenerateNewWaves();
    }

    void OnDisable()
    {
        if (waterMaterial != null)
        {
            Destroy(waterMaterial);
            waterMaterial = null;
        }

        if (mesh != null)
        {
            Destroy(mesh);
            mesh = null;
            vertices = null;
            normals = null;
            displacedVertices = null;
            displacedNormals = null;
        }

        if (waveBuffer != null)
        {
            waveBuffer.Release();
            waveBuffer = null;
        }
    }

    void Update()
    {
        waterMaterial.SetInt("_WaveCount", waveCount);
        waterMaterial.SetVector("_COLOR", color);
        waterMaterial.SetVector("_SunPosition", sun.gameObject.transform.position);

        waveBuffer.SetData(waves);
        waterMaterial.SetBuffer("_Waves", waveBuffer);
    }
}
