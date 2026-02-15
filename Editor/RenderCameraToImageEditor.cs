using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(RenderCameraToImage))]
public class RenderCameraToImageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (resolution, light, etc.)
        DrawDefaultInspector();

        // Reference the actual script
        RenderCameraToImage script = (RenderCameraToImage)target;

        GUILayout.Space(10); // Add a little breathing room

        // Create the button
        if (GUILayout.Button("Render To Image", GUILayout.Height(30)))
        {
            script.RenderToImage();
        }
    }
}