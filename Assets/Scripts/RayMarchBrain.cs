using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class RayMarchBrain : MonoBehaviour
{
    private static RayMarchBrain _instance = null;
    public static RayMarchBrain Instance
    {
        get
        {
            if(_instance == null)
            {
                _instance = FindObjectOfType<RayMarchBrain>();
                if(_instance == null)
                {
                    var go = new GameObject("RayMarch Brain");
                    _instance = go.AddComponent<RayMarchBrain>();
                }
            }

            return _instance;
        }
    }

    public static bool Exist { get { return _instance != null; } }

    [SerializeField]
    private Shader _shader;
    public int maxShapeCount = 100;
    [Range(1, 256)]
    public int iteration = 64;
    [Range(0.001f, 0.1f)]
    public float accuracy = 0.001f;

    [Header("Light Settings")]
    public Light directionalLight;

    [Header("Shadow Settings")]
    [Range(0.0f, 1.0f)]
    public float shadowIntensity = 0.0f;
    [Min(0.01f)]
    public float shadowNearPlane = 0.01f;
    [Min(1.0f)]
    public float shadowFarPlane = 1.0f;
    [Range(1, 128)]
    public float shadowPenumbra = 1;

    [Header("Ambient Occlusion Settings")]
    [Range(0.01f, 10.0f)]
    public float aoStep = 0.01f;
    [Range(1, 10)]
    public int aoIteration = 1;
    [Range(0.0f, 1.0f)]
    public float aoIntensity = 0.0f;

    [Header("Reflection")]
    [Range(0, 4)]
    public int reflectionIteration = 0;
    [Range(0.0f, 1.0f)]
    public float reflectionIntensity = 0.0f;

    [Header("Material")]
    [Range(0.0f, 1.0f)]
    public float toonAmount = 0.1f;
    public float glossiness = 32;
    [Range(0.0f, 1.0f)]
    public float rimAmount = 0.7f;
    [Range(0.0f, 1.0f)]
    public float rimThreshold = 0.1f;

    public Material Material
    {
        get
        {
            if (_shader != null && _material == null)
            {
                _material = new Material(_shader);
                _material.hideFlags = HideFlags.DontSave;
            }

            return _material;
        }
    }
    public Camera Camera
    {
        get
        {
            if (_camera == null)
            {
                _camera = this.GetComponent<Camera>();
            }

            return _camera;
        }
    }
    public ComputeBuffer ShapeBuffer
    {
        get
        {
            if (_shapeBuffer == null || _shapeBuffer.count != this.maxShapeCount)
            {
                _shapeBuffer = new ComputeBuffer(this.maxShapeCount, Marshal.SizeOf(typeof(ShapeData)));
            }

            return _shapeBuffer;
        }
    }

    private Material _material;
    private Camera _camera;
    private Vector3[] _corners = new Vector3[4];
    private List<Shape> _shapes = new List<Shape>();
    private ShapeData[] _shapeDatas = null;
    private int _shapeCount;
    private ComputeBuffer _shapeBuffer;

    private void Awake()
    {
        this.Camera.depthTextureMode = DepthTextureMode.Depth;
    }

    private void OnDestroy()
    {
        if (_shapeBuffer != null)
        {
            _shapeBuffer.Release();
        }
    }

    public void AddShape(Shape shape)
    {
        if(!_shapes.Contains(shape))
        {
            _shapes.Add(shape);
        }
    }

    public void RemoveShape(Shape shape)
    {
        if (_shapes.Contains(shape))
        {
            _shapes.RemoveUnordered(shape);
        }
    }

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (this.Material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        this.PopulateShapeDatas();
        this.ShapeBuffer.SetData(_shapeDatas);

        this.Material.SetBuffer("_ShapeDatas", this.ShapeBuffer);
        this.Material.SetInt("_ShapeCount", _shapeCount);

        this.Material.SetMatrix("_CameraFrustum", this.GetFrustumMatrix());
        this.Material.SetInt("_Iteration", this.iteration);
        this.Material.SetFloat("_Accuracy", this.accuracy);

        this.Material.SetVector("_LightDir", this.directionalLight ? this.directionalLight.transform.forward : Vector3.down);
        this.Material.SetColor("_LightColor", this.directionalLight ? this.directionalLight.color : Color.white);
        this.Material.SetFloat("_LightIntensity", this.directionalLight ? this.directionalLight.intensity : 1.0f);

        this.Material.SetFloat("_ShadowIntensity", this.shadowIntensity);
        this.Material.SetFloat("_ShadowNearPlane", this.shadowNearPlane);
        this.Material.SetFloat("_ShadowFarPlane", this.shadowFarPlane);
        this.Material.SetFloat("_ShadowPenumbra", this.shadowPenumbra);

        this.Material.SetFloat("_AOStep", this.aoStep);
        this.Material.SetFloat("_AOIntensity", this.aoIntensity);
        this.Material.SetInt("_AOIteration", this.aoIteration);

        this.Material.SetInt("_ReflectionIteration", this.reflectionIteration);
        this.Material.SetFloat("_ReflectionIntensity", this.reflectionIntensity);

        this.Material.SetFloat("_ToonAmount", this.toonAmount);
        this.Material.SetFloat("_Glossiness", this.glossiness);
        this.Material.SetFloat("_RimAmount", this.rimAmount);
        this.Material.SetFloat("_RimThreshold", this.rimThreshold);

        Graphics.Blit(source, destination, this.Material);
    }

    private void PopulateShapeDatas()
    {
        if (_shapeDatas == null || _shapeDatas.Length != this.maxShapeCount)
        {
            _shapeDatas = new ShapeData[this.maxShapeCount];
        }

        _shapes.Sort((shape1, shape2) => shape1.smoothPower.CompareTo(shape2.smoothPower));

        _shapeCount = _shapes.Count;
        for (int i = 0; i < _shapes.Count; i++)
        {
            _shapeDatas[i] = _shapes[i].GetShapeData();
        }
    }

    private Matrix4x4 GetFrustumMatrix()
    {
        var matrix = Matrix4x4.identity;

        this.Camera.CalculateFrustumCorners(
            new Rect(0, 0, 1, 1),
            this.Camera.farClipPlane,
            this.Camera.stereoActiveEye,
            _corners
        );
        matrix.SetRow(0, this.Camera.transform.TransformVector(_corners[0]));
        matrix.SetRow(1, this.Camera.transform.TransformVector(_corners[3]));
        matrix.SetRow(2, this.Camera.transform.TransformVector(_corners[1]));
        matrix.SetRow(3, this.Camera.transform.TransformVector(_corners[2]));

        return matrix;
    }
}