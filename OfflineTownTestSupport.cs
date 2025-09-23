#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OfflineTownTestSupport : BaseUnit
{
    public enum Mode
    {
        Town,
        Battle,
    }

    public string StartScenePath => startScenePath; 

    public string[] BattleScenePaths => battleScenePaths;

    public Mode CurrentMode => currentMode;

    public bool EnableSound => enablSound;

    [HideInInspector]
    [SerializeField]
    private string startScenePath;

    [SerializeField]
    private Mode currentMode;

    [SerializeField]
    private string[] battleScenePaths = new string[0];

    [SerializeField]
    private bool enablSound = false;

    public void SetStartScene(string value)
    {
        startScenePath = value;
    }

    public void SetBattleScenePaths(string[] battleScenePaths)
    {
        this.battleScenePaths = battleScenePaths;
    }

    public void SetCurrentMode(Mode mode)
    {
        currentMode = mode;
    }

    private void Awake()
    {
        OfflineTownTest().Forget();
    }

    private async UniTask OfflineTownTest()
    {
        if (currentMode == Mode.Town)
        {
            Scene scene = await UtilModel.Resources.LoadSceneAsync($"Background/{startScenePath}", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }
        else
        {
            await UtilModel.Resources.InstantiateAsync<GameObject>($"Background/Prefab/Battle/{startScenePath}", Vector3.zero, Quaternion.identity);
        }

        GameObject testModeGameManager = await UtilModel.Resources.InstantiateAsync<GameObject>(StringDefine.PATH_TEST_MODE_GAME_MANAGER, null);
        DontDestroyOnLoad(testModeGameManager);

        TransitionManager.ShowGlitch(true);

        if (currentMode == Mode.Town)
        {
            await BackgroundSceneManager.Instance.TestGamePlay(false);
        }
        else
        {
            CameraManager.Instance.SetActiveTownCameras(true);
            CameraManager.Instance.SetActiveCamera(GameCameraType.TownCharacter, true);
        }

        TransitionManager.ShowGlitch(false);
    }
}
#endif
