using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Timeline이 일시정지된 상태에서도 클릭을 감지하여 Timeline을 재개하는 컴포넌트
/// </summary>
public class ClickDetector : MonoBehaviour
{
    private PlayableDirector director;
    private System.Action onClickReceived;
    private bool isWaitingForClick = false;

    private void Start()
    {
        director = FindObjectOfType<PlayableDirector>();
    }

    private void Update()
    {
        if (isWaitingForClick && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            isWaitingForClick = false;
            
            if (director != null)
            {
                director.Resume();
            }
            
            onClickReceived?.Invoke();
            onClickReceived = null;
        }
    }

    public void StartWaitingForClick(System.Action onClickCallback)
    {
        isWaitingForClick = true;
        onClickReceived = onClickCallback;
    }

    public void StopWaitingForClick()
    {
        isWaitingForClick = false;
        onClickReceived = null;
    }
}
