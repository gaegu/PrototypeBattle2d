using UnityEngine;

[ExecuteInEditMode]
public class RotationToMaterial : MonoBehaviour
{
    public Material targetMaterial;
    public string forwardVectorProperty = "_ForwardVector";
    public string rightVectorProperty = "_RightVector";

    private Vector3 lastForward;
    private Vector3 lastRight;

    private void OnValidate()
    {
        // 에디터에서 값이 변경될 때 업데이트
        UpdateVectors();
    }

    private void LateUpdate()
    {
        // 플레이 모드와 에디터 모드 모두에서 매 프레임 업데이트
        UpdateVectors();
    }

    private void UpdateVectors()
    {
        if (targetMaterial == null) return;

        // 현재 오브젝트의 forward와 right 벡터를 가져옵니다.
        Vector3 currentForward = transform.forward;
        Vector3 currentRight = transform.right;

        // 벡터가 변경되었는지 확인합니다.
        if (currentForward != lastForward || currentRight != lastRight)
        {
            // 메터리얼에 벡터 값을 전달합니다.
            targetMaterial.SetVector(forwardVectorProperty, currentForward);
            targetMaterial.SetVector(rightVectorProperty, currentRight);

            // 마지막 값을 업데이트합니다.
            lastForward = currentForward;
            lastRight = currentRight;

#if UNITY_EDITOR
            // 에디터에서 변경 사항을 즉시 반영
            if (!Application.isPlaying)
            {
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }
    }
}