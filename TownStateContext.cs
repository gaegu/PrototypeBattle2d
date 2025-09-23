using System.Collections.Generic;
using UnityEngine;

public class TownStateContext
{
    public FlowState State { get; set; }
    public FlowState PreviousState { get; set; }
    public TownFlowModel Model { get; set; }
    public Dictionary<string, object> Parameters { get; set; }

    public TownStateContext()
    {
        Parameters = new Dictionary<string, object>();
    }

    public T GetParameter<T>(string key)
    {
        if (Parameters.TryGetValue(key, out var value))
            return (T)value;
        return default(T);
    }

    public void SetParameter(string key, object value)
    {
        Parameters[key] = value;
    }
}