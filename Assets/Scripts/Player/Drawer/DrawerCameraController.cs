using Unity.Cinemachine;
using UnityEngine;
using Photon.Pun;
using Allan;

/// <summary>
/// Controls only the local Drawer's view. The camera target and mode are never networked;
/// drawn strokes continue to send their existing world-space positions through DrawMesh.
/// </summary>
public class DrawerCameraController : MonoBehaviourPun
{
    private LevelSetup levelSetup;
    private CinemachineCamera virtualCamera;
    private Camera outputCamera;
    private Collider2D cameraBound;
    private Transform runnerTarget;
    private Transform localTarget;
    private float panSpeed;
    private bool freeMode;
    private bool strokeLocked;
    public void Initialize(LevelSetup setup, CinemachineCamera camera, Camera output, Collider2D bound,
        Transform runner, float speed)
    {
        levelSetup = setup;
        virtualCamera = camera;
        outputCamera = output;
        cameraBound = bound;
        runnerTarget = runner;
        panSpeed = Mathf.Max(0f, speed);

        // This target exists only on the Drawer's client and is never a Photon object.
        GameObject targetObject = new GameObject("Drawer Camera Target (Local)");
        localTarget = targetObject.transform;
        localTarget.position = CameraCenterAsTarget();
    }

    public void SetRunnerTarget(Transform runner)
    {
        runnerTarget = runner;
        if (!freeMode && !strokeLocked && virtualCamera != null)
        {
            virtualCamera.Follow = runnerTarget;
            virtualCamera.PreviousStateIsValid = false;
        }
    }

    private void Update()
    {
        if (EgakuSettingsMenu.IsOpen) return;
        if (virtualCamera == null || outputCamera == null || cameraBound == null || localTarget == null)
            return;

        // The component is created only for the local Drawer. Recheck ownership in case
        // Photon changes it during a reconnect, rather than letting a stale copy read input.
        if (!photonView.IsMine)
        {
            if (freeMode || strokeLocked)
                ReturnToRunner();
            return;
        }

        // Recovery must not retain a distant local camera after gameplay input resumes.
        if (GameManager.InteractionsPausedForRecovery)
        {
            if (freeMode || strokeLocked)
                ReturnToRunner();
            return;
        }

        // Ignore the camera toggle during either device's held stroke. The release
        // frame belongs to that stroke; a fresh press is required to switch later.
        if (GameplayInput.DrawHeld || GameplayInput.DrawReleased)
        {
            if (!strokeLocked)
            {
                strokeLocked = true;
                // Capture the rendered pose even in free mode: the Cinemachine composer may
                // still be catching up to the moving target when the stroke begins.
                localTarget.position = CameraCenterAsTarget();
                virtualCamera.Follow = localTarget;
            }
            // Clear any remaining composer/Confiner damping so Runner motion cannot
            // move the picture beneath a held brush or eraser.
            virtualCamera.PreviousStateIsValid = false;
            return;
        }

        if (strokeLocked)
        {
            strokeLocked = false;
            if (!freeMode)
                virtualCamera.Follow = runnerTarget;
        }

        if (GameplayInput.CameraTogglePressed)
        {
            if (freeMode)
                ReturnToRunner();
            else if (runnerTarget != null)
            {
                // Start from the live output pose, not the Runner's position or the last
                // free target, so entering free view never jumps to a stale location.
                localTarget.position = ClampTarget(CameraCenterAsTarget());
                freeMode = true;
                virtualCamera.Follow = localTarget;
                virtualCamera.PreviousStateIsValid = false;
            }
            return;
        }

        if (!freeMode)
            return;

        // Only this local Drawer reads the assigned device. In local co-op the
        // other device stays with Runner even while this camera is free.
        Vector2 input = GameplayInput.CameraMove;
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Clamp the target itself as well as the rendered camera. Otherwise it could
        // travel far outside the Confiner and feel stuck when the player turns around.
        localTarget.position = ClampTarget(localTarget.position + (Vector3)(input * panSpeed * Time.deltaTime));
    }

    public bool CanUsePointer(Vector3 worldPosition)
    {
        // Follow mode keeps its existing input rules. Free mode draws only in the
        // visible game viewport and inside the level's authored camera boundary.
        return !freeMode || (outputCamera != null && cameraBound != null &&
                             outputCamera.pixelRect.Contains(GameplayInput.PointerScreenPosition(false)) &&
                             cameraBound.OverlapPoint(worldPosition));
    }

    public bool CanEraseAt(Vector2 worldPosition)
    {
        if (!freeMode)
            return true;
        if (cameraBound == null || outputCamera == null || !cameraBound.OverlapPoint(worldPosition))
            return false;

        // A drag may start outside the game window and return inside. Check each sampled
        // eraser point, not only the current cursor, so hidden offscreen objects stay untouched.
        Vector3 screenPosition = outputCamera.WorldToScreenPoint(worldPosition);
        return screenPosition.z > 0f && outputCamera.pixelRect.Contains(screenPosition);
    }

    private Vector3 CameraCenterAsTarget()
    {
        Vector3 cameraPosition = outputCamera.transform.position;
        return new Vector3(cameraPosition.x, cameraPosition.y, runnerTarget != null ? runnerTarget.position.z : 0f);
    }

    private Vector3 ClampTarget(Vector3 position)
    {
        Bounds bounds = cameraBound.bounds;
        float halfHeight = outputCamera.orthographicSize;
        float halfWidth = halfHeight * outputCamera.aspect;

        // Some authored levels are narrower than a 16:9 viewport. No camera center
        // can contain that entire viewport, so center that axis without changing zoom.
        position.x = ClampAxis(position.x, bounds.min.x, bounds.max.x, halfWidth);
        position.y = ClampAxis(position.y, bounds.min.y, bounds.max.y, halfHeight);
        return position;
    }

    private static float ClampAxis(float value, float minimum, float maximum, float halfViewSize)
    {
        float allowedMinimum = minimum + halfViewSize;
        float allowedMaximum = maximum - halfViewSize;
        return allowedMinimum <= allowedMaximum
            ? Mathf.Clamp(value, allowedMinimum, allowedMaximum)
            : (minimum + maximum) * 0.5f;
    }

    private void ReturnToRunner()
    {
        freeMode = false;
        strokeLocked = false;
        virtualCamera.Follow = runnerTarget;
        virtualCamera.PreviousStateIsValid = false;
    }

    private void OnDestroy()
    {
        if (levelSetup != null)
            levelSetup.UnregisterDrawerCamera(this);
        if (virtualCamera != null && runnerTarget != null)
            virtualCamera.Follow = runnerTarget;
        if (localTarget != null)
            Destroy(localTarget.gameObject);
    }
}
