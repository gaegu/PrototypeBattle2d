#pragma warning disable CS1998
#if UNITY_EDITOR
using System.Collections.Generic;
using Cinemachine;
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.UI;

public class TownFreeCameraSupport : BaseUnit
{
    [SerializeField]
    private CinemachineVirtualCamera virtualCamera = null;

    [SerializeField]
    private Toggle freeMoveToggle = null;

    private float moveSpeed = 10f;
    private float boostMultiplier = 2f;
    private float lookSpeed = 2f;
    private float zoomSpeed = 5f;

    private float rotationX;
    private float rotationY;
    private bool freeMoveMode;
    private bool initialized;

    private readonly Dictionary<KeyCode, Vector3> movementKeys = new()
    {
        { KeyCode.W, Vector3.forward },
        { KeyCode.S, Vector3.back },
        { KeyCode.A, Vector3.left },
        { KeyCode.D, Vector3.right },
        { KeyCode.Space, Vector3.up },
        { KeyCode.LeftControl, Vector3.down }
    };

    public override async UniTask ShowAsync()
    {
        freeMoveMode = false;
        freeMoveToggle.isOn = false;
        freeMoveToggle.onValueChanged.AddListener(ApplyFreeMoveState);

        ApplyFreeMoveState(false);

        gameObject.SafeSetActive(true);
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !freeMoveMode)
            return;

        HandleMovement();

        if (Input.GetMouseButton(1))
        {
            ShowCursor(false);
            HandleRotation();
        }
        else
        {
            ShowCursor(true);
        }

        HandleZoom();
    }

    public void InitializeFreeMoveMode(bool enabled)
    {
        freeMoveMode = enabled;
        freeMoveToggle.isOn = enabled;

        virtualCamera.transform.position = new Vector3(0, 5, 0);
        virtualCamera.transform.rotation = Quaternion.identity;
        rotationX = virtualCamera.transform.eulerAngles.x;
        rotationY = virtualCamera.transform.eulerAngles.y;
    }

    private void ApplyFreeMoveState(bool enabled)
    {
        freeMoveMode = enabled;

        if (TownSceneManager.Instance == null)
            return;

        if (BackgroundSceneManager.Instance == null)
            return;

        if (enabled)
        {
            if (PlayerManager.Instance.MyPlayer.TownPlayer?.TownObject == null)
                return;

            virtualCamera.transform.position = CameraManager.Instance.GetBrainCamera().transform.position;
            virtualCamera.transform.rotation = CameraManager.Instance.GetBrainCamera().transform.rotation;
            rotationX = virtualCamera.transform.eulerAngles.x;
            rotationY = virtualCamera.transform.eulerAngles.y;
        }

        virtualCamera.Priority = enabled ? 99 : 0;

        // 입력 off
        TownSceneManager.Instance.SetActiveTownInput(!enabled);
        PlayerManager.Instance.MyPlayer.TownPlayer.TownObject.MoveData.SetMoveLock(enabled);

        // 뷰 off
        UIManager.Instance.ShowCanvas(CanvasType.View, !enabled);
        UIManager.Instance.ShowCanvas(CanvasType.ApplicationView, !enabled);

        // 타운 UI off
        TownSceneManager.Instance.ShowTown(!enabled);

        // 데코 off
        BackgroundSceneManager.Instance.EditorShowDeco(!enabled);

        MessageBoxManager.ShowToastMessage(enabled ? "Free Camera Mode : ON!" : "Free Camera Mode : OFF");
    }

    private void ShowCursor(bool value)
    {
        Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value;
    }

    private void HandleMovement()
    {
        Vector3 move = Vector3.zero;

        foreach (var key in movementKeys)
        {
            if (Input.GetKey(key.Key))
                move += virtualCamera.transform.TransformDirection(key.Value);
        }

        float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f;
        virtualCamera.transform.position += move.normalized * (moveSpeed * speedMultiplier * Time.deltaTime);
    }

    private void HandleRotation()
    {
        rotationX = Mathf.Clamp(rotationX - (Input.GetAxis("Mouse Y") * lookSpeed), -90f, 90f);
        rotationY += Input.GetAxis("Mouse X") * lookSpeed;
        virtualCamera.transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
            virtualCamera.m_Lens.FieldOfView = virtualCamera.m_Lens.FieldOfView - (scroll * zoomSpeed);
    }
}
#endif