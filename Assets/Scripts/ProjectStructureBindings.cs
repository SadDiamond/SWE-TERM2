using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum ProjectStructureAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Jump,
    Dash,
    Slide,
    Interact,
    Grapple,
    Fire,
    AltFire,
    WeaponSlot1,
    WeaponSlot2,
    WeaponSlot3,
    VariantPrev,
    VariantNext,
    Guide
}

public enum ProjectStructureBindingDevice
{
    Keyboard,
    Mouse
}

public enum ProjectStructureMouseButton
{
    Left,
    Right,
    Middle,
    Back,
    Forward
}

public readonly struct ProjectStructureBindingValue
{
    public readonly ProjectStructureBindingDevice Device;
    public readonly Key Key;
    public readonly ProjectStructureMouseButton MouseButton;

    public ProjectStructureBindingValue(Key key)
    {
        Device = ProjectStructureBindingDevice.Keyboard;
        Key = key;
        MouseButton = ProjectStructureMouseButton.Left;
    }

    public ProjectStructureBindingValue(ProjectStructureMouseButton mouseButton)
    {
        Device = ProjectStructureBindingDevice.Mouse;
        Key = Key.None;
        MouseButton = mouseButton;
    }
}

public static class ProjectStructureBindings
{
    private const string PrefPrefix = "project_structure.bind.";

    private sealed class BindingDefinition
    {
        public ProjectStructureAction Action;
        public string Label;
        public ProjectStructureBindingValue DefaultValue;

        public BindingDefinition(ProjectStructureAction action, string label, ProjectStructureBindingValue defaultValue)
        {
            Action = action;
            Label = label;
            DefaultValue = defaultValue;
        }
    }

    private static readonly BindingDefinition[] Definitions =
    {
        new BindingDefinition(ProjectStructureAction.MoveForward, "Move Forward", new ProjectStructureBindingValue(Key.W)),
        new BindingDefinition(ProjectStructureAction.MoveBackward, "Move Back", new ProjectStructureBindingValue(Key.S)),
        new BindingDefinition(ProjectStructureAction.MoveLeft, "Move Left", new ProjectStructureBindingValue(Key.A)),
        new BindingDefinition(ProjectStructureAction.MoveRight, "Move Right", new ProjectStructureBindingValue(Key.D)),
        new BindingDefinition(ProjectStructureAction.Jump, "Jump", new ProjectStructureBindingValue(Key.Space)),
        new BindingDefinition(ProjectStructureAction.Dash, "Dash", new ProjectStructureBindingValue(Key.LeftShift)),
        new BindingDefinition(ProjectStructureAction.Slide, "Slide / Slam", new ProjectStructureBindingValue(Key.LeftCtrl)),
        new BindingDefinition(ProjectStructureAction.Interact, "Interact", new ProjectStructureBindingValue(Key.E)),
        new BindingDefinition(ProjectStructureAction.Grapple, "Grapple", new ProjectStructureBindingValue(Key.R)),
        new BindingDefinition(ProjectStructureAction.Fire, "Fire", new ProjectStructureBindingValue(ProjectStructureMouseButton.Left)),
        new BindingDefinition(ProjectStructureAction.AltFire, "Alt Fire", new ProjectStructureBindingValue(ProjectStructureMouseButton.Right)),
        new BindingDefinition(ProjectStructureAction.WeaponSlot1, "Weapon 1", new ProjectStructureBindingValue(Key.Digit1)),
        new BindingDefinition(ProjectStructureAction.WeaponSlot2, "Weapon 2", new ProjectStructureBindingValue(Key.Digit2)),
        new BindingDefinition(ProjectStructureAction.WeaponSlot3, "Weapon 3", new ProjectStructureBindingValue(Key.Digit3)),
        new BindingDefinition(ProjectStructureAction.VariantPrev, "Variant Prev", new ProjectStructureBindingValue(Key.Q)),
        new BindingDefinition(ProjectStructureAction.VariantNext, "Variant Next", new ProjectStructureBindingValue(Key.E)),
        new BindingDefinition(ProjectStructureAction.Guide, "Guide", new ProjectStructureBindingValue(Key.Tab)),
    };

    private static readonly Dictionary<ProjectStructureAction, ProjectStructureBindingValue> CurrentBindings = new Dictionary<ProjectStructureAction, ProjectStructureBindingValue>();
    private static readonly Dictionary<ProjectStructureAction, BindingDefinition> DefinitionMap = new Dictionary<ProjectStructureAction, BindingDefinition>();
    private static bool loaded;

    public static IReadOnlyList<ProjectStructureAction> GameplayActions => gameplayActions;
    private static readonly ProjectStructureAction[] gameplayActions =
    {
        ProjectStructureAction.MoveForward,
        ProjectStructureAction.MoveBackward,
        ProjectStructureAction.MoveLeft,
        ProjectStructureAction.MoveRight,
        ProjectStructureAction.Jump,
        ProjectStructureAction.Dash,
        ProjectStructureAction.Slide,
        ProjectStructureAction.Interact,
        ProjectStructureAction.Grapple,
        ProjectStructureAction.Fire,
        ProjectStructureAction.AltFire,
        ProjectStructureAction.WeaponSlot1,
        ProjectStructureAction.WeaponSlot2,
        ProjectStructureAction.WeaponSlot3,
        ProjectStructureAction.VariantPrev,
        ProjectStructureAction.VariantNext,
        ProjectStructureAction.Guide
    };

    public static void EnsureLoaded()
    {
        if (loaded)
            return;

        DefinitionMap.Clear();
        CurrentBindings.Clear();
        for (int i = 0; i < Definitions.Length; i++)
        {
            BindingDefinition definition = Definitions[i];
            DefinitionMap[definition.Action] = definition;
            CurrentBindings[definition.Action] = LoadBinding(definition);
        }

        loaded = true;
    }

    public static string GetLabel(ProjectStructureAction action)
    {
        EnsureLoaded();
        return DefinitionMap.TryGetValue(action, out BindingDefinition definition) ? definition.Label : action.ToString();
    }

    public static ProjectStructureBindingValue GetBinding(ProjectStructureAction action)
    {
        EnsureLoaded();
        return CurrentBindings.TryGetValue(action, out ProjectStructureBindingValue value)
            ? value
            : default;
    }

    public static void SetBinding(ProjectStructureAction action, ProjectStructureBindingValue value, bool persist = true)
    {
        EnsureLoaded();
        CurrentBindings[action] = value;
        if (!persist)
            return;

        PlayerPrefs.SetString(GetPrefKey(action), Serialize(value));
        PlayerPrefs.Save();
    }

    public static void ResetAction(ProjectStructureAction action)
    {
        EnsureLoaded();
        if (!DefinitionMap.TryGetValue(action, out BindingDefinition definition))
            return;

        CurrentBindings[action] = definition.DefaultValue;
        PlayerPrefs.DeleteKey(GetPrefKey(action));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        for (int i = 0; i < Definitions.Length; i++)
        {
            BindingDefinition definition = Definitions[i];
            CurrentBindings[definition.Action] = definition.DefaultValue;
            PlayerPrefs.DeleteKey(GetPrefKey(definition.Action));
        }

        PlayerPrefs.Save();
    }

    public static string GetDisplayString(ProjectStructureAction action)
    {
        return GetDisplayString(GetBinding(action));
    }

    public static string GetDisplayString(ProjectStructureBindingValue value)
    {
        return value.Device switch
        {
            ProjectStructureBindingDevice.Mouse => value.MouseButton switch
            {
                ProjectStructureMouseButton.Left => "Left Mouse",
                ProjectStructureMouseButton.Right => "Right Mouse",
                ProjectStructureMouseButton.Middle => "Middle Mouse",
                ProjectStructureMouseButton.Back => "Mouse Back",
                ProjectStructureMouseButton.Forward => "Mouse Forward",
                _ => "Mouse"
            },
            _ => FormatKeyName(value.Key)
        };
    }

    public static bool IsPressed(ProjectStructureAction action)
    {
        ButtonControl control = ResolveControl(GetBinding(action));
        return control != null && control.isPressed;
    }

    public static bool WasPressedThisFrame(ProjectStructureAction action)
    {
        ButtonControl control = ResolveControl(GetBinding(action));
        return control != null && control.wasPressedThisFrame;
    }

    public static bool WasReleasedThisFrame(ProjectStructureAction action)
    {
        ButtonControl control = ResolveControl(GetBinding(action));
        return control != null && control.wasReleasedThisFrame;
    }

    public static Vector2 ReadMovementVector()
    {
        float x = 0f;
        float y = 0f;

        if (IsPressed(ProjectStructureAction.MoveRight)) x += 1f;
        if (IsPressed(ProjectStructureAction.MoveLeft)) x -= 1f;
        if (IsPressed(ProjectStructureAction.MoveForward)) y += 1f;
        if (IsPressed(ProjectStructureAction.MoveBackward)) y -= 1f;

        Vector2 value = new Vector2(x, y);
        return value.sqrMagnitude > 1f ? value.normalized : value;
    }

    public static bool TryCaptureBinding(out ProjectStructureBindingValue value, out bool cancelled)
    {
        EnsureLoaded();
        value = default;
        cancelled = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                cancelled = true;
                return false;
            }

            IReadOnlyList<KeyControl> keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl keyControl = keys[i];
                if (keyControl == null || !keyControl.wasPressedThisFrame)
                    continue;
                value = new ProjectStructureBindingValue(keyControl.keyCode);
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            value = new ProjectStructureBindingValue(ProjectStructureMouseButton.Left);
            return true;
        }
        if (mouse.rightButton.wasPressedThisFrame)
        {
            value = new ProjectStructureBindingValue(ProjectStructureMouseButton.Right);
            return true;
        }
        if (mouse.middleButton.wasPressedThisFrame)
        {
            value = new ProjectStructureBindingValue(ProjectStructureMouseButton.Middle);
            return true;
        }
        if (mouse.backButton != null && mouse.backButton.wasPressedThisFrame)
        {
            value = new ProjectStructureBindingValue(ProjectStructureMouseButton.Back);
            return true;
        }
        if (mouse.forwardButton != null && mouse.forwardButton.wasPressedThisFrame)
        {
            value = new ProjectStructureBindingValue(ProjectStructureMouseButton.Forward);
            return true;
        }

        return false;
    }

    private static ProjectStructureBindingValue LoadBinding(BindingDefinition definition)
    {
        string serialized = PlayerPrefs.GetString(GetPrefKey(definition.Action), string.Empty);
        return TryDeserialize(serialized, out ProjectStructureBindingValue value) ? value : definition.DefaultValue;
    }

    private static string GetPrefKey(ProjectStructureAction action)
    {
        return PrefPrefix + action;
    }

    private static string Serialize(ProjectStructureBindingValue value)
    {
        return value.Device == ProjectStructureBindingDevice.Mouse
            ? "mouse:" + value.MouseButton
            : "key:" + value.Key;
    }

    private static bool TryDeserialize(string serialized, out ProjectStructureBindingValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(serialized))
            return false;

        string[] parts = serialized.Split(':');
        if (parts.Length != 2)
            return false;

        if (parts[0] == "key" && Enum.TryParse(parts[1], out Key key))
        {
            value = new ProjectStructureBindingValue(key);
            return true;
        }
        if (parts[0] == "mouse" && Enum.TryParse(parts[1], out ProjectStructureMouseButton mouseButton))
        {
            value = new ProjectStructureBindingValue(mouseButton);
            return true;
        }

        return false;
    }

    private static ButtonControl ResolveControl(ProjectStructureBindingValue value)
    {
        if (value.Device == ProjectStructureBindingDevice.Mouse)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return null;

            return value.MouseButton switch
            {
                ProjectStructureMouseButton.Left => mouse.leftButton,
                ProjectStructureMouseButton.Right => mouse.rightButton,
                ProjectStructureMouseButton.Middle => mouse.middleButton,
                ProjectStructureMouseButton.Back => mouse.backButton,
                ProjectStructureMouseButton.Forward => mouse.forwardButton,
                _ => null
            };
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || value.Key == Key.None)
            return null;

        return keyboard[value.Key];
    }

    private static string FormatKeyName(Key key)
    {
        return key switch
        {
            Key.LeftShift => "Left Shift",
            Key.RightShift => "Right Shift",
            Key.LeftCtrl => "Left Ctrl",
            Key.RightCtrl => "Right Ctrl",
            Key.LeftAlt => "Left Alt",
            Key.RightAlt => "Right Alt",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Backquote => "`",
            Key.Digit1 => "1",
            Key.Digit2 => "2",
            Key.Digit3 => "3",
            Key.Digit4 => "4",
            Key.Digit5 => "5",
            Key.Digit6 => "6",
            Key.Digit7 => "7",
            Key.Digit8 => "8",
            Key.Digit9 => "9",
            Key.Digit0 => "0",
            _ => key.ToString().Replace("Numpad", "Num ").Replace("Arrow", " Arrow")
        };
    }
}
