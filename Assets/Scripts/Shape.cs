using UnityEngine;

[ExecuteInEditMode]
public class Shape : MonoBehaviour
{
    public ShapePrimitiveType primitiveType;
    public Color color;
    public Vector4 parameter;
    public ShapeBlendOperation blendOperation;
    public float smoothPower;

    private void OnEnable()
    {
        RayMarchBrain.Instance.AddShape(this);
    }

    private void OnDisable()
    {
        if (RayMarchBrain.Exist)
        {
            RayMarchBrain.Instance.RemoveShape(this);
        }
    }

    public ShapeData GetShapeData()
    {
        var shapeData = new ShapeData
        {
            primitiveType = (uint)this.primitiveType,
            position = this.transform.position,
            color = this.color,
            parameter = this.parameter,
            blendOperation = (uint)this.blendOperation,
            smoothPower = this.smoothPower
        };

        return shapeData;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        switch (this.primitiveType)
        {
            case ShapePrimitiveType.Sphere:
                Gizmos.DrawWireSphere(this.transform.position, this.parameter.w);
                break;
            case ShapePrimitiveType.Box:
                Gizmos.DrawWireCube(this.transform.position, new Vector3(this.parameter.x, this.parameter.y, this.parameter.z) * 2);
                break;
            case ShapePrimitiveType.Plane:
                break;
            default:
                break;
        }
    }
}