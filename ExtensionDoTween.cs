using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPLibrary;
using DG.Tweening.Core.Enums;

public static class ExtensionDoTween
{
    #region DOTWEEN

    public static TweenerCore<Vector3, Vector3, VectorOptions> DOMove(this FPTransform target, Vector3 endValue, float duration, bool snapping = false)
    {
        TweenerCore<Vector3, Vector3, VectorOptions> tweenerCore = DOTween.To(() => target.position.ToVector(), delegate (Vector3 x)
        {
            target.position = FPVector.ToFPVector(x);
        }, endValue, duration);
        tweenerCore.SetOptions(snapping).SetTarget(target);
        return tweenerCore;
    }

    public static Tweener DOLookAt(this FPTransform target, Vector3 towards, float duration, ControlsScript controlsScript, AxisConstraint axisConstraint = AxisConstraint.None, Vector3? up = null)
    {
        TweenerCore<Quaternion, Vector3, QuaternionOptions> tweenerCore = DOTween.To(() => target.rotation.ToQuaternion(), delegate (Quaternion x)
        {
            target.rotation = FPQuaternion.ToFPQuaternion(x);

            if (controlsScript != null)
            {
                controlsScript.SetWorldTransformRotation(FPQuaternion.ToFPQuaternion(x), true);
            }
        }, towards, duration).SetTarget(controlsScript.transform).SetSpecialStartupMode(SpecialStartupMode.SetLookAt);
        tweenerCore.plugOptions.axisConstraint = axisConstraint;
        tweenerCore.plugOptions.up = ((!up.HasValue) ? Vector3.up : up.Value);
        tweenerCore.Play();
        return tweenerCore;
    }

    public static Tweener DORotate(this FPTransform target, Vector3 endValue, float duration, ControlsScript controlsScript, RotateMode mode = RotateMode.Fast)
    {
        TweenerCore<Quaternion, Vector3, QuaternionOptions> tweenerCore = DOTween.To(() => target.rotation.ToQuaternion(), delegate (Quaternion x)
        {
            target.rotation = FPQuaternion.ToFPQuaternion(x);

            if (controlsScript != null)
            {
                controlsScript.SetWorldTransformRotation(FPQuaternion.ToFPQuaternion(x), true);
            }
        }, endValue, duration).SetTarget(controlsScript.transform);
        tweenerCore.plugOptions.rotateMode = mode;
        tweenerCore.Play();
        return tweenerCore;
    }

    #endregion
}
