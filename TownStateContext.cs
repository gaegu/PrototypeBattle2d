using System.Collections.Generic;

public class TownStateContext
{
    public TownFlowModel Model { get; set; }
    public FlowState State { get; set; } = FlowState.None;
    public FlowState PreviousState { get; set; } = FlowState.None;
    public Dictionary<string, object> Parameters { get; set; }

    // 상태 히스토리 추가
    private readonly Stack<FlowState> stateHistory;
    private const int MaxHistorySize = 10;

    public TownStateContext()
    {
        Parameters = new Dictionary<string, object>();
        stateHistory = new Stack<FlowState>(MaxHistorySize);
    }

    public T GetParameter<T>(string key)
    {
        if (Parameters != null && Parameters.TryGetValue(key, out object value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }
        }
        return default(T);
    }

    public void SetParameter(string key, object value)
    {
        if (Parameters == null)
            Parameters = new Dictionary<string, object>();

        Parameters[key] = value;
    }

    public bool HasParameter(string key)
    {
        return Parameters?.ContainsKey(key) ?? false;
    }

    public void ClearParameters()
    {
        Parameters?.Clear();
    }

    // 히스토리 관리
    public void PushStateHistory(FlowState state)
    {
        if (stateHistory.Count >= MaxHistorySize)
        {
            // 오래된 히스토리 제거
            var tempStack = new Stack<FlowState>(MaxHistorySize);
            for (int i = 0; i < MaxHistorySize - 1; i++)
            {
                if (stateHistory.Count > 0)
                    tempStack.Push(stateHistory.Pop());
            }

            stateHistory.Clear();
            while (tempStack.Count > 0)
            {
                stateHistory.Push(tempStack.Pop());
            }
        }

        stateHistory.Push(state);
    }

    public FlowState? PopStateHistory()
    {
        return stateHistory.Count > 0 ? stateHistory.Pop() : (FlowState?)null;
    }

    public void ClearHistory()
    {
        stateHistory.Clear();
    }
}