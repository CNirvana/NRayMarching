using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Shape))]
public class ShapeEditor : Editor
{
    private SerializedProperty _primitiveTypeProperty;
    private SerializedProperty _colorProperty;
    private SerializedProperty _parameterProperty;
    private SerializedProperty _blendOperationProperty;
    private SerializedProperty _smoothPowerProperty;

    private void OnEnable()
    {
        _primitiveTypeProperty = this.serializedObject.FindProperty("primitiveType");
        _colorProperty = this.serializedObject.FindProperty("color");
        _parameterProperty = this.serializedObject.FindProperty("parameter");
        _blendOperationProperty = this.serializedObject.FindProperty("blendOperation");
        _smoothPowerProperty = this.serializedObject.FindProperty("smoothPower");
    }

    public override void OnInspectorGUI()
    {
        this.serializedObject.Update();

        EditorGUILayout.PropertyField(_primitiveTypeProperty);
        EditorGUILayout.PropertyField(_colorProperty);

        var parameter = _parameterProperty.vector4Value;
        switch ((ShapePrimitiveType)_primitiveTypeProperty.enumValueIndex)
        {
            case ShapePrimitiveType.Sphere:
                parameter.w = EditorGUILayout.FloatField("Radius", parameter.w);
                break;
            case ShapePrimitiveType.Box:
                var size = new Vector3(parameter.x, parameter.y, parameter.z);
                size = EditorGUILayout.Vector3Field("Size", size);
                parameter.Set(size.x, size.y, size.z, parameter.w);
                break;
            case ShapePrimitiveType.Plane:
                break;
            default:
                break;
        }

        _parameterProperty.vector4Value = parameter;
        EditorGUILayout.PropertyField(_blendOperationProperty);
        EditorGUILayout.PropertyField(_smoothPowerProperty);
        _smoothPowerProperty.floatValue = Mathf.Max(_smoothPowerProperty.floatValue, 0.1f);

        this.serializedObject.ApplyModifiedProperties();
    }
}