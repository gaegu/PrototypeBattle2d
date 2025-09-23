//=========================================================================================================
#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;
//=========================================================================================================

public class ProbabilityInfoController : BaseController
{
    #region Coding rule : Property
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.ProbabilityInfoPopup; } }
    private ProbabilityInfoPopup View { get { return base.BaseView as ProbabilityInfoPopup; } }
    protected ProbabilityInfoPopupModel Model { get; private set; }
    public ProbabilityInfoController() { Model = GetModel<ProbabilityInfoPopupModel>(); }
    public ProbabilityInfoController(BaseModel baseModel) : base(baseModel) { }
    #endregion Coding rule : Property

    #region Coding rule : Value
    #endregion Coding rule : Value

    #region Coding rule : Function
    public override void Enter()
    {
    }

    public override async UniTask LoadingProcess()
    {

    }

    public override async UniTask Process()
    {
        await View.ShowAsync();
    }

    public override void Refresh()
    {
    }

    public override string GetUIPrefabName()
    {
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "Common/ProbabilityInfoPopup";
    }
    #endregion Coding rule : Function
}