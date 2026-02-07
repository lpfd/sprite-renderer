using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Rendering;

public class SpriteRendererTool : EditorWindow
{
    public Camera[] targetCameras;
    public Shader[] shaderOverrides;
    public int outputWidth = 1024;
    public int outputHeight = 1024;
    public string outputFolder = "Assets/RenderedSprites";

    [MenuItem("Tools/Sprite Renderer Tool")]
    public static void ShowWindow()
    {
        GetWindow<SpriteRendererTool>("Sprite Renderer");
    }

    private void OnEnable()
    {
        // Default to Main Camera if array is empty
        if ((targetCameras == null || targetCameras.Length == 0) && Camera.main != null)
        {
            targetCameras = new Camera[] { Camera.main };
        }

        if (shaderOverrides == null || shaderOverrides.Length == 0)
        {
            shaderOverrides = new Shader[] { 
                Shader.Find("LeapForward/SpriteRenderer/Albedo"), 
                Shader.Find("LeapForward/SpriteRenderer/Normal"), 
                Shader.Find("LeapForward/SpriteRenderer/Height")
            };
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);

        // Using SerializedObject to render arrays nicely in the inspector
        ScriptableObject target = this;
        SerializedObject so = new SerializedObject(target);
        
        EditorGUILayout.PropertyField(so.FindProperty("targetCameras"), true);
        EditorGUILayout.PropertyField(so.FindProperty("shaderOverrides"), true);
        
        so.ApplyModifiedProperties();

        outputWidth = EditorGUILayout.IntField("Width", outputWidth);
        outputHeight = EditorGUILayout.IntField("Height", outputHeight);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();

        // Pipeline Validation
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            EditorGUILayout.HelpBox("Error: This tool only supports the Built-in Render Pipeline.", MessageType.Error);
        }
        else
        {
            if (GUILayout.Button("Render Sprites", GUILayout.Height(40)))
            {
                RenderSprites();
            }
        }
    }

    private void RenderSprites()
    {
        if (targetCameras == null || targetCameras.Length == 0) { Debug.LogError("No cameras assigned!"); return; }
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        RenderTexture rt = new RenderTexture(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32);

        try
        {
            foreach (Camera cam in targetCameras)
            {
                if (cam == null) continue;

                // Store original state per camera
                Quaternion originalRotation = cam.transform.rotation;
                Color originalBackground = cam.backgroundColor;
                CameraClearFlags originalFlags = cam.clearFlags;
                RenderTexture originalRT = cam.targetTexture;

                // Setup for transparency
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);

                int shaderCount = Mathf.Max(1, shaderOverrides.Length);
                for (int s = 0; s < shaderCount; s++)
                {
                    Shader currentShader = (shaderOverrides.Length > 0) ? shaderOverrides[s] : null;
                    string shaderName = currentShader != null ? currentShader.name.Replace("/", "_") : "Default";

                    cam.RenderWithShader(currentShader, "RenderType");

                    RenderTexture.active = rt;
                    Texture2D screenShot = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
                    screenShot.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
                    screenShot.Apply();

                    byte[] bytes = screenShot.EncodeToPNG();
                    // Naming: CameraName_ShaderName_AngleIndex.png
                    string fileName = $"{outputFolder}/{cam.name}_{shaderName}.png";
                    File.WriteAllBytes(fileName, bytes);

                    // Reset rotation for next calculation
                    cam.transform.rotation = originalRotation;
                    DestroyImmediate(screenShot);
                }

                // Restore camera state
                cam.targetTexture = originalRT;
                cam.backgroundColor = originalBackground;
                cam.clearFlags = originalFlags;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Batch rendering complete! Files saved to {outputFolder}");
        }
        finally
        {
            RenderTexture.active = null;
            DestroyImmediate(rt);
        }
    }
}