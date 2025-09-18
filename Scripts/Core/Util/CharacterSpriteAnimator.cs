//=========================================================================================================
#pragma warning disable CS1998
using System;

using System.Collections.Generic;
using Cysharp.Threading.Tasks;      // UniTask 관련 클래스 모음
using DG.DemiEditor;
using UnityEngine;                  // UnityEngine 기본
using UnityEngine.UI;               // UnityEngine의 UI 기본

public class CharacterSpriteAnimator : MonoBehaviour
{
    /// </summary>
    [Serializable]
    public class SpriteSheetNode
    {
#if UNITY_EDITOR
        public Sprite sourceImage;
#endif
        public string guid;
        public string key;
        public int frameDelay = 10;
        public bool isCustomDelay;
        public bool isDefault;
        public int[] customFrameDelay;

        public SpriteSheetNode() { }

        public SpriteSheetNode(SpriteSheetNode copyNode)
        {
#if UNITY_EDITOR
            sourceImage = copyNode.sourceImage;
#endif
            key = copyNode.key;
            frameDelay = copyNode.frameDelay;
            guid = copyNode.guid;
        }
    }

    #region Coding rule : Property
    public bool IsLoaded { get; private set; }
    public string CurrentKey { get; private set; }
    #endregion Coding rule : Property

    #region Coding rule : Value
    [SerializeField]
    public List<SpriteSheetNode> spriteSheets = new List<SpriteSheetNode>();

    private SpriteRenderer spriteRenderer = null;
    private Image spriteImage = null;
    private Dictionary<string, Sprite[]> loadedSprites = null;
    private int currentSpriteSheetIndex = 0;
    private int currentAnimationIndex = 0;
    private int currentAnimationCount = 0;
    private int passingFrame = 0;
    private bool isLoop = true;

    private string baseCharKey = "";
    #endregion Coding rule : Value

    #region Coding rule : Function
    private void Start()
    {
        InitRenderer();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터용
    /// </summary>
    public void InitializeByEditor()
    {
        Start();
    }
#endif


    public void Initialize( string baseKey)
    {
        this.baseCharKey = baseKey;
    }

    private void Update()
    {
        OnUpdateAnimation();
    }

    private void OnDestroy()
    {
        IsLoaded = false;

        loadedSprites = null;
    }

    public void OnUpdateAnimation()
    {
        if (!IsLoaded)
            return;

        if (!CheckAnimationFrame())
            return;

        NextAnimation();
    }

    private bool CheckAnimationFrame()
    {
        if (passingFrame < GetFrameDelay())
        {
            passingFrame++;
            return false;
        }
        else
        {
            passingFrame = 0;
            return true;
        }
    }

    public async UniTask LoadAnimationsAsync(bool force = false)
    {
        if (IsLoaded && !force)
            return;

        UniTask[] tasks = new UniTask[spriteSheets.Count];

        for (int i = 0; i < spriteSheets.Count; ++i)
        {
            tasks[i] = LoadSpriteAsync(i);
        }

        await UniTask.WhenAll(tasks);

        IsLoaded = true;
    }

    public static string ExtractCharacterNameSplit(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string[] parts = input.Split('_');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }


    private string getAddressKey( string spriteKey )
    {
        if (baseCharKey.IsNullOrEmpty() == true)
            baseCharKey = "Char_" + ExtractCharacterNameSplit(this.transform.parent.name);


        if( baseCharKey.Contains("Char_") == false )
        {
            baseCharKey = "Char_" + baseCharKey;
        }

        return baseCharKey + "_" + spriteKey + "_png";
    }

    private async UniTask LoadSpriteAsync(int spriteIndex)
    {
        if (spriteIndex < 0)
            return;

        if (spriteIndex >= spriteSheets.Count)
            return;

        string key = spriteSheets[spriteIndex].key;
        string path = ResourcesPathObject.GetPath(spriteSheets[spriteIndex].guid);

        if (loadedSprites != null && loadedSprites.ContainsKey(key))
            return;

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"경로를 찾지 못했습니다. (guid: {spriteSheets[spriteIndex].guid})");
            return;
        }

        string addressKey = getAddressKey(key);

        Sprite[] currentSprites = await ResourceLoadHelper.LoadAssetsAsync<Sprite>(addressKey);

        if (loadedSprites == null)
            loadedSprites = new Dictionary<string, Sprite[]>();

        if (currentSprites == null || currentSprites.Length == 0)
        {
           Debug.LogError($"{path} 에 Sprite 가 없습니다.");
            return;
        }

        loadedSprites[key] = currentSprites;
    }

    public void SetAnimation(string key, bool isLoop = true)
    {
        this.isLoop = isLoop;

        SetSpriteSheet(key);
    }

    private void SetSpriteSheet(string key)
    {
        CurrentKey = GetKey(key, out currentSpriteSheetIndex);
        currentAnimationIndex = 0;
        currentAnimationCount = GetSpriteSheetCount(CurrentKey);

        SetAnimationByIndex(currentAnimationIndex);
    }

    private void SetAnimationByIndex(int animationIndex)
    {
        if (loadedSprites == null)
            return;

        if (string.IsNullOrEmpty(CurrentKey))
            return;

        if (loadedSprites.TryGetValue(CurrentKey, out var sprites))
        {
            if (sprites.Length <= animationIndex)
                return;

            SetSprite(sprites[animationIndex]);
        }
    }


    private void InitRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteImage = GetComponent<Image>();
        }

    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer == null)
            InitRenderer();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
        }
        else if (spriteImage != null)
        {
            spriteImage.sprite = sprite;
        }
    }

    public void SetSize(Vector2 size)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.size = size;
        }
        else if (spriteImage != null)
        {
            spriteImage.rectTransform.sizeDelta = size;
        }
    }

    public void SetFlip(bool flipX, bool flipY)
    {
        if (spriteRenderer == null)
            InitRenderer();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = flipX;
            spriteRenderer.flipY = flipY;
        }
        else if (spriteImage != null)
        {
            spriteImage.rectTransform.localScale = new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 0);
        }
    }

    public void SetLayer(int layer)
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = layer;
    }

    private void NextAnimation()
    {
        SetAnimationByIndex(NextAnimationIndex());
    }

    private int NextAnimationIndex()
    {
        currentAnimationIndex++;

        if (currentAnimationIndex >= currentAnimationCount)
        {
            if (isLoop)
                currentAnimationIndex = 0;
            else
                currentAnimationIndex = currentAnimationCount - 1;
        }

        return currentAnimationIndex;
    }

    public Vector2 GetSize()
    {
        if (spriteRenderer != null)
        {
            return spriteRenderer.size;
        }
        else if (spriteImage != null)
        {
            return spriteImage.rectTransform.sizeDelta;
        }

        return Vector2.zero;
    }

    public (bool flipX, bool flipY) GetFlip()
    {
        if (spriteRenderer != null)
        {
            return (spriteRenderer.flipX, spriteRenderer.flipY);
        }
        else if (spriteImage != null)
        {
            Vector3 scale = spriteImage.rectTransform.localScale;

            return (scale.x < 0, scale.y < 0);
        }

        return (false, false);
    }

    private int GetSpriteSheetCount(string key)
    {
        if (loadedSprites == null)
            return 0;

        if (loadedSprites.TryGetValue(key, out var sprites))
        {
            return sprites.Length;
        }

        return 0;
    }

    private int GetFrameDelay()
    {
        if (spriteSheets.Count <= currentSpriteSheetIndex || currentSpriteSheetIndex < 0)
            return 0;

        SpriteSheetNode node = spriteSheets[currentSpriteSheetIndex];

        if (!node.isCustomDelay)
            return node.frameDelay;

        if (node.customFrameDelay == null || node.customFrameDelay.Length <= currentAnimationIndex)
            return node.frameDelay;

        return node.customFrameDelay[currentAnimationIndex];
    }

    /// <summary>
    /// 애니메이션 Key 값이 없으면 기본 값을 반환함
    /// </summary>
    private string GetKey(string key, out int spriteSheetIndex)
    {
        string defaultkey = string.Empty;
        spriteSheetIndex = 0;

        for (int index = 0; index < spriteSheets.Count; index++)
        {
            if (spriteSheets[index].key == key)
            {
                spriteSheetIndex = index;
                return key;
            }

            if (spriteSheets[index].isDefault)
            {
                spriteSheetIndex = index;
                defaultkey = spriteSheets[index].key;
            }
        }

        return defaultkey;
    }

    public int GetLayer()
    {
        return spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
    }
#endregion Coding rule : Function
}
