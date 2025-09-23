using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using IronJade.Table.Data;
using UnityEngine;

public class PhoneCallCutscene : BaseCutscene
{
    [SerializeField]
    private ResourcesLoader phoneCallUnitLoader = null;

    private PhoneCallPopupUnit phoneCallPopupUnit = null;

    public override UniTask OnLoadCutscene()
    {
        return base.OnLoadCutscene();
    }

    public override void OnNotifyTimeLine(CutsceneTimeLineEvent timeLineState, string key)
    {

    }

    public override async UniTask OnStartCutscene()
    {
        await LoadPhoneCallUnit();

        SetPhoneCallPopupUnitModel();
        await phoneCallPopupUnit.ShowAsync();
    }

    private async UniTask LoadPhoneCallUnit()
    {
        if (phoneCallPopupUnit == null)
            phoneCallPopupUnit = await phoneCallUnitLoader.LoadAsync<PhoneCallPopupUnit>();
    }

    private void SetPhoneCallPopupUnitModel()
    {
        ThumbnailGeneratorModel thumbnailGenerator = new ThumbnailGeneratorModel(PlayerManager.Instance.MyPlayer.User);
        ScriptConvertModel convertModel = new ScriptConvertModel();
        PhoneCallPopupUnitModel phoneCallmodel = new PhoneCallPopupUnitModel();

        ThumbnailCharacterUnitModel beatModel = thumbnailGenerator.GetCharacterUnitModelByDataId((int)CharacterDefine.CHARACTER_BEAT);
        ThumbnailCharacterUnitModel cooperModel = thumbnailGenerator.GetCharacterUnitModelByDataId((int)CharacterDefine.CHARACTER_JOHNCOOPER);
        string text = TableManager.Instance.GetLocalization("LOCALIZATION_PROLOGUE_PHONE");
        PhoneCallScript[] scripts = convertModel.GetScripts<PhoneCallScript>(text);

        phoneCallmodel.SetCallerThumbModel(cooperModel);
        phoneCallmodel.SetRecevierThumbModel(beatModel);

        for (int i = 0; i < scripts.Length; i++)
        {
            CharacterDefine characterDefine = Enum.IsDefined(typeof(CharacterDefine), scripts[i].character) ?
                (CharacterDefine)Enum.Parse(typeof(CharacterDefine), scripts[i].character) :
                CharacterDefine.None;

            string message = scripts[i].text;

            CharacterTalkUnitModel model = new CharacterTalkUnitModel();

            ThumbnailCharacterUnitModel thumbModel = characterDefine == CharacterDefine.None ? beatModel : cooperModel;
            //model.SetAnchor(i % 2 > 0 ? CharacterTalkUnitModel.AnchorType.Right : CharacterTalkUnitModel.AnchorType.Left);
            model.SetThumbnailCharacterModel(thumbModel);
            model.SetTalkText(message);

            phoneCallmodel.AddCharacterTalkUnitModel(model);
        }

        phoneCallPopupUnit.SetModel(phoneCallmodel);
    }
}
