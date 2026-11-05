using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.XRSimulation.Editor
{
    public sealed class PopupTelemetry : IDisposable
    {
        [Serializable] public sealed class ControllerSample
        {
            public int id, trackingState;
            public string name, layout, usages;
            public bool tracked, grip, trigger, primary, secondary, stickClick, menu;
            public float gripAmount, triggerAmount;
            public Vector2 stick;
        }
        [Serializable] public sealed class InputGates
        {
            public bool applicationFocused, menuOpen, playingHand, playerSuspended, playerActive;
            public string sessionFocused, playerFocused, playerAlive, actionStatus, menuResumePending, menuInputArmed;
            public string menuTransitionFrame, travelInputHeld, networkWaiting, demoSpectating, demoAuto;
            public string arrivalActive, arrivalProgress, curatedHold, experience, localActorId, frameRole, movementHeld;
            public string grounded, standingBody, standingSurface;
            public string lastMenuLeft, lastMenuRight, lastMenuTracking, travelReleaseAt, gameplayActionHeld, opticalMenuHeld, inputUpdate;
        }
        [Serializable] public sealed class Boundary
        {
            public int frame;
            public string label, updateType;
            public ControllerSample[] controllers;
        }
        readonly IDisposable sampling;
        public PopupTelemetry() { sampling = ManagedFrameObserver.Register(SampleAfterLateUpdate); }
        public void Dispose() { sampling.Dispose(); }
        public int SampleCount => log.samples.Count;
        public InputGates LatestGates => log.samples.Count == 0 ? null : log.samples[log.samples.Count - 1].gates;
        [Serializable] public sealed class Sample
        {
            public int frame, cameraId, componentCount, visiblePanels, risingEdges;
            public int[] pickerIds;
            public int panelId;
            public int tabletopBoardId, tabletopBoardCount, tabletopIntentCount;
            public int[] worldMenuIds;
            public string phase;
            public bool menuOpen, vr, playingHand, leftDominant, shown, boundsMeasured, fullyInView, canvasEnabled;
            public bool leftControllerTracked, rightControllerTracked;
            public bool seaSpiritActive, tabletopEnabled, tabletopPlacement;
            public InputGates gates;
            public ControllerSample[] controllers;
            public string tabletopStatus;
            public Vector3 cameraPosition, cameraForward, cachedSide, viewportMin, viewportMax;
        }
        [Serializable] sealed class Log
        {
            public string kind = "Read-only managed PlayerLoop observations after real ScriptRunBehaviourLateUpdate; synthetic XR, not headset validation";
            public string samplingPoint = ManagedFrameObserver.SamplingPoint;
            public string interpretation = "Transitions are tagged with the test phase. Reopens and offscreen bounds are observations, not automatic bug verdicts. tabletopIntentCount is private page intent, distinct from board Enabled/Placement. Invoking UI row is not instrumented; phase records deliberate scenario commands only.";
            public string scenePath, selectedMapId, selectedModeId, actualWorldType, actualWorldObject;
            public List<Sample> samples = new List<Sample>();
            public List<Boundary> inputBoundaries = new List<Boundary>();
        }
        public string OutputPath, Phase = "startup";
        readonly Log log = new Log();
        bool previousShown;
        int risingEdges, lastRecordedFrame = -1;
        static ControllerSample[] Controllers()
        {
            var controllers = new List<ControllerSample>();
            foreach (var device in InputSystem.devices)
                if (device is XRController controller)
                    controllers.Add(new ControllerSample
                    {
                        id = controller.deviceId, name = controller.name, layout = controller.layout,
                        usages = string.Join(",", controller.usages), tracked = controller.isTracked.isPressed,
                        trackingState = controller.trackingState.ReadValue(),
                        grip = controller.TryGetChildControl<ButtonControl>("gripPressed")?.isPressed == true,
                        trigger = controller.TryGetChildControl<ButtonControl>("triggerPressed")?.isPressed == true,
                        gripAmount = controller.TryGetChildControl<AxisControl>("grip")?.ReadValue() ?? 0,
                        triggerAmount = controller.TryGetChildControl<AxisControl>("trigger")?.ReadValue() ?? 0,
                        primary = controller.TryGetChildControl<ButtonControl>("primaryButton")?.isPressed == true,
                        secondary = controller.TryGetChildControl<ButtonControl>("secondaryButton")?.isPressed == true,
                        stickClick = controller.TryGetChildControl<ButtonControl>("primary2DAxisClick")?.isPressed == true,
                        menu = (controller.TryGetChildControl<ButtonControl>("menuButton") ?? controller.TryGetChildControl<ButtonControl>("menu"))?.isPressed == true,
                        stick = controller.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue() ?? Vector2.zero
                    });
            return controllers.ToArray();
        }
        public void RecordBoundary(string label)
        {
            log.inputBoundaries.Add(new Boundary { frame = Time.frameCount, label = label,
                updateType = InputState.currentUpdateType.ToString(), controllers = Controllers() });
        }
        static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner);
        static object Property(object owner, string name) => owner?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(owner);
        static object Member(object owner, string name)
        {
            if (owner == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return owner.GetType().GetProperty(name, flags)?.GetValue(owner) ?? owner.GetType().GetField(name, flags)?.GetValue(owner);
        }
        static string Text(object owner, string name) => Member(owner, name)?.ToString() ?? "<absent>";
        static InputGates Gates(CastlePlaySession session)
        {
            var player = session?.Player;
            var modes = Member(session, "Modes");
            object arrival = null;
            if (session) foreach (var component in session.GetComponents<MonoBehaviour>())
                if (component && component.GetType().FullName == "DeadlyJenga.CastlePrototype.PlayerSpawnArrival") arrival = component;
            var gates = new InputGates
            {
                applicationFocused = Application.isFocused, menuOpen = session && session.MenuOpen,
                playingHand = session && session.PlayingHand, playerSuspended = player?.InputSuspended ?? false,
                playerActive = player is Component playerComponent && playerComponent && playerComponent.gameObject.activeInHierarchy,
                sessionFocused = Text(session, "focused"), playerFocused = Text(player, "HasInputFocus"),
                playerAlive = Text(player, "Alive"), actionStatus = Text(player, "ActionStatus"),
                grounded = Text(player, "Grounded"), standingBody = Text(player, "StandingBody"), standingSurface = Text(player, "StandingSurface"),
                menuResumePending = Text(session, "menuResumePending"), menuInputArmed = Text(session, "menuInputArmed"),
                menuTransitionFrame = Text(session, "menuTransitionFrame"), travelInputHeld = Text(session, "travelInputHeld"),
                networkWaiting = Text(Member(session, "Network"), "WaitingForWorld"), demoSpectating = Text(modes, "DemoSpectating"),
                demoAuto = Text(Member(session, "Demo"), "Auto"), arrivalActive = Text(arrival, "Active"), arrivalProgress = Text(arrival, "Progress"),
                localActorId = Text(modes, "LocalActorId"), movementHeld = Text(player, "MovementHeld"), frameRole = "<absent>",
                lastMenuLeft = Text(session, "lastMenuLeft"), lastMenuRight = Text(session, "lastMenuRight"),
                lastMenuTracking = Text(session, "lastMenuTracking"), travelReleaseAt = Text(session, "travelReleaseAt"),
                opticalMenuHeld = Text(player, "MenuHeld"), inputUpdate = InputState.currentUpdateType.ToString()
            };
            if (Member(Member(modes, "Frame"), "players") is Array rows)
                foreach (var row in rows) if (Text(row, "id") == gates.localActorId) gates.frameRole = Text(row, "role");
            var type = session?.GetType().Assembly.GetType("DeadlyJenga.CastlePrototype.RoomGameExperience");
            var lookup = type?.GetMethod("ForManagedMatch", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var provider = lookup?.Invoke(null, new object[] { session });
            gates.experience = Text(provider, "Experience");
            var hold = type?.GetMethod("HoldsLocalParticipant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            gates.curatedHold = provider != null && hold != null ? hold.Invoke(provider, new object[] { session })?.ToString() : "<absent>";
            // These methods only read current input. Never invoke PollMenuInput, HoldMenuGameplayInput or focus handlers.
            var controllerReader = session?.GetType().GetMethod("MenuController", BindingFlags.Static | BindingFlags.NonPublic);
            var heldReader = session?.GetType().GetMethod("GameplayActionHeld", BindingFlags.Instance | BindingFlags.NonPublic);
            gates.gameplayActionHeld = controllerReader != null && heldReader != null ? heldReader.Invoke(session, new[]
            { controllerReader.Invoke(null, new object[] { true }), controllerReader.Invoke(null, new object[] { false }) })?.ToString() : "<absent>";
            return gates;
        }
        public string GateSummary() => JsonUtility.ToJson(Gates(Services.Session as CastlePlaySession));
        public InputGates CaptureGates() => Gates(Services.Session as CastlePlaySession);

        void SampleAfterLateUpdate()
        {
            var session = Services.Session;
            var camera = session?.ActiveCamera;
            var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pickers = Array.FindAll(components,
                component => component && component.GetType().FullName == "DeadlyJenga.CastlePrototype.CharacterPicker");
            var menus = Array.FindAll(components,
                component => component && component.GetType().FullName == "DeadlyJenga.CastlePrototype.CastleWorldMenu");
            var sample = new Sample
            {
                frame = Time.frameCount, phase = Phase, componentCount = pickers.Length,
                pickerIds = Array.ConvertAll(pickers, picker => picker.GetInstanceID()),
                worldMenuIds = Array.ConvertAll(menus, menu => menu.GetInstanceID()),
                menuOpen = session?.MenuOpen ?? false, vr = session?.IsVR ?? false,
                playingHand = session is CastlePlaySession game && game.PlayingHand,
                leftDominant = PlayerPrefs.GetInt("CandyBlaster.LeftHanded", 0) != 0,
                cameraId = camera ? camera.GetInstanceID() : 0,
                cameraPosition = camera ? camera.transform.position : Vector3.zero,
                cameraForward = camera ? camera.transform.forward : Vector3.zero,
                leftControllerTracked = DeadlyJenga.CandyPlayers.HybridXRInput.ControllerTracked(true),
                rightControllerTracked = DeadlyJenga.CandyPlayers.HybridXRInput.ControllerTracked(false),
                seaSpiritActive = Property(session, "SeaSpiritActive") is bool sea && sea,
                gates = Gates(session as CastlePlaySession), controllers = Controllers(),
            };
            foreach (var menu in menus)
                if (Field(menu, "tabletopPage") is bool intent && intent) sample.tabletopIntentCount++;
            var boards = Array.FindAll(components, component => component && component.GetType().FullName == "DeadlyJenga.Presentation.CandyTabletop");
            sample.tabletopBoardCount = boards.Length;
            var board = boards.Length > 0 ? boards[0].GetType().GetProperty("Current", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as MonoBehaviour : null;
            if (board)
            {
                sample.tabletopBoardId = board.GetInstanceID();
                sample.tabletopEnabled = Property(board, "Enabled") is bool enabled && enabled;
                sample.tabletopPlacement = Property(board, "Placement") is bool placement && placement;
                sample.tabletopStatus = Property(board, "Status") as string;
            }
            foreach (var picker in pickers)
            {
                bool shown = Field(picker, "shown") is bool value && value;
                sample.shown |= shown;
                var panel = Field(picker, "panel") as Canvas;
                if (shown && panel && panel.gameObject.activeInHierarchy)
                {
                    sample.visiblePanels++;
                    sample.panelId = panel.GetInstanceID(); sample.canvasEnabled = panel.enabled;
                    if (Field(picker, "side") is Vector3 side) sample.cachedSide = side;
                    var rect = panel.transform as RectTransform;
                    if (rect && camera)
                    {
                        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                        foreach (var corner in corners)
                        {
                            var point = camera.WorldToViewportPoint(corner);
                            min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                        }
                        sample.boundsMeasured = true; sample.viewportMin = min; sample.viewportMax = max;
                        sample.fullyInView = min.z > 0 && min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1;
                    }
                }
            }
            if (sample.shown && !previousShown) risingEdges++;
            sample.risingEdges = risingEdges; previousShown = sample.shown;
            if (lastRecordedFrame != sample.frame && log.samples.Count < 12000)
            { log.samples.Add(sample); lastRecordedFrame = sample.frame; }
        }
        public void Save()
        {
            log.scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            log.selectedMapId = MatchSetup.MapId; log.selectedModeId = MatchSetup.ModeId;
            log.actualWorldType = Services.World?.GetType().FullName;
            log.actualWorldObject = (Services.World as Component)?.gameObject.name;
            if (!string.IsNullOrEmpty(OutputPath)) File.WriteAllText(OutputPath, JsonUtility.ToJson(log, true));
        }
    }
}
