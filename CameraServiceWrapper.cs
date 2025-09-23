using Cinemachine;
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;

public class CameraServiceWrapper : ICameraService
{
    private CameraManager manager => CameraManager.Instance;

    #region Camera Management

    public Transform TownCamera => manager?.TownCamera;

    public Camera GetCamera(GameCameraType type)
    {
        return manager?.GetCamera(type);
    }

    public void SetActiveTownCameras(bool isActive)
    {
        manager?.SetActiveTownCameras(isActive);
    }

    #endregion

    #region Brain

    public void SetEnableBrain(bool isEnable)
    {
        manager?.SetEnableBrain(isEnable);
    }

    public CinemachineBrain GetBrainCamera()
    {
        return manager?.GetBrainCamera();
    }

    #endregion

    #region DOF

    public void RestoreDofTarget()
    {
        manager?.RestoreDofTarget();
    }

    #endregion

    #region Cinemachine

    public void SetLiveVirtualCamera()
    {
        manager?.SetLiveVirtualCamera();
    }

    public async UniTask WaitCinemachineClearShotBlend()
    {
        if (manager != null)
            await manager.WaitCinemachineClearShotBlend();
    }

    #endregion

    #region Talk Camera

    public void SetTalkCamera(bool isTalk)
    {
        manager?.SetTalkCamera(isTalk);
    }

    #endregion

    #region Volume

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

    #endregion

    #region Additive Prefab

    public void SetAdditivePrefabCameraState(UIType uiType)
    {
        manager?.SetAdditivePrefabCameraState(uiType);
    }

    #endregion

    #region Properties

    public float RecomposerPan => manager.RecomposerPan;

    #endregion

    #region Render

    public async UniTask ShowBackgroundRenderTexture(bool isShow)
    {
        if (manager != null)
            await manager.ShowBackgroundRenderTexture(isShow);
    }

    public void RestoreCharacterCameraCullingMask()
    {
        manager?.RestoreCharacterCameraCullingMask();
    }

    #endregion
}