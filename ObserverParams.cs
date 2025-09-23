using IronJade.Observer.Core;
using UnityEngine;


public class CutsceneParam : IObserverParam
{
    public bool IsShow { get; set; }
    public bool IsTownCutscene { get; set; }
    public bool IsTownRefresh { get; set; }
}

public class CinemachineTimelineParam : IObserverParam
{
    public string TimelineName { get; set; }
    public bool IsPlay { get; set; }
}

public class OverlayCameraParam : IObserverParam
{
    public GameCameraType CameraType { get; set; }
    public bool IsAdd { get; set; }
}