using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace DeadlyJenga.XRSimulation.Editor
{
    internal sealed class StockExpressionActions : IDisposable
    {
        [Serializable] sealed class State
        { public string expression, action, id; public bool priorEnabled, enabledForCase, enabledAfterRestore; }
        [Serializable] sealed class References
        {
            public string expression, performedActionId, valueActionId, repairedFromAsset;
            public bool originalPerformedPresent, temporaryPerformedReference, referenceRestored = true;
        }
        [Serializable] sealed class Receipt
        {
            public string scope = "Only stock Open/Fist referenced actions; borrowed actions are never disposed";
            public string loadedAssemblyMvid = typeof(StockExpressionActions).Assembly.ManifestModule.ModuleVersionId.ToString();
            public List<State> states = new List<State>();
            public List<References> references = new List<References>();
        }
        readonly Receipt receipt = new Receipt();
        readonly List<InputAction> actions = new List<InputAction>();
        readonly List<(XRInputButtonReader reader, InputActionReference original, InputActionReference temporary, References details)> bindings = new();
        readonly string output;
        bool disposed;
        public bool Restored => actions.Select((action, i) => action.enabled == receipt.states[i].priorEnabled).All(x => x) &&
            bindings.All(binding => ReferenceEquals(binding.reader.inputActionReferencePerformed, binding.original) && binding.details.referenceRestored);
        public StockExpressionActions(XRInteractionSimulator simulator, string output)
        {
            this.output = output;
            try
            {
                foreach (var name in new[] { "Open", "Fist" })
                {
                    var expression = simulator.handExpressionManager.simulatedHandExpressions.Single(x => x.name == name);
                    var reader = expression.toggleInput;
                    if (reader.inputSourceMode != XRInputButtonReader.InputSourceMode.InputActionReference)
                        throw new InvalidOperationException("Expected stock referenced expression actions for " + name);
                    var performed = reader.inputActionReferencePerformed?.action;
                    var value = reader.inputActionReferenceValue?.action;
                    var details = new References { expression = name, originalPerformedPresent = performed != null, valueActionId = value?.id.ToString() ?? "<absent>" };
                    receipt.references.Add(details);
                    if (performed == null)
                    {
                        var path = ProjectPath.Find("XR Interaction Hand Controls.inputactions");
                        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                        var candidate = asset?.FindAction("Hands/" + name);
                        var key = name == "Open" ? "<Keyboard>/o" : "<Keyboard>/p";
                        if (candidate != null && candidate.type == InputActionType.Button && candidate.bindings.Any(binding => binding.path == key))
                        {
                            var temporary = InputActionReference.Create(candidate);
                            bindings.Add((reader, reader.inputActionReferencePerformed, temporary, details));
                            reader.inputActionReferencePerformed = temporary;
                            performed = temporary.action;
                            details.repairedFromAsset = path;
                            details.temporaryPerformedReference = true;
                            details.referenceRestored = false;
                        }
                    }
                    details.performedActionId = performed?.id.ToString() ?? "<absent>";
                    if (performed == null) throw new InvalidOperationException("Missing stock performed expression action for " + name);
                    foreach (var action in new[] { performed, value }.Where(action => action != null).Distinct())
                    {
                        if (actions.Contains(action)) continue;
                        actions.Add(action);
                        var state = new State { expression = name, action = action.name, id = action.id.ToString(), priorEnabled = action.enabled };
                        receipt.states.Add(state);
                        action.Enable();
                        state.enabledForCase = action.enabled;
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            for (int i = 0; i < actions.Count; i++)
                try
                {
                    if (receipt.states[i].priorEnabled) actions[i].Enable(); else actions[i].Disable();
                    receipt.states[i].enabledAfterRestore = actions[i].enabled;
                }
                catch (Exception error) { errors.Add(error); }
            foreach (var binding in bindings)
                try
                {
                    binding.reader.inputActionReferencePerformed = binding.original;
                    UnityEngine.Object.DestroyImmediate(binding.temporary);
                    binding.details.referenceRestored = true;
                }
                catch (Exception error) { errors.Add(error); }
            File.WriteAllText(output, JsonUtility.ToJson(receipt, true));
            if (errors.Count > 0) throw new AggregateException("Restore stock expression action states", errors);
        }
    }
}
