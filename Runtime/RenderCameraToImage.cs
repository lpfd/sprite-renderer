using System;
using System.IO;
using System.Linq;
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

    [Header("Shaded")]
    public bool renderShaded = true;

    [Header("Alpha")]
    public bool renderAlpha = true;

    [Header("Normal Map")]
    public bool renderNormalMap = true;

    [Header("Ambient Occlusion")]
    public bool renderAO = true;

    [Header("Depth")]
    public bool renderDepth = true;

    [Header("Shadow")]
    public bool renderShadowMap = true;

    public float nearClipPlaneOverride = 0.01f;

    public LayerMask shadowReceiverMask = -1;

    public bool invertShadowColor = false;

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
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);


                cam.cullingMask = commonRenderMask;

                if (renderDepth)
                    RenderDepth(cam);

                if (renderAlbedo)
                    RenderAlbedo(cam, rt);

                if (renderAO)
                    RenderAO(cam, rt);

                if (renderAlpha)
                    RenderAlpha(cam, rt);

                if (renderNormalMap)
                    RenderNormalMap(cam, rt);

                if (renderShaded)
                {
                    RenderPerLigthSource(cam, rt, light => {
                        RenderWithShader(cam, null, "Shaded_" + light.name, rt);
                    });
                }

                if (renderShadowMap)
                {
                    cam.cullingMask = shadowReceiverMask;
                    cam.nearClipPlane = nearClipPlaneOverride;
                    RenderPerLigthSource(cam, rt, light => {
                        if (invertShadowColor)
                            RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/ShadowReceiverInv"), "Shadow_" + light.name, rt);
                        else
                            RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/ShadowReceiver"), "Shadow_" + light.name, rt);
                        //RenderShadowProjection(cam, allLights[i], Shader.Find("LeapForward/SpriteRenderer/ShadowMap"), "_" + allLights[i].name);
                    });
                    
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

    private void RenderDepth(Camera cam)
    {
        // 1. Create the Depth RenderTexture
        RenderTexture shadowMap = new RenderTexture(outputWidth, outputHeight, 24, RenderTextureFormat.Depth);
        shadowMap.filterMode = FilterMode.Bilinear;
        shadowMap.wrapMode = TextureWrapMode.Clamp;

        // 4. Render specifically for depth
        cam.targetTexture = shadowMap;
        // We use a replacement shader if we want pure depth, 
        // but for a simple shadow map, the depth buffer is enough.
        cam.Render();

        SaveRT(shadowMap, $"{outputFolder}/{cam.name}_Depth.exr");

        cam.targetTexture = null;

        // 5. Cleanup
        DestroyImmediate(shadowMap);
    }

    private void RenderPerLigthSource(Camera cam, RenderTexture rt, Action<Light> renderer)
    {
        Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var areGameObjectsEnabled = new bool[allLights.Length];
        var areEnabled = new bool[allLights.Length];
        for (int i = 0; i < allLights.Length; i++)
        {
            areEnabled[i] = allLights[i].enabled;
            areGameObjectsEnabled[i] = allLights[i].gameObject.activeSelf;
            allLights[i].enabled = false;
        }
        for (int i = 0; i < allLights.Length; i++)
        {
            //if (!areEnabled[i])
            //    continue;
            allLights[i].enabled = true;
            allLights[i].gameObject.SetActive(true);

            renderer(allLights[i]);

            allLights[i].enabled = false;
            allLights[i].gameObject.SetActive(areGameObjectsEnabled[i]);
        }
        for (int i = 0; i < allLights.Length; i++)
        {
            allLights[i].enabled = areEnabled[i];
        }
    }

    public void RenderShadowProjection(Camera mainCam, Light dirLight, Shader shadowProjectorShader, string suffix)
    {
        if (dirLight.type != LightType.Directional)
        {
            Debug.LogError("Shadow source must be a Directional Light.");
            return;
        }

        int resolution = 2048;
        float shadowDistance = 100f; // Range to cover scene

        // --- 1. SETUP SHADOW CAMERA & RENDER DEPTH MAP ---
        GameObject tempCamGO = new GameObject("ShadowCamera");
        Camera shadowCam = tempCamGO.AddComponent<Camera>();
        shadowCam.cullingMask = commonRenderMask;
        shadowCam.transform.rotation = dirLight.transform.rotation;
        // Position it back from the scene center to see objects
        shadowCam.transform.position = dirLight.transform.position;//.tra -dirLight.transform.forward * (shadowDistance * 0.5f);

        shadowCam.orthographic = true;
        shadowCam.orthographicSize = shadowDistance * 0.5f;
        shadowCam.nearClipPlane = 0.1f;
        shadowCam.farClipPlane = shadowDistance;
        shadowCam.enabled = false;

        RenderTexture shadowMap = RenderTexture.GetTemporary(resolution, resolution, 24, RenderTextureFormat.Depth);
        shadowCam.targetTexture = shadowMap;
        shadowCam.Render();

        // --- 2. CALCULATE WORLD-TO-SHADOW MATRIX ---
        // Matrix to convert Clip Space (-1 to 1) to Texture Space (0 to 1)
        Matrix4x4 m_clipToTex = Matrix4x4.identity;
        m_clipToTex.SetRow(0, new Vector4(0.5f, 0, 0, 0.5f));
        m_clipToTex.SetRow(1, new Vector4(0, 0.5f, 0, 0.5f));
        m_clipToTex.SetRow(2, new Vector4(0, 0, 0.5f, 0.5f));
        m_clipToTex.SetRow(3, new Vector4(0, 0, 0, 1.0f));

        // Account for platform-specific Projection Matrix differences (DirectX vs OpenGL)
        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(shadowCam.projectionMatrix, false);
        Matrix4x4 worldToShadowMatrix = m_clipToTex * projectionMatrix * shadowCam.worldToCameraMatrix;

        // --- 3. RENDER SCENE WITH PROJECTOR SHADER ---
        RenderTexture outputRT = RenderTexture.GetTemporary(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32);

        // Pass globals
        Shader.SetGlobalTexture("_ShadowMap", shadowMap);
        Shader.SetGlobalMatrix("_WorldToShadowMatrix", worldToShadowMatrix);
        Shader.SetGlobalFloat("_ShadowBias", 0.005f);

        // Render main camera into the output texture using the replacement shader
        mainCam.targetTexture = outputRT;
        mainCam.RenderWithShader(shadowProjectorShader, "RenderType");
        mainCam.targetTexture = null;

        // --- 4. SAVE TO PNG ---
        SaveRT(outputRT, $"{outputFolder}/{mainCam.name}_Shadow{suffix ?? ""}.png");

        // --- 5. CLEANUP ---
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(shadowMap);
        RenderTexture.ReleaseTemporary(outputRT);
        DestroyImmediate(tempCamGO);

        // Clear globals to prevent leaking into scene view
        Shader.SetGlobalTexture("_ShadowMap", null);
        Shader.SetGlobalMatrix("_WorldToShadowMatrix", Matrix4x4.identity);
    }

    private void RenderAlbedo(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/Albedo"), "Albedo", rt);
    }
    private void RenderAlpha(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/Alpha"), "Alpha", rt);
    }

    private void RenderAO(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/AO"), "AO", rt);
    }

    private void RenderNormalMap(Camera cam, RenderTexture rt)
    {
        RenderWithShader(cam, Shader.Find("LeapForward/SpriteRenderer/Normal"), "Normal", rt);
    }

    private void RenderWithShader(Camera cam, Shader shader, string shaderName, RenderTexture rt)
    {
        cam.targetTexture = rt;

        if (shader != null)
            cam.RenderWithShader(shader, "RenderType");
        else
            cam.Render();

        cam.targetTexture = null;

        SaveRT(rt, $"{outputFolder}/{cam.name}_{shaderName}.png");
    }

    private void SaveRT(RenderTexture rt, string fileName, bool invert = false)
    {
        TextureFormat format =  TextureFormat.RGBA32;

        Func<Texture2D, Byte[]> encodeFunc;

        RenderTexture tempColorRT = null;

        switch (Path.GetExtension(fileName).ToLower())
        {
            case ".exr":
                tempColorRT = new RenderTexture(rt.width, rt.height, 0, RenderTextureFormat.RFloat);
                tempColorRT.Create();
                Graphics.Blit(rt, tempColorRT);
                rt = tempColorRT;
                format = TextureFormat.RFloat;
                encodeFunc = (image)=> image.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
                break;
            case ".tga":
                encodeFunc = (image) => image.EncodeToTGA();
                break;
            case ".jpg":
                encodeFunc = (image) => image.EncodeToJPG();
                break;
            default:
                encodeFunc = (image) => image.EncodeToPNG();
                break;

        }

        RenderTexture.active = rt;
        Texture2D screenShot = new Texture2D(outputWidth, outputHeight, format, false);
        screenShot.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
        screenShot.Apply();

        byte[] bytes = encodeFunc(screenShot);

        File.WriteAllBytes(fileName, bytes);

        RenderTexture.active = null;

        if (tempColorRT != null)
            DestroyImmediate(tempColorRT);
        DestroyImmediate(screenShot);
    }
}

