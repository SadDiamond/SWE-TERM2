using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RubikCubeLoader : MonoBehaviour
{
    private const float CubeStep = 0.72f;
    private const float CubeScale = 0.64f;
    private static readonly Vector3 InitialEuler = new Vector3(18f, -28f, 8f);

    private readonly List<Renderer> cubeRenderers = new List<Renderer>();
    private readonly List<Color> cubeColors = new List<Color>();
    private readonly List<Transform> cubePieces = new List<Transform>();
    private readonly List<Vector3Int> cubeCoordinates = new List<Vector3Int>();

    private CanvasGroup canvasGroup;
    private RawImage preview;
    private Transform cubeRoot;
    private Camera cubeCamera;
    private RenderTexture cubeTexture;
    private Vector3 currentAxis;
    private Vector3 targetAxis;
    private float axisBlendTimer;
    private float axisBlendDuration;
    private float rotationSpeed;
    public void Configure(string textureName, string cameraName, string cubeName)
    {
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        preview = transform.Find("Preview")?.GetComponent<RawImage>();
        if (preview == null)
        {
            GameObject previewObject = new GameObject("Preview");
            previewObject.transform.SetParent(transform, false);
            RectTransform rect = previewObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            preview = previewObject.AddComponent<RawImage>();
        }

        preview.raycastTarget = false;
        preview.color = Color.white;

        cubeTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        cubeTexture.name = textureName;
        preview.texture = cubeTexture;

        GameObject cameraObject = new GameObject(cameraName);
        cameraObject.transform.SetParent(transform, false);
        cubeCamera = cameraObject.AddComponent<Camera>();
        cubeCamera.targetTexture = cubeTexture;
        cubeCamera.clearFlags = CameraClearFlags.SolidColor;
        cubeCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cubeCamera.cullingMask = 1 << 30;
        cubeCamera.fieldOfView = 32f;
        cubeCamera.nearClipPlane = 0.1f;
        cubeCamera.farClipPlane = 50f;

        cubeRoot = new GameObject(cubeName).transform;
        cubeRoot.SetParent(transform, false);
        cubeRoot.localPosition = new Vector3(10000f, 10000f, 10012f);
        cameraObject.transform.localPosition = new Vector3(10004.8f, 10004.2f, 10005.2f);
        cameraObject.transform.LookAt(cubeRoot.position);

        BuildCube();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
        if (cubeCamera != null)
            cubeCamera.enabled = visible;
    }

    public void SetThemeColors(Color frame, Color backdrop, Color halo)
    {
        // Retained for callers that theme older loader presentations. The minimal
        // loader deliberately renders only the neutral-grey cube.
    }

    public void ResetScrambled()
    {
        if (cubeRoot == null)
            return;

        cubeRoot.localRotation = Quaternion.Euler(InitialEuler);
        for (int i = 0; i < cubePieces.Count; i++)
        {
            Transform piece = cubePieces[i];
            Vector3Int coordinate = cubeCoordinates[i];
            if (piece == null)
                continue;

            piece.SetParent(cubeRoot, false);
            piece.localPosition = new Vector3(coordinate.x, coordinate.y, coordinate.z) * CubeStep;
            piece.localRotation = Quaternion.identity;
        }

        ApplySliceInstant(1, 1, 1);
        ApplySliceInstant(0, -1, -1);
        ApplySliceInstant(2, 1, 1);
        ApplySliceInstant(1, -1, -1);
        ApplySliceInstant(0, 1, 1);
        ResetDriftState();
    }

    public IEnumerator PlaySolveAndSpin()
    {
        if (cubeRoot == null)
            yield break;

        yield return PlaySolveSequence();

        while (gameObject.activeSelf)
        {
            StepDriftRotation(Time.unscaledDeltaTime);
            yield return null;
        }
    }

    public IEnumerator PlayLoopingSolveAndSpin()
    {
        if (cubeRoot == null)
            yield break;

        while (gameObject.activeSelf)
        {
            ResetScrambled();
            yield return PlaySolveSequence();
            float idleTime = 0f;
            while (gameObject.activeSelf && idleTime < 0.18f)
            {
                float dt = Time.unscaledDeltaTime;
                idleTime += dt;
                StepDriftRotation(dt);
                yield return null;
            }
        }
    }

    private IEnumerator PlaySolveSequence()
    {
        ResetDriftState();
        yield return SpinFor(0.16f);
        yield return RotateSlice(0, 1, -1, 0.28f);
        yield return RotateSlice(1, -1, 1, 0.28f);
        yield return RotateSlice(2, 1, -1, 0.28f);
        yield return RotateSlice(0, -1, 1, 0.28f);
        yield return RotateSlice(1, 1, -1, 0.28f);
    }

    private void BuildCube()
    {
        cubeRenderers.Clear();
        cubeColors.Clear();
        cubePieces.Clear();
        cubeCoordinates.Clear();

        Color solvedGrey = new Color(0.58f, 0.62f, 0.66f, 1f);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"Cube_{x}_{y}_{z}";
            piece.layer = 30;
            piece.transform.SetParent(cubeRoot, false);
            piece.transform.localPosition = new Vector3(x, y, z) * CubeStep;
            piece.transform.localScale = Vector3.one * CubeScale;

            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Material material = new Material(shader);
            material.color = solvedGrey;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", material.color * 0.35f);
            }

            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.material = material;
            cubeRenderers.Add(renderer);
            cubeColors.Add(material.color);
            cubePieces.Add(piece.transform);
            cubeCoordinates.Add(new Vector3Int(x, y, z));
        }
    }

    private IEnumerator RotateSlice(int axis, int layer, int direction, float duration)
    {
        GameObject pivotObject = new GameObject("CubeSlicePivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(cubeRoot, false);
        List<int> selected = new List<int>(9);

        for (int i = 0; i < cubePieces.Count; i++)
        {
            Vector3Int coordinate = cubeCoordinates[i];
            int value = axis == 0 ? coordinate.x : axis == 1 ? coordinate.y : coordinate.z;
            if (value != layer || cubePieces[i] == null)
                continue;

            selected.Add(i);
            cubePieces[i].SetParent(pivot, true);
        }

        Vector3 rotationAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            pivot.localRotation = Quaternion.AngleAxis(90f * direction * eased, rotationAxis);
            StepDriftRotation(dt);
            yield return null;
        }

        pivot.localRotation = Quaternion.AngleAxis(90f * direction, rotationAxis);
        for (int i = 0; i < selected.Count; i++)
        {
            int index = selected[i];
            Transform piece = cubePieces[index];
            piece.SetParent(cubeRoot, true);
            piece.localPosition = SnapCubeVector(piece.localPosition);
            piece.localRotation = SnapCubeRotation(piece.localRotation);
            cubeCoordinates[index] = Vector3Int.RoundToInt(piece.localPosition / CubeStep);
        }

        Destroy(pivotObject);
        yield return SpinFor(0.06f);
    }

    private IEnumerator SpinFor(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            StepDriftRotation(dt);
            yield return null;
        }
    }

    private void ApplySliceInstant(int axis, int layer, int direction)
    {
        Vector3 rotationAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Quaternion rotation = Quaternion.AngleAxis(90f * direction, rotationAxis);

        for (int i = 0; i < cubePieces.Count; i++)
        {
            Vector3Int coordinate = cubeCoordinates[i];
            int value = axis == 0 ? coordinate.x : axis == 1 ? coordinate.y : coordinate.z;
            if (value != layer || cubePieces[i] == null)
                continue;

            Transform piece = cubePieces[i];
            piece.localPosition = SnapCubeVector(rotation * piece.localPosition);
            piece.localRotation = SnapCubeRotation(rotation * piece.localRotation);
            cubeCoordinates[i] = Vector3Int.RoundToInt(piece.localPosition / CubeStep);
        }
    }

    private void ResetDriftState()
    {
        currentAxis = new Vector3(0.35f, 0.9f, 0.2f).normalized;
        targetAxis = currentAxis;
        axisBlendTimer = 0f;
        axisBlendDuration = 0f;
        rotationSpeed = 22f;
    }

    private void StepDriftRotation(float deltaTime)
    {
        if (cubeRoot == null || deltaTime <= 0f)
            return;

        if (axisBlendTimer >= axisBlendDuration)
        {
            currentAxis = targetAxis;
            targetAxis = Random.onUnitSphere;
            if (targetAxis.sqrMagnitude < 0.001f)
                targetAxis = Vector3.up;
            axisBlendTimer = 0f;
            axisBlendDuration = Random.Range(1.8f, 3.8f);
            rotationSpeed = Random.Range(16f, 28f);
        }

        axisBlendTimer += deltaTime;
        float axisT = axisBlendDuration <= 0.001f ? 1f : Mathf.Clamp01(axisBlendTimer / axisBlendDuration);
        Vector3 blendedAxis = Vector3.Slerp(currentAxis, targetAxis, axisT).normalized;
        if (blendedAxis.sqrMagnitude < 0.001f)
            blendedAxis = Vector3.up;

        cubeRoot.Rotate(blendedAxis, rotationSpeed * deltaTime, Space.World);
    }

    private static Vector3 SnapCubeVector(Vector3 value)
    {
        return new Vector3(
            Mathf.Round(value.x / CubeStep),
            Mathf.Round(value.y / CubeStep),
            Mathf.Round(value.z / CubeStep)) * CubeStep;
    }

    private static Quaternion SnapCubeRotation(Quaternion value)
    {
        Vector3 euler = value.eulerAngles;
        euler.x = Mathf.Round(euler.x / 90f) * 90f;
        euler.y = Mathf.Round(euler.y / 90f) * 90f;
        euler.z = Mathf.Round(euler.z / 90f) * 90f;
        return Quaternion.Euler(euler);
    }

    private void OnDestroy()
    {
        if (cubeTexture != null)
        {
            cubeTexture.Release();
            Destroy(cubeTexture);
        }
    }
}
