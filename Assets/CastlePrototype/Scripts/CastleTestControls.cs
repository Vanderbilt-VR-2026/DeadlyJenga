using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.CastlePrototype
{
    public class CastleTestControls : MonoBehaviour
    {
        public CastleGenerator generator;
        public Camera viewCamera;
        public float lightImpulse = 160, strongImpulse = 900;
        Vector3 target;
        float yaw = 40, pitch = 25, distance = 65;
        float frameMs;
        Rigidbody selected;

        void Start() { FrameCastle(); }
        public void FrameCastle()
        {
            float height = (generator.settings.singleTower ? generator.settings.cornerCourses : Mathf.Max(generator.settings.keepCourses, generator.settings.cornerCourses)) * generator.settings.blockSize.y;
            target = generator.transform.position + Vector3.up * height * .4f;
            distance = Mathf.Max(CastleLayout.Footprint(generator.settings), height) * 2;
            UpdateCamera();
        }
        void Update()
        {
            frameMs = Mathf.Lerp(frameMs, Time.unscaledDeltaTime * 1000, .05f);
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) { generator.Rebuild(); selected = null; }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame) FrameCastle();
            if (mouse == null) return;
            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue(); yaw += d.x * .18f; pitch = Mathf.Clamp(pitch - d.y * .18f, -10, 80);
            }
            if (mouse.middleButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                target -= (viewCamera.transform.right * d.x + viewCamera.transform.up * d.y) * distance * .001f;
            }
            distance = Mathf.Clamp(distance * Mathf.Exp(-mouse.scroll.ReadValue().y * .001f), 4, 240);
            UpdateCamera();
            Vector2 cursor = mouse.position.ReadValue();
            if (cursor.x < 365 && cursor.y > Screen.height - 360) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                selected = null;
                if (Physics.Raycast(viewCamera.ScreenPointToRay(cursor), out var hit, 500, ~(1 << CastleBlock.SurfaceLayer))
                    && hit.rigidbody && generator.Bodies.Contains(hit.rigidbody))
                {
                    selected = hit.rigidbody;
                    bool strong = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                    selected.AddForceAtPosition(viewCamera.transform.forward * (strong ? strongImpulse : lightImpulse), hit.point, ForceMode.Impulse);
                }
            }
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                if (Physics.Raycast(viewCamera.ScreenPointToRay(cursor), out var hit, 500, ~(1 << CastleBlock.SurfaceLayer))
                    && hit.rigidbody && generator.Bodies.Contains(hit.rigidbody))
                { generator.Bodies.Remove(hit.rigidbody); Destroy(hit.rigidbody.gameObject); selected = null; }
            }
        }
        void UpdateCamera()
        {
            viewCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            viewCamera.transform.position = target - viewCamera.transform.forward * distance;
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(14, 14, 345, 340), GUI.skin.box);
            GUILayout.Label("CASTLE PHYSICS LAB");
            GUILayout.Label("Right drag: orbit · Middle drag: pan · Wheel: zoom");
            GUILayout.Label("Click: push · Shift+click: impact · X: remove hovered block");
            GUILayout.Label("R: rebuild · F: frame castle");
            if (GUILayout.Button("Reset castle")) { generator.Rebuild(); selected = null; }
            if (GUILayout.Button(generator.settings.singleTower ? "Build complete castle" : "Test one paired tower"))
            { generator.settings.singleTower = !generator.settings.singleTower; generator.Rebuild(); FrameCastle(); }
            if (GUILayout.Button("Wake all blocks (stability test)")) foreach (var b in generator.Bodies) if (b) b.WakeUp();
            GUI.enabled = selected;
            if (GUILayout.Button("Strong impact on selected block")) selected.AddForce(viewCamera.transform.forward * strongImpulse, ForceMode.Impulse);
            if (GUILayout.Button("Remove selected block"))
            { generator.Bodies.Remove(selected); Destroy(selected.gameObject); selected = null; }
            GUI.enabled = true;
            int awake = 0; foreach (var b in generator.Bodies) if (b && !b.IsSleeping()) awake++;
            GUILayout.Label($"Blocks: {generator.Bodies.Count} · Awake: {awake}");
            GUILayout.Label($"Frame: {frameMs:F1} ms · Build: {generator.LastBuildMilliseconds:F1} ms");
            GUILayout.Label(selected ? $"Last pushed: {selected.name}" : "No block selected");
            GUILayout.EndArea();
        }
    }
}


