#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using Cysharp.Threading.Tasks;

[CustomEditor(typeof(CharacterSpriteAnimator))]
public class CharacterSpriteAnimatorInspector : Editor
{
    private CharacterSpriteAnimator inspectorTarget = null;

    //============================================================
    //=========    Coding rule에 맞춰서 작업 바랍니다.   =========
    //========= Coding rule region은 절대 지우지 마세요. =========
    //=========    문제 시 '김철옥'에게 문의 바랍니다.   =========
    //============================================================

    #region Coding rule : Property
    public bool IsPlayingTestAnimation { get => playingSpriteSheetIndex != -1; }
    #endregion Coding rule : Property

    #region Coding rule : Value
    private bool folder = true;
    private List<bool> dataFolders = new List<bool>();
    private List<bool> defaultValues = new List<bool>();
    private CharacterSpriteAnimator util = null;
    private int count = 0;
    private int playingSpriteSheetIndex = -1;
    #endregion Coding rule : Value

    #region Coding rule : Function
    private void ResetValues()
    {
        dataFolders.Clear();
        defaultValues.Clear();

        bool isExistDefault = false;
        for (int i = 0; i < util.spriteSheets.Count; ++i)
        {
            dataFolders.Add(true);
            defaultValues.Add(util.spriteSheets[i].isDefault);

            isExistDefault |= util.spriteSheets[i].isDefault;
        }

        // 첫 번째 인덱스가 기본
        if (!isExistDefault && defaultValues.Count > 0)
            defaultValues[0] = true;

        count = util.spriteSheets.Count;
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private void UpdateAnimation()
    {
        if (!IsPlayingTestAnimation)
            return;

        util.OnUpdateAnimation();
    }

    private async UniTask PlayAnimation(int index)
    {
        if (IsPlayingTestAnimation)
            StopAnimation();
        else
            await util.LoadAnimationsAsync(true);

        playingSpriteSheetIndex = index;
        var spriteSheet = util.spriteSheets[index];

        util.InitializeByEditor();
        util.SetAnimation(spriteSheet.key);

        while (true)
        {
            UpdateAnimation();

            await UniTask.Delay(20, cancellationToken: TokenPool.Get(GetHashCode()));
        }
    }

    private void StopAnimation()
    {
        playingSpriteSheetIndex = -1;

        TokenPool.Cancel(GetHashCode());
    }

    public override void OnInspectorGUI()
    {
        try
        {
            if (util == null)
            {
                util = target as CharacterSpriteAnimator;

                ResetValues();
            }

            if (count < util.spriteSheets.Count)
            {
                // 개수가 줄어든 것
                while (count < util.spriteSheets.Count)
                {
                    int removeIndex = util.spriteSheets.Count - 1;

                    dataFolders.RemoveAt(removeIndex);
                    defaultValues.RemoveAt(removeIndex);
                    util.spriteSheets.RemoveAt(removeIndex);
                }
            }
            else if (count > util.spriteSheets.Count)
            {
                // 개수가 늘어난 것
                while (count > util.spriteSheets.Count)
                {
                    util.spriteSheets.Add(new CharacterSpriteAnimator.SpriteSheetNode());
                    dataFolders.Add(false);
                    defaultValues.Add(defaultValues.Count == 0);
                }
            }

            GUILayout.BeginHorizontal();
            {
                folder = EditorGUILayout.Foldout(folder, "Sheet");
                count = EditorGUILayout.IntField(util.spriteSheets.Count, GUILayout.Width(50));
            }
            GUILayout.EndHorizontal();

            if (!folder)
                return;

            EditorGUILayout.Space(6);

            GUILayout.BeginVertical("애니메이션 안나오면 해당 텍스쳐스 폴더에서오른쪽 버튼 누르시고 *COSMOS*/Setup This Folder 누르샴 ", "window");
            {
                EditorGUILayout.Space(16);

                for (int i = 0; i < util.spriteSheets.Count; ++i)
                {
                    EditorGUI.indentLevel = 1;

                    GUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.Space(10, false);

                        GUILayout.BeginVertical(style: "window");
                        {
                            EditorGUILayout.Space(-20, false);

                            dataFolders[i] = EditorGUILayout.Foldout(dataFolders[i], "Element");

                            EditorGUI.indentLevel = 2;

                            if (dataFolders[i])
                            {
                                EditorGUI.BeginDisabledGroup(IsPlayingTestAnimation);
                                DrawKey(i);
                                DrawDefaultValue(i);
                                DrawFrameDelay(i);
                                DrawMain(i);
                                EditorGUI.EndDisabledGroup();
                                DrawPlayButton(i);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel = 1;

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("+", GUILayout.Width(22)))
                    {
                        if (util.spriteSheets.Count < int.MaxValue)
                        {
                            if (util.spriteSheets.Count == 0)
                                util.spriteSheets.Add(new CharacterSpriteAnimator.SpriteSheetNode());
                            else
                                util.spriteSheets.Add(new CharacterSpriteAnimator.SpriteSheetNode(util.spriteSheets[util.spriteSheets.Count - 1]));

                            count = util.spriteSheets.Count;
                            dataFolders.Add(true);
                            defaultValues.Add(defaultValues.Count == 0);
                        }
                    }

                    if (GUILayout.Button("-", GUILayout.Width(22)))
                    {
                        if (util.spriteSheets.Count > 0)
                        {
                            int removeIndex = util.spriteSheets.Count - 1;
                            count = removeIndex;
                            dataFolders.RemoveAt(removeIndex);
                            defaultValues.RemoveAt(removeIndex);
                            util.spriteSheets.RemoveAt(removeIndex);
                        }
                    }
                }
                GUILayout.EndHorizontal();

                EditorGUI.indentLevel = 0;
            }
            GUILayout.EndVertical();

            if (!IsPlayingTestAnimation)
            {
                PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                EditorUtility.SetDirty(ResourcesPathObject.GetEditorSaveObject());
                EditorUtility.SetDirty(target);

                serializedObject.ApplyModifiedProperties();
            }
        }
        catch
        {
            util = null;
        }
    }

    private void DrawKey(int i)
    {
        CharacterSpriteAnimator.SpriteSheetNode data = util.spriteSheets[i];

        GUILayout.BeginHorizontal();
        {
            data.key = EditorGUILayout.TextField($"Key", data.key);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawDefaultValue(int i)
    {
        CharacterSpriteAnimator.SpriteSheetNode data = util.spriteSheets[i];

        GUILayout.BeginHorizontal();
        {
            bool isDefault = EditorGUILayout.Toggle($"Default Animation", defaultValues[i]);

            if (defaultValues[i] != isDefault)
            {
                for(int j = 0; j < defaultValues.Count; j++)
                {
                    int enableIndex = isDefault ? i : 0;

                    defaultValues[j] = j == enableIndex;
                }
            }

            data.isDefault = defaultValues[i];
        }
        GUILayout.EndHorizontal();
    }

    private void DrawFrameDelay(int i)
    {
        CharacterSpriteAnimator.SpriteSheetNode data = util.spriteSheets[i];

        GUILayout.BeginHorizontal();
        {
            data.isCustomDelay = EditorGUILayout.Toggle("Custom Delay 사용 유무", data.isCustomDelay);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            if (data.isCustomDelay)
            {
                GUILayout.BeginVertical();
                {
                    int count = data.customFrameDelay == null ? 0 : data.customFrameDelay.Length;

                    for (int index = 0; index < count; index++)
                    {
                        if (index == 0)
                            EditorGUILayout.PrefixLabel("Custom Delay");

                        data.customFrameDelay[index] = EditorGUILayout.IntField(data.customFrameDelay[index]);
                    }

                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("+", GUILayout.Width(22)))
                        {
                            int[] array = new int[count + 1];

                            if(data.customFrameDelay != null)
                                System.Array.Copy(data.customFrameDelay, array, count);

                            data.customFrameDelay = array;
                        }


                        if (GUILayout.Button("-", GUILayout.Width(22)))
                        {
                            if (count > 0)
                            {
                                int[] array = new int[count - 1];
                                System.Array.Copy(data.customFrameDelay, array, array.Length);
                                data.customFrameDelay = array;
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            else if (data.customFrameDelay != null && data.customFrameDelay.Length > 0)
                data.customFrameDelay = new int[0];
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            int frameDelay = EditorGUILayout.IntField("Frame Delay (기본 딜레이)", data.frameDelay);

            if (frameDelay >= 0)
                data.frameDelay = frameDelay;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMain(int i)
    {
        CharacterSpriteAnimator.SpriteSheetNode data = util.spriteSheets[i];

        string guidToPath = AssetDatabase.GUIDToAssetPath(data.guid);

        if (!string.IsNullOrEmpty(guidToPath))
            data.sourceImage = AssetDatabase.LoadAssetAtPath<Sprite>(guidToPath);

        data.sourceImage = (Sprite)EditorGUILayout.ObjectField("SourceImage", data.sourceImage, typeof(Sprite), false);

        if (data.sourceImage == null)
        {
            data.guid = string.Empty;
            return;
        }

        (string, string) result = ResourcesPathObject.GetEditorGuids(data.sourceImage);
        string guid = result.Item1;
        string path = result.Item2;

        if (string.IsNullOrEmpty(guid))
        {
            data.guid = string.Empty;
            return;
        }

        data.guid = guid;

        ResourcesPathObject.SetPath(guid, path);
    }

    private void DrawPlayButton(int i)
    {
        if (playingSpriteSheetIndex == i)
        {
            if (GUILayout.Button("Stop", GUILayout.Width(150)))
                StopAnimation();
        }
        else
        {
            if (GUILayout.Button("Play Animation Test", GUILayout.Width(150)))
                PlayAnimation(i).Forget();
        }
    }
    #endregion Coding rule : Function
}
#endif