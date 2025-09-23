#if CHEAT

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Cinemachine.PostFX;
using Cysharp.Threading.Tasks;
using Sentry;
using SRDebugger;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public partial class SROptions
{
    private const string OUTLINE_RENDERER_FEATURE_NAME = "Outline Pass";
    private const string GRAB_RENDERER_FEATURE_NAME = "GrabScreenFeature";
    private const string BEAUTIFY_RENDERER_FEATURE_NAME = "Beautify";
    private const string DECAL_RENDERER_FEATURE_NAME = "DecalRendererFeature";
    private const string BLUR_RENDERER_FEATURE_NAME = "BlurRendererFeature";

    private const string CATEGORY_NAME_00 = "[12. Setting]";
    private const string CATEGORY_NAME_01 = "[01. Battle Debug]";
    private const string CATEGORY_NAME_02 = "[02. Dead & Damage]";
    private const string CATEGORY_NAME_03 = "[03. Round & Match]";
    private const string CATEGORY_NAME_04 = "[04. Game]";
    private const string CATEGORY_NAME_05 = "[05. Graphics]";
    private const string CATEGORY_NAME_06 = "[06. Battle Effect]";
    private const string CATEGORY_NAME_07 = "[07. Sound]";
    private const string CATEGORY_NAME_08 = "[08. Reset]";
    private const string CATEGORY_NAME_09 = "[09. URP]";
    private const string CATEGORY_NAME_10 = "[10. DeviceRenderQuality]";
    private const string CATEGORY_NAME_11 = "[11. Debug]";

    public enum CheatValues
    {
        On,
        Off,
    }

    public enum TimeScales
    {
        x0,
        x1,
        x2,
        x4,
        x8
    }

    #region Coding Rule: Value
    //private int frameRate = 60;
    private CheatValues volume = CheatValues.On;
    private CheatValues beautifyVolume = CheatValues.On;
    private CheatValues effectRenderOnOff = CheatValues.On;
    private CheatValues effectLoadOnOff = CheatValues.On;
    private CheatValues effectActiveOnOff = CheatValues.On;
    private CheatValues effectHitOnOff = CheatValues.On;
    private CheatValues effectAttackOnOff = CheatValues.On;
    private CheatValues timeScaleOnOff = CheatValues.On;
    private CheatValues effectVolumeOnOff = CheatValues.On;
    private CheatValues cutSceneInteractionSuccessMode = CheatValues.On;
    private CheatValues fmodActiveState = CheatValues.On;
    private CheatValues isDamageCheatOn = CheatValues.Off;
    private CheatValues isBattleCharacterDebugInfoOn = CheatValues.Off;
    private CheatValues isBattleCharacterDebugQueueInfoOn = CheatValues.Off;
    private CheatValues isBattleLineDebugInfoOn = CheatValues.Off;
    private float timeScale = 1f;
    private bool forceHorizontalMode = false;
    private bool dofOnOff = true;
    #endregion

    #region [0. Setting]
    [Category(CATEGORY_NAME_00)]
    public void RotateScreen()
    {
        if (GameSettingManager.Instance.GraphicSettingModel.GetViewModeType() == SettingViewModeType.Horizontal)
            GameSettingManager.Instance.GraphicSettingModel.ViewMode((int)SettingViewModeType.Vertical);
        else
            GameSettingManager.Instance.GraphicSettingModel.ViewMode((int)SettingViewModeType.Horizontal);
    }

    [Category(CATEGORY_NAME_00)]
    public void SelectLanguage()
    {
        if (GameSettingManager.Instance.GetLanguageType() == IronJade.Table.Data.Localization.LanguageType.Eng)
            GameSettingManager.Instance.SaveLanguageOption(SettingLanguageType.Korean, SettingLanguageType.Korean);
        else
            GameSettingManager.Instance.SaveLanguageOption(SettingLanguageType.English, SettingLanguageType.English);

        Application.Quit();
    }
    #endregion

    #region [1. Battle Debug]
    [Category(CATEGORY_NAME_01), DisplayName("데미지치트"), Sort(11)]
    public CheatValues DamageCheat
    {
        get { return isDamageCheatOn; }
        set
        {
            isDamageCheatOn = value;
            bool isOn = isDamageCheatOn == CheatValues.On;
            CheatManager.Instance.IsDamageCheatOn = isOn;
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("전투 캐릭터 큐 정보 표시"), Sort(16)]
    public CheatValues BattleCharacterDebugQueueInfo
    {
        get { return isBattleCharacterDebugQueueInfoOn; }
        set
        {
            isBattleCharacterDebugQueueInfoOn = value;
            bool isOn = isBattleCharacterDebugQueueInfoOn == CheatValues.On;
            CheatManager.Instance.IsBattleCharacterDebugQueueInfoOn = isOn;
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("전투 캐릭터 디버그 정보 표시"), Sort(18)]
    public CheatValues BattleCharacterDebugInfo
    {
        get { return isBattleCharacterDebugInfoOn; }
        set
        {
            isBattleCharacterDebugInfoOn = value;
            bool isOn = isBattleCharacterDebugInfoOn == CheatValues.On;
            CheatManager.Instance.IsBattleCharacterDebugInfoOn = isOn;
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("전투 디버그 라인 정보 표시"), Sort(17)]
    public CheatValues BattleLineDebugInfo
    {
        get { return isBattleLineDebugInfoOn; }
        set
        {
            isBattleLineDebugInfoOn = value;
            bool isOn = isBattleLineDebugInfoOn == CheatValues.On;
            CheatManager.Instance.IsBattleLineDebugInfoOn = isOn;
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("보스 처형스킬 발동"), Sort(1)]
    public async void PlayBossExecutionSkillTimeline()
    {
        await BattleProcessManager.Instance.OnPlayExecutionSkillTimeLine(UFE.Instance.GetBossControlsScript(UFE_TeamSide.Enemy, false));
    }

    [Category(CATEGORY_NAME_01), DisplayName("디버그 공격속도"), Sort(19)]
    public bool DebugAttackSpeed { get { return CheatManager.Instance.IsOnDebugAttackSpeed; } set { CheatManager.Instance.IsOnDebugAttackSpeed = value; } }
    [Category(CATEGORY_NAME_01), DisplayName("디버그 큐"), Sort(20)]
    public bool DebugQueue { get { return CheatManager.Instance.IsOnDebugQueue; } set { CheatManager.Instance.IsOnDebugQueue = value; } }
    [Category(CATEGORY_NAME_01), DisplayName("디버그 슬롯"), Sort(21)]
    public bool DebugPosition { get { return CheatManager.Instance.IsOnDebugPosition; } set { CheatManager.Instance.IsOnDebugPosition = value; } }
    [Category(CATEGORY_NAME_01), DisplayName("디버그 포메이션"), Sort(22)]
    public bool DebugFormation { get { return CheatManager.Instance.IsOnDebugFormation; } set { CheatManager.Instance.IsOnDebugFormation = value; } }
    [Category(CATEGORY_NAME_01), DisplayName("디버그 토큰"), Sort(23)]
    public bool DebugToken { get { return CheatManager.Instance.IsOnDebugToken; } set { CheatManager.Instance.IsOnDebugToken = value; } }

    [Category(CATEGORY_NAME_01), DisplayName("아군 크리티컬")]
    public CheatValues Critical
    {
        get
        {
            if (CheatManager.Instance.IsCriticalOn)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsCriticalOn != isOn)
                CheatManager.Instance.OnClickCriticalOnOff();
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("데미지 0")]
    public CheatValues IgnoreDamage
    {
        get
        {
            if (CheatManager.Instance.IsIgnoreDamage)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsIgnoreDamage != isOn)
                CheatManager.Instance.OnClickIgnoreDamageOnOff();
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("아군 무적")]
    public CheatValues PowerOverwhelming
    {
        get
        {
            if (CheatManager.Instance.IsPowerOverwhelming)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsPowerOverwhelming != isOn)
                CheatManager.Instance.OnClickPowerOverwhelmingOnOff();
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("적군 전체 체력 10%"), Sort(5)]
    public void EnemyCharacterHpDecrease()
    {
        List<ControlsScript> enemyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Enemy);
        foreach (var controlsScript in enemyControlsScripts)
        {
            controlsScript.DamageMe(controlsScript.HP * 0.9, null, null, null);
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("적군 전체에 10% 데미지"), Sort(6)]
    public void EnemyCharacterGetDamage()
    {
        List<ControlsScript> enemyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Enemy);
        foreach (var controlsScript in enemyControlsScripts)
        {
            if (controlsScript.HP > controlsScript.MaxHP * 0.1)
                controlsScript.DamageMe(controlsScript.MaxHP * 0.1, null, null, null);
            else
                controlsScript.DamageMe(controlsScript.HP - 1, null, null, null);
        }
    }

    [Category(CATEGORY_NAME_01), DisplayName("적군 전체 회복"), Sort(7)]
    public void EnemyCharacterHpMax()
    {
        List<ControlsScript> enemyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Enemy);
        foreach (var controlsScript in enemyControlsScripts)
        {
            controlsScript.BattleCharInfo.AddHp(controlsScript.BattleCharInfo.MaxHp);
        }
    }

    private void AddEnemyToken(int token)
    {
        List<ControlsScript> enemys = UFE.Instance.GetControlsScriptTeam(UFE_TeamSide.Enemy);
        foreach (ControlsScript enemy in enemys)
        {
            enemy.BattleCharInfo.TokenInfo.AddToken(token);
        }
        UFE.Instance.SetEnemySlotUnits();
    }

    [Category(CATEGORY_NAME_01), DisplayName("적 토큰1 추가"), Sort(0)]
    public void AddEnemyTokenOne()
    {
        AddEnemyToken(1);
    }

    [Category(CATEGORY_NAME_01), DisplayName("적 토큰2 추가"), Sort(0)]
    public void AddEnemyTokenTwo()
    {
        AddEnemyToken(2);
    }

    [Category(CATEGORY_NAME_01), DisplayName("적 토큰3 추가"), Sort(0)]
    public void AddEnemyTokenThree()
    {
        AddEnemyToken(3);
    }

    [Category(CATEGORY_NAME_01), DisplayName("피버")]
    public void Fever()
    {
        var tempOpScript = UFE.Instance.GetControlsScriptTeam(UFE_TeamSide.Ally);
        for (int i = 0; i < tempOpScript.Count; ++i)
        {
            if (!tempOpScript[i].IsMainCharacter)
                continue;

            BattleProcessManager.Instance.AddFeverGaugeValue(9999999);
            tempOpScript[i].FeverStart().Forget();
            BattleProcessManager.Instance.PlayerFeverGaugeInfo.ClearAllFeverGauge(false);
            break;
        }
    }


    [Category(CATEGORY_NAME_01), DisplayName("피버게이지허용")]
    public bool IsFeverGaugeFillingAllowed
    {
        get
        {
            if (BattleProcessManager.Instance == null)
            {
                return false;
            }

            return BattleProcessManager.Instance.IsFeverGaugeFillingAllowed;
        }

        set
        {
            if (BattleProcessManager.Instance != null)
            {
                BattleProcessManager.Instance.SetFeverGaugeFillingAllowed(value);
            }
        }
    }
    #endregion

    #region [2. Dead & Damage]
    [Category(CATEGORY_NAME_02), DisplayName("아군 1번 사망"), Sort(0)]
    public void AllyFirstCharacterDead()
    {
        AllyCharacterDead(0);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 2번 사망"), Sort(0)]
    public void AllySecondCharacterDead()
    {
        AllyCharacterDead(1);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 3번 사망"), Sort(0)]
    public void AllyThirdCharacterDead()
    {
        AllyCharacterDead(2);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 4번 사망"), Sort(0)]
    public void AllyFourthCharacterDead()
    {
        AllyCharacterDead(3);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 5번 사망"), Sort(0)]
    public void AllyFifthCharacterDead()
    {
        AllyCharacterDead(4);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 1번 10% 데미지"), Sort(1)]
    public void AllyFirstCharacterDamaged()
    {
        AllyCharacterDamaged(0);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 2번 10% 데미지"), Sort(1)]
    public void AllySecondCharacterDamaged()
    {
        AllyCharacterDamaged(1);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 3번 10% 데미지"), Sort(1)]
    public void AllyThirdCharacterDamaged()
    {
        AllyCharacterDamaged(2);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 4번 10% 데미지"), Sort(1)]
    public void AllyFourthCharacterDamaged()
    {
        AllyCharacterDamaged(3);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 5번 10% 데미지"), Sort(1)]
    public void AllyFifthCharacterDamaged()
    {
        AllyCharacterDamaged(4);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 메인 체력 10%"), Sort(2)]
    public void MainCharacterHpDecrease()
    {
        UFE.Instance.P1ControlsScript.DamageMe(UFE.Instance.P1ControlsScript.HP * 0.9, null, null, null);
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 전체 체력 10%"), Sort(4)]
    public void AllyCharacterHpDecrease()
    {
        List<ControlsScript> allyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Ally);
        foreach (var controlsScript in allyControlsScripts)
        {
            controlsScript.DamageMe(controlsScript.HP * 0.9, null, null, null);
        }
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 전체 회복"), Sort(5)]
    public void AllyCharacterHpMax()
    {
        List<ControlsScript> allyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Ally);
        foreach (var controlsScript in allyControlsScripts)
        {
            controlsScript.BattleCharInfo.AddHp(controlsScript.BattleCharInfo.MaxHp);
        }
    }

    [Category(CATEGORY_NAME_02), DisplayName("아군 서브 체력 10%"), Sort(3)]
    public void SubCharacterHpDecrease()
    {
        List<ControlsScript> allyControlsScripts = UFE.Instance.GetOnStageAliveControlsScriptByTeam(UFE_TeamSide.Ally);
        foreach (var controlsScript in allyControlsScripts)
        {
            if (controlsScript == UFE.Instance.P1ControlsScript)
                continue;

            controlsScript.DamageMe(controlsScript.HP * 0.9, null, null, null);
        }
    }

    private void AllyCharacterDead(int slotIndex)
    {
        ControlsScript controlsScript = UFE.Instance.GetControlsScriptbySlotIndex(UFE_TeamSide.Ally, slotIndex);
        controlsScript.SetDeadByDamageSkillEvent(controlsScript, controlsScript.MaxHP, true);
    }

    private void AllyCharacterDamaged(int slotIndex)
    {
        ControlsScript controlsScript = UFE.Instance.GetControlsScriptbySlotIndex(UFE_TeamSide.Ally, slotIndex);
        if (controlsScript.HP > controlsScript.MaxHP * 0.1)
            controlsScript.DamageMe(controlsScript.MaxHP * 0.1, null, null, null);
        else
            controlsScript.DamageMe(controlsScript.HP - 1, null, null, null);
    }
    #endregion

    #region [3. Round & Match]
    [Category(CATEGORY_NAME_03), DisplayName("웨이브 승리")]
    public void VictoryRound()
    {
        CheatManager.Instance.Command_VitoryRound();
    }

    [Category(CATEGORY_NAME_03), DisplayName("던전 승리")]
    public void VictoryMatch()
    {
        CheatManager.Instance.Command_VitoryMatch();
    }

    [Category(CATEGORY_NAME_03), DisplayName("웨이브 패배")]
    public void DefeatRound()
    {
        CheatManager.Instance.Command_DefeatRound();
    }

    [Category(CATEGORY_NAME_03), DisplayName("던전 패배")]
    public void DefeatMatch()
    {
        CheatManager.Instance.Command_DefeatMatch();
    }

    [Category(CATEGORY_NAME_03), DisplayName("던전마스터 던전 선택")]
    public void ChainDungeonSelect()
    {
        if (UFE.Instance == null || !UFE.Instance.GameRunning)
            return;

        UFE.Instance.FireGameEndsByChaindungeonCheat(UFE_TeamSide.Enemy);
        CheatManager.Instance.Command_ChainDungeonSelect();
    }

    [Category(CATEGORY_NAME_03), DisplayName("던전마스터 진행도")]
    public void DungeonMasterProgress()
    {
        CheatManager.Instance.Command_ShowDungeonMasterProgress();
    }
    #endregion

    #region [4. Game]
    [Category(CATEGORY_NAME_04), DisplayName("화면회전대기")]
    public bool UseMonitorOrientation
    {
        get => GameSettingManager.Instance.UseMonitorOrientation;
        set => GameSettingManager.Instance.UseMonitorOrientation = value;
    }

    [Category(CATEGORY_NAME_04), DisplayName("배속"), NumberRange(0, 16), Increment(1)]
    public float TimeScale
    {
        get { return timeScale; }
        set
        {
            timeScale = value;
            CheatManager.Instance.CheatTimeScale = timeScale;
            Time.timeScale = CheatManager.Instance.CheatTimeScale;

            if (UFE.Instance)
                UFE.Instance.SetTimeScale(1);
        }
    }

    [Category(CATEGORY_NAME_04), DisplayName("인터랙션 컷씬 성공모드")]
    public CheatValues CutSceneInteractionSuccessMode
    {
        get { return cutSceneInteractionSuccessMode; }
        set
        {
            cutSceneInteractionSuccessMode = value;
            bool isOn = cutSceneInteractionSuccessMode == CheatValues.On;
            if (CutsceneManager.Instance.IsPlaying)
                CutsceneManager.Instance.IsInteractAlwaysSuccess = isOn;
        }
    }

    [Category(CATEGORY_NAME_04)]
    public void BattleUI()
    {
        CheatManager.Instance.Command_BattleUI();
    }

    [Category(CATEGORY_NAME_04), DisplayName("플러피 타운")]
    public void HousingGroup()
    {
        CheatManager.Instance.Command_HousingGroup();
    }

    [Category(CATEGORY_NAME_04), DisplayName("아이템 전체생성")]
    public void GetAllItems()
    {
        CheatManager.Instance.Command_GetAllItems();
    }

    // [Category(CATEGORY_NAME_04), DisplayName("다음 전투튜토리얼")]
    // public void NextBattleTutorial()
    // {
    //     CheatManager.Instance.Command_NextBattleTutorial();
    // }

    // [Category(CATEGORY_NAME_04), DisplayName("튜토리얼 종료")]
    // public void EndBattleTutorial()
    // {
    //     CheatManager.Instance.Command_EndBattleTutorial();
    // }

    [Category(CATEGORY_NAME_04), DisplayName("전체 캐릭터\nMAX 레벨업")]
    public void CharacterAllMaxLevel()
    {
        CheatManager.Instance.Command_CharacterAllMaxLevel();
    }

    [Category(CATEGORY_NAME_04), DisplayName("재화 전체생성")]
    public void ItemSurrencyCreate()
    {
        CheatManager.Instance.Command_ItemSurrencyCreate();
    }

    [Category(CATEGORY_NAME_04), DisplayName("퀘스트")]
    public void QuestGroup()
    {
        CheatManager.Instance.Command_QuestGroup();
    }

    [Category(CATEGORY_NAME_04), DisplayName("스위핑")]
    public void UpdateStageDungeonCheat()
    {
        CheatManager.Instance.Command_UpdateStageDungeonCheat();
    }

    [Category(CATEGORY_NAME_04), DisplayName("하이드 컨텐츠 입장")]
    public void ContentsOpenGroup()
    {
        CheatManager.Instance.Command_ContentsOpenGroup();
    }

    [Category(CATEGORY_NAME_04), DisplayName("튜토리얼 재생")]
    public void TutorialGroup()
    {
        CheatManager.Instance.Command_TutorialGroup();
    }

    [Category(CATEGORY_NAME_04), DisplayName("캐쉬 샵")]
    public void CashShop()
    {
        CheatManager.Instance.Command_CashShop();
    }

    [Category(CATEGORY_NAME_04), DisplayName("FMOD")]
    public CheatValues FmodActiveState
    {
        get { return fmodActiveState; }
        set
        {
            fmodActiveState = value;
            bool isOn = fmodActiveState == CheatValues.On;
            CheatManager.Instance.Command_ActiveFMOD(isOn);
        }
    }

    [Category(CATEGORY_NAME_04), DisplayName("메일 전체 생성")]
    public void SendAllMail()
    {
        CheatManager.Instance.Command_SendAllMail();
    }

    [Category(CATEGORY_NAME_04), DisplayName("가챠 테스트")]
    public void TestGacha()
    {
        CheatManager.Instance.Command_TestGacha();
    }
    #endregion

    #region [5. Graphics]
    [Category(CATEGORY_NAME_05), DisplayName("아웃라인"), Sort(0)]
    public bool Outline
    {
        get
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return false;

            var outlineRendererFeature = FindRendererFeatureByName(urpAsset, OUTLINE_RENDERER_FEATURE_NAME);

            if (outlineRendererFeature != null)
            {
                return outlineRendererFeature.isActive;
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{OUTLINE_RENDERER_FEATURE_NAME}");
            }

            return false;
        }
        set
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return;

            var outlineRendererFeature = FindRendererFeatureByName(urpAsset, OUTLINE_RENDERER_FEATURE_NAME);

            if (outlineRendererFeature != null)
            {
                bool isActive = !outlineRendererFeature.isActive;
                outlineRendererFeature.SetActive(isActive);
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{OUTLINE_RENDERER_FEATURE_NAME}");
            }

            OnValueChanged(nameof(Outline), value);
        }
    }

    [Category(CATEGORY_NAME_05), NumberRange(30, 120), Increment(30)]
    public int FPS
    {
        get { return Application.targetFrameRate; }
        set
        {
            IronJade.Debug.Log($"[SROptions.FourGround9 FPS] 1 Application.targetFrameRate = {Application.targetFrameRate}");
            Application.targetFrameRate = value;
            IronJade.Debug.Log($"[SROptions.FourGround9 FPS] 2 Application.targetFrameRate = {Application.targetFrameRate}");
            BattleHelper.SetUseTargetFrame60(Application.targetFrameRate == 60);
            OnValueChanged("frameRate", value);
        }
    }

    [Category(CATEGORY_NAME_05)]
    public bool Decal
    {
        //get 
        //{ 
        //    var urpAsset = GetRendererData(0);

        //    if (urpAsset == null) 
        //        return false;

        //    return urpAsset.rendererFeatures[4].isActive;
        //}
        //set
        //{
        //    var urpAsset = GetRendererData(0);

        //    if (urpAsset == null) 
        //        return;

        //    urpAsset.rendererFeatures[4].SetActive(value);
        //}
        get
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return false;

            var decalRendererFeature = FindRendererFeatureByName(urpAsset, DECAL_RENDERER_FEATURE_NAME);

            if (decalRendererFeature != null)
            {
                return decalRendererFeature.isActive;
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{DECAL_RENDERER_FEATURE_NAME}");
            }

            return false;
        }
        set
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return;

            var decalRendererFeature = FindRendererFeatureByName(urpAsset, DECAL_RENDERER_FEATURE_NAME);

            if (decalRendererFeature != null)
            {
                bool isActive = !decalRendererFeature.isActive;
                decalRendererFeature.SetActive(isActive);
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{DECAL_RENDERER_FEATURE_NAME}");
            }

            OnValueChanged(nameof(Decal), value);
        }
    }

    [Category(CATEGORY_NAME_05)]
    public bool Grab
    {
        //get
        //{
        //    var urpAsset = GetRendererData(0);

        //    if (urpAsset == null)
        //        return false;

        //    return urpAsset.rendererFeatures[0].isActive;
        //}
        //set
        //{
        //    var urpAsset = GetRendererData(0);

        //    if (urpAsset == null)
        //        return;

        //    urpAsset.rendererFeatures[0].SetActive(value);
        //}
        get
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return false;

            var grabRendererFeature = FindRendererFeatureByName(urpAsset, GRAB_RENDERER_FEATURE_NAME);

            if (grabRendererFeature != null)
            {
                return grabRendererFeature.isActive;
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{GRAB_RENDERER_FEATURE_NAME}");
            }

            return false;
        }
        set
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return;

            var grabRendererFeature = FindRendererFeatureByName(urpAsset, GRAB_RENDERER_FEATURE_NAME);

            if (grabRendererFeature != null)
            {
                bool isActive = !grabRendererFeature.isActive;
                grabRendererFeature.SetActive(isActive);
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{GRAB_RENDERER_FEATURE_NAME}");
            }

            OnValueChanged(nameof(Grab), value);
        }
    }

    [Category(CATEGORY_NAME_05)]
    public bool Beautify
    {
        get
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return false;

            var beautifyRendererFeature = FindRendererFeatureByName(urpAsset, BEAUTIFY_RENDERER_FEATURE_NAME);

            if (beautifyRendererFeature != null)
            {
                return beautifyRendererFeature.isActive;
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{BEAUTIFY_RENDERER_FEATURE_NAME}");
            }

            return false;
        }
        set
        {
            var urpAsset = GetRendererData(0);

            if (urpAsset == null)
                return;

            var beautifyRendererFeature = FindRendererFeatureByName(urpAsset, BEAUTIFY_RENDERER_FEATURE_NAME);

            if (beautifyRendererFeature != null)
            {
                bool isActive = !beautifyRendererFeature.isActive;
                beautifyRendererFeature.SetActive(isActive);
            }
            else
            {
                IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{BEAUTIFY_RENDERER_FEATURE_NAME}");
            }

            OnValueChanged(nameof(Beautify), value);
        }
    }

    [Category(CATEGORY_NAME_05)]
    public void VSync()
    {
        CheatManager.Instance.Command_VSync();
    }

    [Category(CATEGORY_NAME_05)]
    public bool CameraPostProcess
    {
        get
        {
            Camera camera = Camera.main;
            if (!camera) return false;

            UniversalAdditionalCameraData universalAdditionalCameraData = camera.GetUniversalAdditionalCameraData();
            if (!universalAdditionalCameraData) return false;

            return universalAdditionalCameraData.renderPostProcessing;
        }
        set
        {
            Camera camera = Camera.main;
            if (!camera) return;

            UniversalAdditionalCameraData universalAdditionalCameraData = camera.GetUniversalAdditionalCameraData();
            if (!universalAdditionalCameraData) return;

            universalAdditionalCameraData.renderPostProcessing = value;
        }
    }

    public bool useBeautifyTurboMode = false;
    [Category(CATEGORY_NAME_05)]
    public bool UseBeautifyTurboMode
    {
        get
        {
            return useBeautifyTurboMode;
        }
        set
        {
            useBeautifyTurboMode = value;

            foreach (var volumeSettings in UnityEngine.Object.FindObjectsOfType<CinemachineVolumeSettings>())
            {
                if (!volumeSettings || !volumeSettings.m_Profile) continue;

                if (volumeSettings.m_Profile.TryGet<Beautify.Universal.Beautify>(out var beautifySettings))
                {
                    beautifySettings.turboMode.value = value;
                }
            }

            foreach (var volumeSettings in UnityEngine.Object.FindObjectsOfType<Volume>())
            {
                if (!volumeSettings || !volumeSettings.profile) continue;

                if (volumeSettings.profile.TryGet<Beautify.Universal.Beautify>(out var beautifySettings))
                {
                    beautifySettings.turboMode.value = value;
                }
            }
        }
    }

    [Category(CATEGORY_NAME_05)]
    public DepthPrimingMode DepthPrimingMode
    {
        get
        {
            var urpAsset = GetRendererData(0) as UniversalRendererData;
            if (urpAsset == null)
            {
                return DepthPrimingMode.Disabled;
            }

            return urpAsset.depthPrimingMode;
        }
        set
        {
            var urpAsset = GetRendererData(0) as UniversalRendererData;
            if (urpAsset == null)
            {
                return;
            }

            urpAsset.depthPrimingMode = value;
        }
    }

    [Category(CATEGORY_NAME_05)]
    public bool UseNativeRenderPass
    {
        get
        {
            var urpAsset = GetRendererData(0) as UniversalRendererData;
            if (urpAsset == null)
            {
                return false;
            }

            return urpAsset.useNativeRenderPass;
        }
        set
        {
            var urpAsset = GetRendererData(0) as UniversalRendererData;
            if (urpAsset == null)
            {
                return;
            }

            urpAsset.useNativeRenderPass = value;
        }
    }

    [Category(CATEGORY_NAME_05)]
    public string CurrentVolumeProfiles
    {
        get
        {
            string log = string.Empty;

            foreach (var volumeSettings in UnityEngine.Object.FindObjectsOfType<CinemachineVolumeSettings>())
            {
                if (!volumeSettings.gameObject.activeInHierarchy || !volumeSettings.isActiveAndEnabled) continue;
                if (!volumeSettings || !volumeSettings.m_Profile) continue;

                log += $"{volumeSettings.m_Profile.name}, ";
            }

            foreach (var volumeSettings in UnityEngine.Object.FindObjectsOfType<Volume>())
            {
                if (!volumeSettings.gameObject.activeInHierarchy || !volumeSettings.isActiveAndEnabled) continue;
                if (!volumeSettings || !volumeSettings.profile) continue;

                log += $"{volumeSettings.profile.name}, ";
            }

            return log;
        }
    }

    [Category(CATEGORY_NAME_05)]
    public bool UseDefaultV2Renderer
    {
        get
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                UnityEngine.Debug.LogError("URP Asset not assigned.");
                return false;
            }

            var field = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                UnityEngine.Debug.LogError("Cannot access m_DefaultRendererIndex.");
                return false;
            }

            return (int)field.GetValue(urpAsset) == 2;
        }
        set
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                UnityEngine.Debug.LogError("URP Asset not assigned.");
                return;
            }

            var field = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                UnityEngine.Debug.LogError("Cannot access m_DefaultRendererIndex.");
                return;
            }

            field.SetValue(urpAsset, value ? 2 : 0);
        }
    }

    //[Category(CATEGORY_NAME_05)]
    //public bool Blur
    //{
    //    get
    //    {
    //        var urpAsset = GetRendererData(0);

    //        if (urpAsset == null)
    //            return false;

    //        var blurRendererFeature = FindRendererFeatureByName(urpAsset, BLUR_RENDERER_FEATURE_NAME);

    //        if (blurRendererFeature != null)
    //        {
    //            return blurRendererFeature.isActive;
    //        }
    //        else
    //        {
    //            IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{BLUR_RENDERER_FEATURE_NAME}");
    //        }

    //        return false;
    //    }
    //    set
    //    {
    //        var urpAsset = GetRendererData(0);

    //        if (urpAsset == null)
    //            return;

    //        var blurRendererFeature = FindRendererFeatureByName(urpAsset, BLUR_RENDERER_FEATURE_NAME);

    //        if (blurRendererFeature != null)
    //        {
    //            bool isActive = !blurRendererFeature.isActive;
    //            blurRendererFeature.SetActive(isActive);
    //        }
    //        else
    //        {
    //            IronJade.Debug.LogWarning($"Renderer feature not found. Renderer feature name:{BLUR_RENDERER_FEATURE_NAME}");
    //        }

    //        OnValueChanged(nameof(Blur), value);
    //    }
    //}
    #endregion

    #region [6. Battle Effect]
    [Category(CATEGORY_NAME_06)]
    public CheatValues EffectRender
    {
        get { return effectRenderOnOff; }
        set
        {
            effectRenderOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = effectRenderOnOff == CheatValues.On;
                    UFE.Instance.SetEffectRenderOn(isOn);
                }
                return;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues EffectLoad
    {
        get { return effectLoadOnOff; }
        set
        {
            effectLoadOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = effectLoadOnOff == CheatValues.On;
                    UFE.Instance.SetEffectLoadOn(isOn);
                }
                return;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues EffectActive
    {
        get { return effectActiveOnOff; }
        set
        {
            effectActiveOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = effectActiveOnOff == CheatValues.On;
                    UFE.Instance.SetEffectActiveOn(isOn);
                }
                return;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues EffectHit
    {
        get { return effectHitOnOff; }
        set
        {
            effectHitOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = effectHitOnOff == CheatValues.On;
                    UFE.Instance.SetEffectNoHitOn(isOn);
                }
                return;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues EffectAttack
    {
        get { return effectAttackOnOff; }
        set
        {
            effectAttackOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = effectAttackOnOff == CheatValues.On;
                    UFE.Instance.SetEffectAttackOn(isOn);
                }
                return;

            }
        }
    }

    [Category(CATEGORY_NAME_06), DisplayName("TimeScale")]
    public CheatValues EffectTimeScale
    {
        get { return timeScaleOnOff; }
        set
        {
            timeScaleOnOff = value;

            if (SceneManager.GetActiveScene().name == "BattlePrototype" || SceneManager.GetActiveScene().name == "500_Battle")
            {
                if (UFE.Instance != null)
                {
                    bool isOn = timeScaleOnOff == CheatValues.On;
                    UFE.Instance.SetTimeScaleLock(!isOn);
                }
                return;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues PostProcess
    {
        get { return effectVolumeOnOff; }
        set
        {
            effectVolumeOnOff = value;
            bool isOn = effectVolumeOnOff == CheatValues.On;

            Volume[] allVolumes = Resources.FindObjectsOfTypeAll<Volume>();

            foreach (Volume volume in allVolumes)
            {
                volume.enabled = isOn;
            }
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues EnemyOne
    {
        get
        {
            if (CheatManager.Instance.IsEnemyOneOn)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsEnemyOneOn != isOn)
                CheatManager.Instance.OnClickEnemyOneOnOff();
        }
    }

    [Category(CATEGORY_NAME_06)]
    public CheatValues DebugUI
    {
        get
        {
            if (CheatManager.Instance.IsDebugUIOn)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsDebugUIOn != isOn)
                CheatManager.Instance.OnClickDebugUI();
        }
    }

    [Category(CATEGORY_NAME_06), Sort(10)]
    public CheatValues HitStop
    {
        get
        {
            if (CheatManager.Instance.IsHitStopOn)
                return CheatValues.On;
            else
                return CheatValues.Off;
        }
        set
        {
            bool isOn = value == CheatValues.On;

            if (CheatManager.Instance.IsHitStopOn != isOn)
                CheatManager.Instance.OnClickHitStopOnOff();
        }
    }

    //[Category(CATEGORY_NAME_06)]
    //public async void BossGimmick()
    //{
    //    string timelinePath = BattleProcessManager.Instance.BattleInfo.CurrentWaveInfo.DungeonGimmickInfo[BattleProcessManager.Instance.BattleInfo.CurrentWaveInfo.CurrentGimmickIndex].TimeLinePath;
    //    List<string> prefabPaths = BattleProcessManager.Instance.BattleInfo.CurrentWaveInfo.DungeonGimmickInfo[BattleProcessManager.Instance.BattleInfo.CurrentWaveInfo.CurrentGimmickIndex].PrefabPaths;
    //
    //    if (GameObject.Find("OffsetSaveGimmickTimeLine").transform.childCount == 0)
    //    {
    //        GameObject timeline = UtilModel.Resources.Instantiate<GameObject>(timelinePath);
    //        List<GameObject> prefabs = new List<GameObject>() { UtilModel.Resources.Instantiate<GameObject>(prefabPaths[0]) };
    //        BattleProcessTimelinePlayer.OnEventGimmickTimelineEnd(timelinePath, timeline, prefabPaths, prefabs);
    //    }
    //    //UFE.Instance.P2ControlsScript.AI.SetAIState(UFE_AIStates.QueueBattleGimmickPositionMove);
    //    await BattleProcessTimelinePlayer.OnPlayGimmickTimeLine();
    //}
    #endregion

    #region [7. Sound]
    [Category(CATEGORY_NAME_07)]
    public CheatValues Volume
    {
        get { return volume; }
        set
        {
            BackgroundSceneManager bgManager = BackgroundSceneManager.Instance;

            if (bgManager == null)
                return;

            if (bgManager.GetBeautifyVolumes() == null)
                return;

            if (value == CheatValues.On)
            {
                bool isBeautify = BeautifyVolume == CheatValues.On;

                GameObject[] volumes = isBeautify ? bgManager.GetBeautifyVolumes() : bgManager.GetDefaultVolumes();

                if (volumes == null)
                {
                    return;
                }

                foreach (var obj in volumes)
                    obj.SafeSetActive(true);
            }
            else
            {
                foreach (var obj in bgManager.GetBeautifyVolumes())
                    obj.SafeSetActive(false);
                foreach (var obj in bgManager.GetDefaultVolumes())
                    obj.SafeSetActive(false);

                BeautifyVolume = CheatValues.Off;
                OnValueChanged(nameof(BeautifyVolume), value);
            }
            volume = value;
        }
    }

    [Category(CATEGORY_NAME_07)]
    public CheatValues BeautifyVolume
    {
        get
        {
            if (volume == CheatValues.Off)
                beautifyVolume = CheatValues.Off;

            return beautifyVolume;
        }
        set
        {
            BackgroundSceneManager bgManager = BackgroundSceneManager.Instance;

            if (bgManager == null)
                return;

            if (bgManager.GetBeautifyVolumes() == null)
                return;

            if (volume == CheatValues.Off)
                return;

            bool isBeautify = beautifyVolume == CheatValues.On;

            foreach (var obj in bgManager.GetBeautifyVolumes())
                obj.SafeSetActive(isBeautify);

            foreach (var obj in bgManager.GetDefaultVolumes())
                obj.SafeSetActive(!isBeautify);

            beautifyVolume = value;
        }
    }

    [Category(CATEGORY_NAME_07)]
    public void FindVolume()
    {
        CheatManager.Instance.Command_FindVolume();
    }
    #endregion

    #region [8. Reset]
    [Category(CATEGORY_NAME_08), DisplayName("Re-Select\nServer")]
    public void ReSelectServer()
    {
        CheatManager.Instance.Command_ReSelectServer();
    }

    [Category(CATEGORY_NAME_08), DisplayName("Reset Daily\nAnimation")]
    public void ResetDailyAnimation()
    {
        CheatManager.Instance.Command_ResetDailyAnimation();
    }

    [Category(CATEGORY_NAME_08), DisplayName("\bReset User")]
    public void ResetUser()
    {
        CheatManager.Instance.OnClickResetUser();
    }

    [Category(CATEGORY_NAME_08), DisplayName("Delete\nCache")]
    public void DeleteCache()
    {
        CheatManager.Instance.OnClickDeleteCache();
    }

    [Category(CATEGORY_NAME_08), DisplayName("ReturnToLogo")]
    public void ReturnToLogo()
    {
        GameManager.Instance.ReturnToLogo();
    }
    #endregion

    #region [9. URP]
    [Category(CATEGORY_NAME_09), DisplayName("Focus to [URP-HighFidelity]")]
    public void FocusRenderPipelineAsset()
    {
        CheatManager.Instance.Command_FocusRenderPipelineAsset();
    }
    #endregion

    #region [10. DeviceRenderQuality]
    [Category(CATEGORY_NAME_10)]
    public bool IsHighEndMemory => DeviceRenderQuality.IsHighEndMemory();

    [Category(CATEGORY_NAME_10)]
    public bool IsHighEndMobileGPU => DeviceRenderQuality.IsHighEndMobileGPU();

    [Category(CATEGORY_NAME_10), NumberRange(0.25f, 2f), Increment(0.25f)]
    public float DefaultRenderScale
    {
        get => DeviceRenderQuality.DefaultRenderScale;
        set
        {
            DeviceRenderQuality.DefaultRenderScale = value;
            DeviceRenderQuality.RecalcRenderScale();
        }
    }

    [Category(CATEGORY_NAME_10), NumberRange(0.25f, 2f), Increment(0.25f)]
    public float HighRenderScale
    {
        get => DeviceRenderQuality.HighRenderScale;
        set
        {
            DeviceRenderQuality.HighRenderScale = value;
            DeviceRenderQuality.RecalcRenderScale();
        }
    }

    [Category(CATEGORY_NAME_10), NumberRange(0.25f, 1f), Increment(0.25f)]
    public bool PortiraitMode
    {
        get => DeviceRenderQuality.PortiraitMode;
    }

    [Category(CATEGORY_NAME_10), NumberRange(0.25f, 1f), Increment(0.25f)]
    public bool VideoMode
    {
        get => DeviceRenderQuality.VideoMode;
    }

    [Category(CATEGORY_NAME_10), NumberRange(0.25f, 1f), Increment(0.25f)]
    public float CurrRenderScale
    {
        get => DeviceRenderQuality.CurrRenderScale;
    }

    [Category(CATEGORY_NAME_10)]
    public bool UseLowMemoryMode
    {
        get => DeviceRenderQuality.UseLowMemoryMode;
        set => DeviceRenderQuality.UseLowMemoryMode = value;
    }
    #endregion

    #region [11. Debug]
    [Category(CATEGORY_NAME_11), DisplayName("RuntimeInspectorUI"), Sort(0)]
    public void CreateRuntimeInspectorUI()
    {
        RuntimeInspectorUI.CreateInstance();
        SRDebug.Instance.HideDebugPanel();
    }

    [Category(CATEGORY_NAME_11), DisplayName("RenderingDebugger"), Sort(1)]
    public void CreateRenderingDebugger()
    {
        RenderingDebugger.CreateInstance();
        SRDebug.Instance.HideDebugPanel();
    }

    [Category(CATEGORY_NAME_11), DisplayName("DisableSentry"), Sort(2)]
    public void DisableSentry()
    {
        if (SentrySdk.IsEnabled)
        {
            IronJade.Debug.Log("Sentry is disabled.");
            SentrySdk.Close();
        }
        else
        {
            IronJade.Debug.Log("Sentry is already disabled.");
        }
    }

    [Category(CATEGORY_NAME_11), DisplayName("EnableUnityLog"), Sort(2)]
    public bool EnableUnityLog
    {
        get
        {
            return Debug.unityLogger.logEnabled;
        }

        set
        {
            Debug.unityLogger.logEnabled = value;
        }
    }
    #endregion

    private void OnValueChanged(string n, object newValue)
    {
        IronJade.Debug.Log($"[SRDebug] {n} value changed to {newValue}");
        OnPropertyChanged(n);
    }

    private static ScriptableRendererData GetRendererData(int rendererIndex = 0)
    {
        // 현재 사용 중인 Render Pipeline Asset을 가져옵니다.
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urpAsset == null)
        {
            IronJade.Debug.LogError("현재 Universal Render Pipeline Asset이 설정되지 않았습니다.");
            return null;
        }

        FieldInfo propertyInfo = urpAsset.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        ScriptableRendererData[] rendererDatas = (ScriptableRendererData[])propertyInfo.GetValue(urpAsset);
        if (rendererDatas == null || rendererDatas.Length <= 0) return null;
        if (rendererIndex < 0 || rendererDatas.Length <= rendererIndex) return null;

        return rendererDatas[rendererIndex];
    }

    private static ScriptableRendererFeature FindRendererFeatureByName(ScriptableRendererData urpAsset, string featureName)
    {
        for (int i = 0; i < urpAsset.rendererFeatures.Count; i++)
        {
            if (urpAsset.rendererFeatures[i].name == featureName)
            {
                return urpAsset.rendererFeatures[i];
            }
        }

        return null;
    }
}

#endif