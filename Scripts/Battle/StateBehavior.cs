using System.Security.Cryptography;

public class StateBehaviour
{
    #region Coding rule : Property
    public bool IsPlay { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsEnded { get; private set; }
    public float CurrentTime { get; private set; }
    public float EndTime { get; private set; }
    #endregion Coding rule : Property

    #region Coding rule : Value
    private object[] parameters = null;
    private System.Action onEventStart = null;
    private System.Action<object[]> onEventProcess = null;
    private System.Action onEventEnd = null;
    private System.Func<object[], bool> onEventCheckComplete = null;
    #endregion Coding rule : Value

    #region Coding rule : Function
    public void SetPlay(bool isPlay)
    {
        IsPlay = isPlay;
    }

    public void SetParameters(params object[] parameters)
    {
        this.parameters = parameters;
    }

    public void SetEndTime(float endTime)
    {
        EndTime = endTime;
    }

    public void SetEventStart(System.Action onEvent)
    {
        onEventStart = onEvent;
    }

    public void SetEventProcess(System.Action<object[]> onEvent)
    {
        onEventProcess = onEvent;
    }

    public void SetEventEnd(System.Action onEvent)
    {
        onEventEnd = onEvent;
    }

    public void SetEventCheckComplete(System.Func<object[], bool> onEvent)
    {
        onEventCheckComplete = onEvent;
    }

    public void Start()
    {
        if (IsStarted)
            return;

        onEventStart?.Invoke();

        IsStarted = true;
    }

    public void Process(float deltaTime)
    {
        if (EndTime > 0)
            CurrentTime += deltaTime;

        onEventProcess?.Invoke(parameters);
    }

    public void End()
    {
        if (IsEnded)
            return;

        IsEnded = true;

        onEventEnd?.Invoke();
    }

    public bool CheckComplete()
    {
        if (EndTime > 0 && CurrentTime >= EndTime)
            return true;

        if (onEventCheckComplete == null)
            return false;

        return onEventCheckComplete.Invoke(parameters);
    }

    public void Reset()
    {
        CurrentTime = 0f;
        EndTime = 0f;
        IsPlay = false;
        IsStarted = false;
        IsEnded = false;

        onEventStart = null;
        onEventProcess = null;
        onEventEnd = null;
        onEventCheckComplete = null;

        parameters = null;
    }
    #endregion Coding rule : Function
}
