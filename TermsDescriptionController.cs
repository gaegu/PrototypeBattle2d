#pragma warning disable CS1998
using Cysharp.Threading.Tasks;
using IronJade.UI.Core;

public class TermsDescriptionController : BaseController
{
    public override bool IsPopup { get { return true; } }
    public override UIType UIType { get { return UIType.TermsDescriptionPopup; } }
    public override void SetModel() { SetModel(new TermsDescriptionPopupModel()); }
    private TermsDescriptionPopup View { get { return base.BaseView as TermsDescriptionPopup; } }
    private TermsDescriptionPopupModel Model { get { return GetModel<TermsDescriptionPopupModel>(); } }

    #region Coding rule : Property
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
        return StringDefine.PATH_PREFAB_UI_CONTENTS + "TermsDescriptionPopup";
    }
    #endregion Coding rule : Function
}