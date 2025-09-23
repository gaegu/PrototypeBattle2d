using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ReversalMask : Image
{
    private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
    private Material modifiedMaterial;

    public override Material materialForRendering
    {
        get
        {
            if (modifiedMaterial == null)
            {
                modifiedMaterial = new Material(base.materialForRendering);
                modifiedMaterial.SetInt(StencilComp, (int) CompareFunction.NotEqual);
            }
            return modifiedMaterial;
        }
    }

    protected override void OnDestroy()
    {
        // 메모리 관리를 위해 Material이 더 이상 필요 없을 때 파기
        if (modifiedMaterial != null)
        {
            DestroyImmediate(modifiedMaterial);
        }
        base.OnDestroy();
    }
}