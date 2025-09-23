using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

public abstract class TransitionTemplate
{
    protected TransitionType transitionType;
    protected TownFlowModel model;

    public async UniTask Execute()
    {
        // 공통 패턴: 트랜지션 In → 작업 → 트랜지션 Out
        await BeforeTransition();
        await TransitionManager.In(transitionType);

        try
        {
            await DoWork();
        }
        catch (Exception e)
        {
            Debug.LogError($"[TransitionTemplate] Error during work: {e}");
            await HandleError(e);
        }
        finally
        {
            await TransitionManager.Out(transitionType);
            await AfterTransition();
        }
    }

    protected virtual async UniTask BeforeTransition()
    {
        // 트랜지션 전 준비 (옵션)
        await UniTask.Yield();
    }

    protected abstract UniTask DoWork();  // 실제 작업 (필수)

    protected virtual async UniTask AfterTransition()
    {
        // 트랜지션 후 정리 (옵션)
        await UniTask.Yield();
    }

    protected virtual async UniTask HandleError(Exception e)
    {
        // 에러 처리 (옵션)
        Debug.LogError($"[TransitionTemplate] Handled error: {e.Message}");
        await UniTask.Yield();
    }
}