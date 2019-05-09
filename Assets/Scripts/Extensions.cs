using System.Collections.Generic;
using UnityEngine;

public static class Extensions
{
    public static Matrix4x4 GetCameraFrustum(this Camera camera)
    {
        var frustum = Matrix4x4.identity;
        float tanFov = Mathf.Tan((camera.fieldOfView * 0.5f) * Mathf.Deg2Rad);

        var up = Vector3.up * tanFov;
        var right = Vector3.right * tanFov * camera.aspect;

        var topLeft = (-Vector3.forward - right + up);
        var topRight = (-Vector3.forward + right + up);
        var bottomLeft = (-Vector3.forward - right - up);
        var bottomRight = (-Vector3.forward + right - up);

        frustum.SetRow(0, topLeft);
        frustum.SetRow(1, topRight);
        frustum.SetRow(2, bottomRight);
        frustum.SetRow(3, bottomLeft);

        return frustum;
    }

    public static void RemoveUnordered<T>(this List<T> list, T item)
    {
        var index = list.IndexOf(item);
        if (index != -1)
        {
            var temp = list[list.Count - 1];
            list[list.Count - 1] = list[index];
            list[index] = temp;
            list.RemoveAt(list.Count - 1);
        }
    }
}

public static class GraphicsExtension
{
    public static void CustomBlit(Texture source, RenderTexture destination, Material material, int pass = 0)
    {
        RenderTexture.active = destination;
        GL.PushMatrix();
        {
            GL.LoadOrtho();

            material.SetTexture("_MainTex", source);
            material.SetPass(pass);

            GL.Begin(GL.QUADS);
            {
                // bottom left
                GL.MultiTexCoord2(0, 0.0f, 0.0f);
                GL.Vertex3(0.0f, 0.0f, 3.0f);

                // bottom right
                GL.MultiTexCoord2(0, 1.0f, 0.0f);
                GL.Vertex3(1.0f, 0.0f, 2.0f);

                // top right
                GL.MultiTexCoord2(0, 1.0f, 1.0f);
                GL.Vertex3(1.0f, 1.0f, 1.0f);

                // top left
                GL.MultiTexCoord2(0, 0.0f, 1.0f);
                GL.Vertex3(0.0f, 1.0f, 0.0f);
            }
            GL.End();
        }
        GL.PopMatrix();
    }
}