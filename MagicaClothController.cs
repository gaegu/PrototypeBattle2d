using UnityEngine;
using System.Collections.Generic;
    
public class MagicaClothController : MonoBehaviour
{
    public Animator animator;
    private List<MagicaCloth2.MagicaCloth> magicaClothList = new List<MagicaCloth2.MagicaCloth>();

    [System.Serializable]
    public class AnimationClothState
    {
        public string animationName;
        public bool enableCloth;
    }

    public List<AnimationClothState> animationClothStates = new List<AnimationClothState>();
    private Dictionary<int, bool> stateHashToClothEnabled = new Dictionary<int, bool>();
    private int lastStateHash = 0;

    private void Awake()
    {
        // Animator 컴포넌트가 연결되지 않은 경우 자동으로 찾기
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // 모든 자식 객체에서 MagicaCloth 컴포넌트 찾기
        magicaClothList.AddRange(GetComponentsInChildren<MagicaCloth2.MagicaCloth>());

        if (magicaClothList.Count == 0)
        {
           IronJade.Debug.LogWarning("No MagicaCloth components found in children.");
        }
    }

    private void Start()
    {
        // 초기화: 애니메이션 이름의 해시와 Cloth 상태를 딕셔너리에 저장
        foreach (var state in animationClothStates)
        {
            int stateHash = Animator.StringToHash(state.animationName);
            stateHashToClothEnabled[stateHash] = state.enableCloth;
        }
    }

    private void LateUpdate()
    {
        if (animator == null || magicaClothList.Count == 0) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;

        // 애니메이션 상태가 변경되었을 때만 체크
        if (currentStateHash != lastStateHash)
        {
            lastStateHash = currentStateHash;

            if (stateHashToClothEnabled.TryGetValue(currentStateHash, out bool shouldEnableCloth))
            {
                foreach (var magicaCloth in magicaClothList)
                {
                    if (magicaCloth.enabled != shouldEnableCloth)
                    {
                        magicaCloth.enabled = shouldEnableCloth;
                    }
                }
            }
        }
    }

    // Editor에서 MagicaCloth 컴포넌트 목록을 갱신하는 메소드
    public void RefreshMagicaClothList()
    {
        magicaClothList.Clear();
        magicaClothList.AddRange(GetComponentsInChildren<MagicaCloth2.MagicaCloth>());
    }
}