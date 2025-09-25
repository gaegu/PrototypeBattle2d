
using Cysharp.Threading.Tasks;
using IronJade.Observer.Core;
using UnityEngine;
using IronJade.UI.Core;
using Cinemachine;

[DisallowMultipleComponent]
[ExecuteInEditMode]
public class BackgroundSceneManagerNew : MonoBehaviour, IObserver
{
    private static BackgroundSceneManagerNew instance;
    public static BackgroundSceneManagerNew Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(BackgroundSceneManagerNew).Name;
                GameObject manager = GameObject.Find(className);

                if (manager != null)
                    instance = manager.GetComponent<BackgroundSceneManagerNew>();

                return instance;
            }

            return instance;
        }
    }

    private BackgroundSceneManagerNew() { }

    public FieldMapDefine FieldMapDefine => fieldMapDefine;
    public GameObject TownObjectParent { get => townObjects; }


    [SerializeField]
    [Header("타운씬 로드")]
    private bool isTownSceneUnLoad = false;


    [Header("필드맵")]
    [SerializeField]
    private FieldMapDefine fieldMapDefine;

    [Header("그룹")]
    [SerializeField]
    private GameObject townGroup = null;

    [Header("타운 오브젝트들")]
    [SerializeField]
    private BaseTownObjectSupport[] baseTownObjectSupports = null;


    [Header("배경 오브젝트")]
    [SerializeField]
    private GameObject environment = null;

    [Header("타운오브젝트")]
    [SerializeField]
    private GameObject townObjects = null;

    [Header("NPC Transfrom")]
    [SerializeField]
    private Transform fixedNpcObjects = null;

    [Header("데코레이터 공장")]
    [SerializeField]
    private TownDecoratorFactory townDecoratorFactory;

    [SerializeField]
    private bool isNetwork = true;

    [SerializeField]
    private Transform warpPointParent;


    [SerializeField]
    private CinemachineClearShot mainCinemachineClearShot = null;
    public CinemachineClearShot MainCinemachineClearShot => mainCinemachineClearShot;

    public virtual async UniTask ChangeViewAsync(UIType uIType, UIType prevUIType)
    {
        switch (uIType)
        {
            case UIType.HousingSimulationView:
            case UIType.StageDungeonView:
                {
                    ShowTownGroup(false);
                    CameraManager.Instance.SetActiveTownCameras(false);
                    break;
                }

            case UIType.CharacterIntroduceView:
            case UIType.CharacterCollectionDetailView:
            case UIType.CharacterCostumeView:
                {
                    ShowTownGroup(false);
                    break;
                }

            default:
                {
                    ShowTownGroup(true);
                    CameraManager.Instance.SetActiveTownCameras(true);
                    break;
                }
        }

        await UniTask.CompletedTask;
    }

    public virtual async UniTask ChangePopupAsync(UIType uIType, UIType prevViewUIType)
    {
        if (uIType == UIType.ToastMessagePopup)
            return;

        if (UIManager.Instance.CheckOpenCurrentView(UIType.LobbyView))
        {
            ShowTownGroup(true);
            CameraManager.Instance.SetActiveTownCameras(true);
        }
        else if (UIManager.Instance.CheckOpenCurrentView(UIType.HousingSimulationView))
        {
            ShowTownGroup(false);
            CameraManager.Instance.SetActiveTownCameras(false);
        }

        await UniTask.CompletedTask;
    }

    public void AddTownObjects(FieldMapDefine fieldMapEnumId)
    {
        TownObjectManager.Instance.SetEventWarpPointGet(GetWarpPoint);

        if (baseTownObjectSupports == null)
            return;

        for (int i = 0; i < baseTownObjectSupports.Length; ++i)
        {
            var townObject = baseTownObjectSupports[i];

            if (townObject == null)
                continue;

            TownObjectManager.Instance.AddTownObject(townObject.TownObjectType, townObject);
            TownObjectManager.Instance.SetTownNpcInfo(townObject, fieldMapEnumId);
        }
    }

    /// <summary>
    /// 데코레이터 공장을 가동한다.
    /// </summary>
    public void OperateTownDecoratorFactory()
    {
        if (townDecoratorFactory != null)
            TownObjectManager.Instance.SetDecoratorFactory(townDecoratorFactory.OnEventOperate, townDecoratorFactory.OnEventCancel);
    }


    /// <summary>
    /// 마을 그룹군을 활성/비활성 한다. (배경, NPC, Gimmick 등)
    /// </summary>
    public void ShowTownGroup(bool isActive)
    {
        townGroup?.SafeSetActive(isActive);

        //기본적으로 배경도 타운그룹이랑 따라간다.
        environment?.SafeSetActive(isActive);
        townObjects?.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을의 고정 배경을 활성/비활성 한다. (씬에 박아 놓고 사용하는 리소스들)
    /// </summary>
    public void ShowEnvironment(bool isActive)
    {
        environment?.SafeSetActive(isActive);
    }

    /// <summary>
    /// 마을 오브젝트들을 활성/비활성 한다.
    /// </summary>
    public void ShowTownObjects(bool isActive)
    {
        townObjects.SafeSetActive(isActive);
    }


    /// <summary>
    /// 마을 배경을 불러온다.
    /// </summary>
    public async UniTask LoadTownBackground(string path)
    {
        // 현재는 씬에 박혀있음
        // 추후에는 Field Table을 추가해서 상황별 맵을 불러와야 함
        // 기본 : 아무런 상황이 아닐 때
        // 이벤트 : 이벤트 기간 동안
        // 스토리 : 스토리 진행 여부에 따라서 달라질 수도 있을 듯
        await UniTask.CompletedTask;
    }

    public Transform GetWarpPoint(string key)
    {
        Transform objectParent = townObjects != null ? townObjects.transform : transform;

        if (warpPointParent == null)
            warpPointParent = objectParent.Find(StringDefine.TOWN_OBJECT_WARP_POINTS_PARENT);

        if (warpPointParent == null)
            warpPointParent = objectParent;

        return warpPointParent.SafeGetChildByName(key);
    }

    public bool CheckActiveTown()
    {
        return townGroup.SafeGetActiveSelf();
    }

    void IObserver.HandleMessage(System.Enum observerMessage, IObserverParam observerParam)
    {
        switch (observerMessage)
        {
            case CharacterObserverID.Change:
                {

                    break;
                }

            case TownObserverID.SfxState:
                {
                    BoolParam boolParam = (BoolParam)observerParam;

                    TownObjectManager.Instance.SoundMute(!boolParam.Value1);
                    break;
                }
        }
    }

    public void PlayCutsceneState(bool isShow, bool isTown)
    {
        if (isTown)
        {
            TownObjectManager.Instance.RefreshProcess().Forget();
        }
        else
        {
            gameObject.SafeSetActive(!isShow);

            PlayerManager.Instance.ShowMyTownPlayerGroup(!isShow);
        }
    }


    private void Awake()
    {
        instance = this;

        ObserverManager.AddObserver(CharacterObserverID.Change, this);
        ObserverManager.AddObserver(TownObserverID.SfxState, this);
    }

    private void OnDestroy()
    {
        ObserverManager.RemoveObserver(CharacterObserverID.Change, this);
        ObserverManager.RemoveObserver(TownObserverID.SfxState, this);
    }

}