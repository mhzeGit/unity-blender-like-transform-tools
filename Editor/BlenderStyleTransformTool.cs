using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Minimal Blender-inspired transform tool for Unity.
/// Hotkeys:
/// - G: Enter Grab/Move mode
/// - R: Enter Rotate mode  
/// - S: Enter Scale mode
/// - X/Y/Z: Filter to specific axis during transform
/// - Shift+X/Y/Z: Exclude specific axis from transform
/// - Escape/RightClick: Cancel current transform
/// - LeftClick/Enter: Confirm current transform
/// </summary>
public static class BlenderStyleTransformTool
{
    public enum TransformMode
    {
        None,
        Grab,
        Rotate, 
        Scale
    }

    public enum AxisFilter
    {
        None,
        X,
        Y, 
        Z,
        YZ, // Shift+X (exclude X)
        XZ, // Shift+Y (exclude Y)
        XY  // Shift+Z (exclude Z)
    }

    private static TransformMode currentMode = TransformMode.None;
    private static AxisFilter currentAxisFilter = AxisFilter.None;
    private static Vector2 mouseStartPos;
    private static Vector3[] originalPositions;
    private static Quaternion[] originalRotations;
    private static Vector3[] originalScales;
    private static Vector3[] currentPositions; // Current state during transform
    private static Quaternion[] currentRotations;
    private static Vector3[] currentScales;
    private static Transform[] selectedTransforms;
    private static bool isTransforming = false;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        SceneView.duringSceneGui -= OnSceneViewGUI;
        SceneView.duringSceneGui += OnSceneViewGUI;
        Selection.selectionChanged -= OnSelectionChanged;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged()
    {
        // Cancel any active transform when selection changes
        if (isTransforming)
        {
            CancelTransform();
        }
    }

    private static void OnSceneViewGUI(SceneView sceneView)
    {
        Event e = Event.current;
        
        // Handle input based on current state
        if (isTransforming)
        {
            HandleTransformInput(e);
        }
        else
        {
            // Check if we're in Unity navigation mode (RMB held)
            if (!IsNavigating())
            {
                HandleModeInput(e);
            }
        }

        // Draw UI overlay if transforming
        if (isTransforming)
        {
            DrawTransformOverlay();
        }
    }
    
    private static bool IsNavigating()
    {
        // Simply check if right mouse button is currently pressed in the event
        // This avoids using Input class which conflicts with new Input System
        Event current = Event.current;
        return current.button == 1 && 
               (current.type == EventType.MouseDown || 
                current.type == EventType.MouseDrag || 
                current.type == EventType.MouseUp);
    }

    private static void HandleModeInput(Event e)
    {
        if (e.type != EventType.KeyDown) return;
        if (Selection.transforms.Length == 0) return;
        
        // Only trigger if no modifier keys are pressed to avoid conflicts (Ctrl+S, etc.)
        if (e.control || e.alt || e.command) return;

        TransformMode newMode = TransformMode.None;
        
        switch (e.keyCode)
        {
            case KeyCode.G:
                newMode = TransformMode.Grab;
                break;
            case KeyCode.R:
                newMode = TransformMode.Rotate;
                break;
            case KeyCode.S:
                newMode = TransformMode.Scale;
                break;
        }

        if (newMode != TransformMode.None)
        {
            StartTransform(newMode);
            e.Use();
        }
    }

    private static void HandleTransformInput(Event e)
    {
        // Block context menu completely while transforming
        if (e.type == EventType.ContextClick || (e.type == EventType.MouseDown && e.button == 1))
        {
            CancelTransform();
            e.Use();
            return;
        }
        
        switch (e.type)
        {
            case EventType.KeyDown:
                HandleTransformKeyDown(e);
                break;
                
            case EventType.MouseMove:
            case EventType.MouseDrag:
                UpdateTransform(e.mousePosition);
                SceneView.RepaintAll();
                e.Use();
                break;
                
            case EventType.MouseDown:
                if (e.button == 0) // Left click - confirm
                {
                    ConfirmTransform();
                    e.Use();
                }
                break;
        }
    }

    private static void HandleTransformKeyDown(Event e)
    {
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                CancelTransform();
                e.Use();
                break;
                
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                ConfirmTransform();
                e.Use();
                break;
                
            case KeyCode.X:
                SetAxisFilter(e.shift ? AxisFilter.YZ : AxisFilter.X);
                e.Use();
                break;
                
            case KeyCode.Y:
                SetAxisFilter(e.shift ? AxisFilter.XZ : AxisFilter.Y);
                e.Use();
                break;
                
            case KeyCode.Z:
                SetAxisFilter(e.shift ? AxisFilter.XY : AxisFilter.Z);
                e.Use();
                break;
        }
    }

    private static void StartTransform(TransformMode mode)
    {
        currentMode = mode;
        currentAxisFilter = AxisFilter.None;
        isTransforming = true;
        mouseStartPos = Event.current.mousePosition;
        
        // Store selected transforms  
        selectedTransforms = Selection.transforms;
        originalPositions = new Vector3[selectedTransforms.Length];
        originalRotations = new Quaternion[selectedTransforms.Length];
        originalScales = new Vector3[selectedTransforms.Length];
        currentPositions = new Vector3[selectedTransforms.Length];
        currentRotations = new Quaternion[selectedTransforms.Length];
        currentScales = new Vector3[selectedTransforms.Length];
        
        // Store original values
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            originalPositions[i] = selectedTransforms[i].position;
            originalRotations[i] = selectedTransforms[i].rotation;
            originalScales[i] = selectedTransforms[i].localScale;
            
            currentPositions[i] = originalPositions[i];
            currentRotations[i] = originalRotations[i];
            currentScales[i] = originalScales[i];
        }
    }

    private static void SetAxisFilter(AxisFilter filter)
    {
        currentAxisFilter = filter;
    }

    private static void UpdateTransform(Vector2 mousePos)
    {
        Vector2 mouseDelta = mousePos - mouseStartPos;
        
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            Transform t = selectedTransforms[i];
            
            switch (currentMode)
            {
                case TransformMode.Grab:
                    UpdateGrab(t, originalPositions[i], mouseDelta, 0f); // sensitivity not needed for world-space
                    currentPositions[i] = t.position;
                    break;
                    
                case TransformMode.Rotate:
                    UpdateRotate(t, originalRotations[i], mouseDelta, 0f); // sensitivity not needed for world-space
                    currentRotations[i] = t.rotation;
                    break;
                    
                case TransformMode.Scale:
                    float distanceBasedSensitivity = GetDistanceBasedSensitivity(); // Keep scaling system
                    UpdateScale(t, originalScales[i], mouseDelta, distanceBasedSensitivity);
                    currentScales[i] = t.localScale;
                    break;
            }
        }
    }
    
    private static float GetDistanceBasedSensitivity()
    {
        if (selectedTransforms == null || selectedTransforms.Length == 0)
            return 0.001f;
            
        Camera sceneCamera = SceneView.lastActiveSceneView.camera;
        if (sceneCamera == null) return 0.001f;
        
        // Calculate average distance from camera to selected objects
        float averageDistance = 0f;
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            averageDistance += Vector3.Distance(sceneCamera.transform.position, selectedTransforms[i].position);
        }
        averageDistance /= selectedTransforms.Length;
        
        // Scale sensitivity based on distance and camera field of view
        // This ensures consistent visual movement speed regardless of camera distance
        float baseSensitivity = 0.001f;
        float distanceScale = averageDistance * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float screenScale = sceneCamera.pixelHeight * 0.5f;
        
        return baseSensitivity * distanceScale / screenScale * Screen.dpi / 96f;
    }

    private static void UpdateGrab(Transform t, Vector3 originalPos, Vector2 mouseDelta, float sensitivity)
    {
        // Project mouse start and current positions onto an imaginary plane at the object
        // The plane normal depends on the active axis constraint
        Vector3 startWorld = MouseToWorldOnPlane(mouseStartPos, originalPos, GetGrabPlaneNormal(originalPos));
        Vector3 currentWorld = MouseToWorldOnPlane(Event.current.mousePosition, originalPos, GetGrabPlaneNormal(originalPos));
        
        if (startWorld == Vector3.zero && currentWorld == Vector3.zero)
            return;
        
        Vector3 worldDelta = currentWorld - startWorld;
        
        // Constrain delta to the active axis
        worldDelta = ApplyAxisFilter(worldDelta);
        t.position = originalPos + worldDelta;
    }

    private static void UpdateRotate(Transform t, Quaternion originalRot, Vector2 mouseDelta, float sensitivity)
    {
        Vector3 rotAxis = GetRotationAxis();
        
        // Imaginary plane at the object facing the rotation axis
        Vector3 startWorld = MouseToWorldOnPlane(mouseStartPos, t.position, rotAxis);
        Vector3 currentWorld = MouseToWorldOnPlane(Event.current.mousePosition, t.position, rotAxis);
        
        if (startWorld == t.position || currentWorld == t.position)
            return;
        
        // Vectors from object center to each mouse world position
        Vector3 fromVec = (startWorld - t.position).normalized;
        Vector3 toVec   = (currentWorld - t.position).normalized;
        
        // Signed angle between them around the rotation axis
        float angle = Vector3.SignedAngle(fromVec, toVec, rotAxis);
        t.rotation = Quaternion.AngleAxis(angle, rotAxis) * originalRot;
    }

    private static void UpdateScale(Transform t, Vector3 originalScale, Vector2 mouseDelta, float sensitivity)
    {
        float scaleAmount;
        float scaleSensitivity = sensitivity * 2000f; // Scale up for distance-based sensitivity
        
        // Calculate scale amount based on axis filtering
        switch (currentAxisFilter)
        {
            case AxisFilter.X:
                scaleAmount = mouseDelta.x * scaleSensitivity;
                break;
            case AxisFilter.Y:
                scaleAmount = -mouseDelta.y * scaleSensitivity; // Flip Y
                break;
            case AxisFilter.Z:
                scaleAmount = -mouseDelta.y * scaleSensitivity; // Use Y mouse for Z
                break;
            default:
                scaleAmount = mouseDelta.x * scaleSensitivity; // Uniform scaling
                break;
        }
        
        float scaleMultiplier = 1f + scaleAmount;
        scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier); // Prevent negative scale
        
        Vector3 scaleVector = GetScaleVector(scaleMultiplier);
        Vector3 newScale = Vector3.Scale(originalScale, scaleVector);
        newScale = ApplyScaleAxisFilter(originalScale, newScale);
        t.localScale = newScale;
    }

    private static Vector3 ApplyAxisFilter(Vector3 vector)
    {
        switch (currentAxisFilter)
        {
            case AxisFilter.X:
                return new Vector3(vector.x, 0, 0);
            case AxisFilter.Y:
                return new Vector3(0, vector.y, 0);
            case AxisFilter.Z:
                return new Vector3(0, 0, vector.z);
            case AxisFilter.YZ: // Shift+X (exclude X)
                return new Vector3(0, vector.y, vector.z);
            case AxisFilter.XZ: // Shift+Y (exclude Y)
                return new Vector3(vector.x, 0, vector.z);
            case AxisFilter.XY: // Shift+Z (exclude Z)
                return new Vector3(vector.x, vector.y, 0);
            default:
                return vector;
        }
    }

    private static Vector3 ApplyScaleAxisFilter(Vector3 originalScale, Vector3 newScale)
    {
        switch (currentAxisFilter)
        {
            case AxisFilter.X:
                return new Vector3(newScale.x, originalScale.y, originalScale.z);
            case AxisFilter.Y:
                return new Vector3(originalScale.x, newScale.y, originalScale.z);
            case AxisFilter.Z:
                return new Vector3(originalScale.x, originalScale.y, newScale.z);
            case AxisFilter.YZ:
                return new Vector3(originalScale.x, newScale.y, newScale.z);
            case AxisFilter.XZ:
                return new Vector3(newScale.x, originalScale.y, newScale.z);
            case AxisFilter.XY:
                return new Vector3(newScale.x, newScale.y, originalScale.z);
            default:
                return newScale;
        }
    }

    private static Vector3 GetRotationAxis()
    {
        switch (currentAxisFilter)
        {
            case AxisFilter.X:
                return Vector3.right;
            case AxisFilter.Y:
                return Vector3.up;
            case AxisFilter.Z:
                return Vector3.forward;
            default:
                return Vector3.up; // Default to Y-axis rotation
        }
    }

    private static void ConfirmTransform()
    {
        // Restore to original state
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            selectedTransforms[i].position = originalPositions[i];
            selectedTransforms[i].rotation = originalRotations[i];
            selectedTransforms[i].localScale = originalScales[i];
        }
        
        // Register undo for complete objects at original state
        Undo.RegisterCompleteObjectUndo(selectedTransforms, GetUndoName());
        
        // Apply final transform state
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            selectedTransforms[i].position = currentPositions[i];
            selectedTransforms[i].rotation = currentRotations[i];
            selectedTransforms[i].localScale = currentScales[i];
        }
        
        EndTransform();
    }

    private static void CancelTransform()
    {
        // Manually restore original states (no undo recording)
        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            selectedTransforms[i].position = originalPositions[i];
            selectedTransforms[i].rotation = originalRotations[i];
            selectedTransforms[i].localScale = originalScales[i];
        }
        
        EndTransform();
    }

    private static void EndTransform()
    {
        isTransforming = false;
        currentMode = TransformMode.None;
        currentAxisFilter = AxisFilter.None;
        SceneView.RepaintAll();
    }

    private static string GetUndoName()
    {
        switch (currentMode)
        {
            case TransformMode.Grab:
                return "Blender Grab";
            case TransformMode.Rotate:
                return "Blender Rotate";
            case TransformMode.Scale:
                return "Blender Scale";
            default:
                return "Blender Transform";
        }
    }

    private static void DrawTransformOverlay()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        Handles.BeginGUI();
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.BeginVertical("box");
        
        // Mode indicator
        string modeText = currentMode.ToString().ToUpper();
        GUILayout.Label($"Mode: {modeText}", EditorStyles.boldLabel);
        
        // Axis filter indicator
        if (currentAxisFilter != AxisFilter.None)
        {
            string axisText = GetAxisFilterText();
            GUILayout.Label($"Axis: {axisText}", EditorStyles.label);
        }
        
        // Controls hint
        GUILayout.Label("LMB/Enter: Confirm | RMB/Esc: Cancel");
        GUILayout.Label("X/Y/Z: Filter axis | Shift+X/Y/Z: Exclude axis");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        Handles.EndGUI();
    }

    // Converts a GUI-space mouse position to a world-space point on an imaginary plane.
    // planePoint: a point on the plane (usually the object's position)
    // planeNormal: the normal of the imaginary plane
    private static Vector3 MouseToWorldOnPlane(Vector2 guiMousePos, Vector3 planePoint, Vector3 planeNormal)
    {
        Camera cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null) return Vector3.zero;
        
        // GUI space has Y flipped relative to screen space
        Vector3 screenPos = new Vector3(guiMousePos.x, cam.pixelHeight - guiMousePos.y, 0f);
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane plane = new Plane(planeNormal, planePoint);
        
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);
        
        return planePoint; // Ray is parallel to plane - no intersection
    }
    
    // For grab: the plane normal faces the camera so free movement is intuitive,
    // but when an axis is active we use a plane that best reveals that axis.
    private static Vector3 GetGrabPlaneNormal(Vector3 objectPos)
    {
        Camera cam = SceneView.lastActiveSceneView?.camera;
        if (cam == null) return Vector3.forward;
        
        switch (currentAxisFilter)
        {
            case AxisFilter.X:
                // Moving along X: use the plane whose normal is most aligned with camera
                // and is perpendicular to X (either Y or Z plane)
                return BestPerpendicularNormal(Vector3.right, cam.transform.forward);
            case AxisFilter.Y:
                return BestPerpendicularNormal(Vector3.up, cam.transform.forward);
            case AxisFilter.Z:
                return BestPerpendicularNormal(Vector3.forward, cam.transform.forward);
            default:
                // Free movement: plane faces the camera
                return cam.transform.forward;
        }
    }
    
    // Returns the plane normal perpendicular to 'axis' that is most face-on to the camera.
    // This gives the most stable drag plane for axis-constrained movement.
    private static Vector3 BestPerpendicularNormal(Vector3 axis, Vector3 cameraForward)
    {
        // Remove the axis component from the camera direction, then normalise
        Vector3 camFlat = cameraForward - Vector3.Project(cameraForward, axis);
        if (camFlat.sqrMagnitude < 0.0001f)
        {
            // Camera is looking straight along the axis - fall back
            camFlat = Vector3.up - Vector3.Project(Vector3.up, axis);
        }
        return camFlat.normalized;
    }
    
    private static Vector3 GetScaleVector(float multiplier)
    {
        switch (currentAxisFilter)
        {
            case AxisFilter.X: return new Vector3(multiplier, 1f, 1f);
            case AxisFilter.Y: return new Vector3(1f, multiplier, 1f);
            case AxisFilter.Z: return new Vector3(1f, 1f, multiplier);
            default: return Vector3.one * multiplier; // uniform scaling
        }
    }

    private static string GetAxisFilterText()
    {
        switch (currentAxisFilter)
        {
            case AxisFilter.X: return "X";
            case AxisFilter.Y: return "Y";
            case AxisFilter.Z: return "Z";
            case AxisFilter.YZ: return "Y, Z (exclude X)";
            case AxisFilter.XZ: return "X, Z (exclude Y)";
            case AxisFilter.XY: return "X, Y (exclude Z)";
            default: return "All";
        }
    }
}
