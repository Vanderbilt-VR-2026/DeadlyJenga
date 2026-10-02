using UnityEditor;

namespace FlyingHand.Editor
{
    [CustomEditor(typeof(FlyingHandController))]
    public sealed class FlyingHandControllerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (FlyingHandController)target;
            if (EditorApplication.isPlaying)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.EnumPopup("Active Control Mode", controller.ActiveControlMode);
                Repaint();
            }
        }
    }
}
