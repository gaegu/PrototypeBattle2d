using Cysharp.Threading.Tasks;
using Cinemachine;
using IronJade.Camera.Core;
using IronJade.UI.Core;
using UnityEngine;

public class CameraServiceWrapper : ICameraService
{
    private CameraManager manager => CameraManager.Instance;

    public Transform TownCamera => manager?.TownCamera;
    public float RecomposerPan => manager?.RecomposerPan ?? 0f;

    public Camera GetCamera(GameCameraType type)
    {
        return manager?.GetCamera(type);
    }

    public void SetActiveTownCameras(bool isActive)
    {
        manager?.SetActiveTownCameras(isActive);
    }

    public void SetEnableBrain(bool isEnable)
    {
        manager?.SetEnableBrain(isEnable);
    }

    public CinemachineBrain GetBrainCamera()
    {
        return manager?.GetBrainCamera();
    }

    public void RestoreDofTarget()
    {
        manager?.RestoreDofTarget();
    }

    public void SetLiveVirtualCamera()
    {
        manager?.SetLiveVirtualCamera();
    }

    public async UniTask WaitCinemachineClearShotBlend()
    {
        if (manager != null)
            await manager.WaitCinemachineClearShotBlend();
    }

    public void SetTalkCamera(bool isTalk)
    {
        manager?.SetTalkCamera(isTalk);
    }

    public void ChangeVolumeType(VolumeType volumeType)
    {
        manager?.ChangeVolumeType(volumeType);
    }

    public void ChangeVolumeType(string scenePath, VolumeType defaultType)
    {
        manager?.ChangeVolumeType(scenePath, defaultType);
    }

    public void ChangeFreeLockVolume()
    {
        manager?.ChangeFreeLockVolume();
    }

    public void SetAdditivePrefabCameraState(UIType uiType)
    {
        manager?.SetAdditivePrefabCameraState(uiType);
    }

    public async UniTask ShowBackgroundRenderTexture(bool isShow)
    {
        if (manager != null)
            await manager.ShowBackgroundRenderTexture(isShow);
    }

    public void RestoreCharacterCameraCullingMask()
    {
        manager?.RestoreCharacterCameraCullingMask();
    }
}