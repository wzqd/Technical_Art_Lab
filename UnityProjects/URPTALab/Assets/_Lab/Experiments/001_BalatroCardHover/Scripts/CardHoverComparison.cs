using UnityEngine;

namespace TechArtLab.Balatro
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class CardHoverComparison : MonoBehaviour
    {
        public Material hlslMaterial;
        public Material shaderGraphMaterial;

        void OnGUI()
        {
            var card = GetComponent<MeshRenderer>();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Max(0.5f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(Screen.width / scale - 285, 20, 265, 105), GUI.skin.box);
            GUILayout.Label(card.sharedMaterial == shaderGraphMaterial ? "Active: Shader Graph" : "Active: HLSL");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shader Graph")) card.sharedMaterial = shaderGraphMaterial;
            if (GUILayout.Button("HLSL")) card.sharedMaterial = hlslMaterial;
            GUILayout.EndHorizontal();
            GUILayout.Label("Same geometry, texture and driver inputs.");
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
