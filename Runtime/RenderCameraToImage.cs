using System.IO;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RenderCameraToImage : MonoBehaviour
{
    public int outputWidth = 1024;
    public int outputHeight = 1024;
    public string outputFolder = "Assets/RenderedSprites";

    public LayerMask commonRenderMask = -1;

    [Header("Albedo")]
    public bool renderAlbedo = true;

    [Header("Normal Map")]
    public bool renderNormalMap = true;

    [Header("Shadow")]
    public bool renderShadowMap = true;

    public float nearClipPlaneOverride = 0.01f;

    public LayerMask shadowReceiverMask = -1;

    public void RenderToImage()
    {
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        RenderTexture rt = new RenderTexture(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32);

        try
        {
            var cam = GetComponent<Camera>();
            {
                // Store original state per camera
                Quaternion originalRotation = cam.transform.rotation;
                Color originalBackground = cam.backgroundColor;
                CameraClearFlags originalFlags = cam.clearFlags;
                RenderTexture originalRT = cam.targetTexture;
                int originalCullingMask = cam.cullingMask;
                var originalNearClipPlane = cam.nearClipPlane;

                // Setup for transparency
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);

                cam.cullingMask = commonRenderMask;

                if (renderAlbedo)
                    RenderAlbedo(cam, rt);

                if (renderNormalMap)
                    RenderNormalMap(cam, rt);

                if (renderNormalMap)
                {
                    cam.cullingMask = shadowReceiverMask;
                    cam.nearClipPlane = nearClipPlaneOverride;
                    RenderShadowMap(cam, rt);
                }

                // Restore camera state
                cam.targetTexture = originalRT;
                cam.backgroundColor = originalBackground;
                cam.clearFlags = originalFlags;
                cam.cullingMask = originalCullingMask;
                cam.nearClipPlane = originalNearClipPlane;

                AssetDatabase.Refresh();
            }

            //AssetDatabase.Refresh();
            Debug.Log($"Batch rendering complete! Files saved to {outputFolder}");
        }
        finally
        {
            RenderTexture.active = null;
            DestroyImmediate(rt);
        }
    }

    private void RenderShadowMap(Camera cam, RenderTexture rt)
    {
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var areEnabled = new bool[allLights.Length];
        for (int i = 0; i < allLights.Length; i++)
        {
            areEnabled[i] = allLights[i].enabled;
            allLights[i].enabled = false;
        }
        for (int i = 0; i < allLights.Length; i++)
        {
            //if (!areEnabled[i])
            //    continue;
            allLights[i].enabled = true;
            RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/ShadowReceiver"), rt, "_" + allLights[i].name);
        }
        for (int i = 0; i < allLights.Length; i++)
        {
            allLights[i].enabled = areEnabled[i];
        }

    }

    private void RenderAlbedo(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/Albedo"), rt);
    }

    private void RenderNormalMap(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/Normal"), rt);
    }

    private void RenderWithShader(Camera cam, Shader shader, RenderTexture rt, string suffix = null)
    {
        string shaderName = shader != null ? shader.name.Replace("/", "_") : "Default";

        cam.RenderWithShader(shader, "RenderType");

        SaveRT(rt, $"{outputFolder}/{cam.name}_{shaderName}{suffix ?? ""}.png");
    }

    private void SaveRT(RenderTexture rt, string fileName)
    {
        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
        screenShot.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
        screenShot.Apply();

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(fileName, bytes);

        DestroyImmediate(screenShot);
    }
}

