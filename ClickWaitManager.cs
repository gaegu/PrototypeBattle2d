using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline이 일시정지된 상태에서도 클릭을 감지하여 Timeline을 재개하는 매니저
/// </summary>
public class ClickWaitManager : MonoBehaviour
{
    private static ClickWaitManager _instance;
    public static ClickWaitManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ClickWaitManager");
                _instance = go.AddComponent<ClickWaitManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private PlayableDirector director;
    private System.Action onClickReceived;
    private bool isWaitingForClick = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        director = FindObjectOfType<PlayableDirector>();
    }

    private void Update()
    {
        if (isWaitingForClick && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            Debug.Log("[ClickWaitManager] Update에서 클릭 감지됨! Timeline 재개 시도...");
            Debug.Log($"[ClickWaitManager] Director 상태: {director?.state}");
            Debug.Log($"[ClickWaitManager] Director null 여부: {director == null}");
            
            isWaitingForClick = false;
            
            if (director != null)
            {
                director.Resume();
                Debug.Log($"[ClickWaitManager] Resume 후 Director 상태: {director.state}");
            }
            else
            {
                Debug.LogError("[ClickWaitManager] Director가 null입니다!");
            }
            
            onClickReceived?.Invoke();
            onClickReceived = null;
        }
    }

    private void OnGUI()
    {
        // Timeline이 일시정지된 상태에서도 클릭을 감지하기 위해 OnGUI 사용
        if (isWaitingForClick && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Debug.Log("[ClickWaitManager] OnGUI에서 클릭 감지됨!");
            Debug.Log($"[ClickWaitManager] Director 상태: {director?.state}");
            Debug.Log($"[ClickWaitManager] Director null 여부: {director == null}");
            
            isWaitingForClick = false;
            
            if (director != null)
            {
                director.Resume();
                Debug.Log($"[ClickWaitManager] Resume 후 Director 상태: {director.state}");
            }
            else
            {
                Debug.LogError("[ClickWaitManager] Director가 null입니다!");
            }
            
            onClickReceived?.Invoke();
            onClickReceived = null;
        }
    }

    public void StartWaitingForClick(System.Action onClickCallback)
    {
        isWaitingForClick = true;
        onClickReceived = onClickCallback;
        Debug.Log("[ClickWaitManager] 클릭 대기 시작");
    }

    public void StopWaitingForClick()
    {
        isWaitingForClick = false;
        onClickReceived = null;
        Debug.Log("[ClickWaitManager] 클릭 대기 중지");
    }
}
