using System.Collections.Generic;
using IronJade.Table.Data;          // 데이터 테이블
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class InputManager : MonoBehaviour
{
    private static InputManager instance;
    public static InputManager Instance
    {
        get
        {
            if (instance == null)
            {
                string className = typeof(InputManager).Name;
                GameObject manager = GameObject.Find(className);
                instance = manager.GetComponent<InputManager>();

                if (instance == null)
                    instance = manager.AddComponent<InputManager>();
            }

            return instance;
        }
    }

    private InputManager() { }

    /// <summary>
    /// 변경이 가능한 단축키로 쓰는놈들만 여기에 추가하자. defalutKeys와 순서와 갯수 꼭 맞춰야함.
    /// UIBaseSettingShortCutKeysTab.cs 에 labelKeys도 밑에 순서에 맞게 넣어라.
    /// </summary>
    public enum EInputType
    {
        Hero,
    }

    /// <summary>
    /// EInputType과 순서와 갯수 꼭 맞춰야함.
    /// </summary>
    private readonly string[] defalutKeys = new string[]
    {
        "H",
    };

    /// <summary>
    /// 캐릭터 이동 관련 키, ESC키는 사용하면 안됨. 변경이 안되는 단축키는 여기다 넣고 Update()에 그냥 쓰자.
    /// </summary>
    private readonly string[] dontUseKeys = new string[]
    {
        "W",
        "A",
        "S",
        "D",
        "Escape",
    };

    private bool isInit = false;
    private bool isMultiTouch = false;
    private bool isInputBlock = false;
    private Dictionary<EInputType, InputKeyInfo> inputInfos = new Dictionary<EInputType, InputKeyInfo>();
    public Dictionary<EInputType, InputKeyInfo> InputInfos { get { return inputInfos; } }


    private void Awake()
    {
        Init();
        DontDestroyOnLoad(gameObject);

        InitInputInfos();
        isInit = true;
    }

    private void Init()
    {
        isInit = false;
        isInputBlock = false;
        isMultiTouch = false;
        inputInfos.Clear();
    }

    public void Create()
    {

    }

    private void InitInputInfos()
    {
        int nCount = 0;
        foreach (EInputType vKey in System.Enum.GetValues(typeof(EInputType)))
        {
            InputKeyInfo kInfo = new InputKeyInfo();
            kInfo.defaultKey = defalutKeys[nCount];
            kInfo.useKey = kInfo.defaultKey;

            inputInfos.Add(vKey, kInfo);
            nCount++;
        }
    }

    /// <summary>
    /// 로그인후에 로드해야함.
    /// </summary>
    public void LoadInputInfos()
    {
        //foreach (KeyValuePair<EInputType, InputKeyInfo> keyValues in inputInfos)
        //{
        //    string useKey = SaveLoadSystem.LoadString("InputKeyInfo_" + keyValues.Key.ToString(), "");
        //    if (string.IsNullOrEmpty(useKey) == false)
        //        keyValues.Value.useKey = useKey;
        //}
    }

    /// <summary>
    /// 저장은 UseKey만 하면 된다.
    /// </summary>
    public bool SaveInputInfos(bool isDefalut)
    {
        int iCount = 0;

        //foreach (KeyValuePair<EInputType,InputKeyInfo> keyValues in inputInfos)
        //{
        //    if (isDefalut == true)
        //    {
        //        if (keyValues.Value.defaultKey.Equals(keyValues.Value.useKey)) continue;

        //        keyValues.Value.useKey = keyValues.Value.defaultKey;
        //    }
        //    else
        //    {
        //        if (string.IsNullOrEmpty(keyValues.Value.tempKey)) continue;

        //        if (keyValues.Value.tempKey.Equals(keyValues.Value.useKey)) continue;

        //        keyValues.Value.useKey = keyValues.Value.tempKey;
        //    }

        //    iCount++;

        //    SaveLoadSystem.SaveString("InputKeyInfo_" + keyValues.Key.ToString(), keyValues.Value.useKey);
        //}

        return iCount > 0 ? true : false;
    }

    public void InitInputInfosTempKey()
    {
        foreach (KeyValuePair<EInputType, InputKeyInfo> keyValues in inputInfos)
        {
            keyValues.Value.tempKey = keyValues.Value.useKey;
        }
    }

    public bool CheckSameKey(EInputType eType, string strKey)
    {
        return inputInfos[eType].tempKey == strKey;
    }

    public bool CheckUsedKey(EInputType eType, string strKey)
    {
        if (dontUseKeys != null)
        {
            for(int iCount = 0; iCount < dontUseKeys.Length; iCount++)
            {
                if (dontUseKeys[iCount].Equals(strKey))
                    return true;
            }
        }

        foreach (KeyValuePair<EInputType, InputKeyInfo> keyValues in inputInfos)
        {
            if (keyValues.Value.tempKey.Equals(strKey))
                return true;
        }

        return false;
    }

    private void Update()
    {
        if (isInit == false) return;

        // 키 설정중에는 여기 타면 안됨.
        if (isInputBlock == true) return;

        //ESC는 고정키 다른데서 쓰면 안됩니다.
        if (Input.GetKeyUp(KeyCode.Escape))
        {

        }

        if (Input.GetKeyDown(GetKeyFromInfo(EInputType.Hero)))
        {

        }
    }

    private void InitMultiTouch()
    {
        isMultiTouch = false;
    }

    /// <summary>
    /// UI 표기용.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string KeyCodeToString(KeyCode key)
    {
        string output = "";
        output = key.ToString();
        output = output.Replace("Mouse0", "Left Mouse");
        output = output.Replace("Mouse1", "Right Mouse");
        output = output.Replace("Mouse2", "Middle Mouse");
        if (output == "Alpha0") output = "0";
        if (output == "Alpha1") output = "1";
        if (output == "Alpha2") output = "2";
        if (output == "Alpha3") output = "3";
        if (output == "Alpha4") output = "4";
        if (output == "Alpha5") output = "5";
        if (output == "Alpha6") output = "6";
        if (output == "Alpha7") output = "7";
        if (output == "Alpha8") output = "8";
        if (output == "Alpha9") output = "9";
        if (output == "Exclaim") output = "!";
        if (output == "DoubleQuote") output = "\"";
        if (output == "Hash") output = "#";
        if (output == "Dollar") output = "$";
        if (output == "Percent") output = "%";
        if (output == "Ampersand") output = "&";
        if (output == "Quote") output = "\'";
        if (output == "LeftParen") output = "(";
        if (output == "RightParen") output = ")";
        if (output == "Asterisk") output = "*";
        if (output == "Plus") output = "+";
        if (output == "Minus") output = "-";
        //if (output == "Comma") output = ",";
        //if (output == "Peroid") output = ".";
        if (output == "Slash") output = "/";
        if (output == "Colon") output = ":";
        if (output == "Semicolon") output = ";";
        if (output == "Less") output = "<";
        if (output == "Greater") output = ">";
        if (output == "Equals") output = "=";
        if (output == "Question") output = "?";
        if (output == "At") output = "@";
        if (output == "LeftBracket") output = "[";
        if (output == "RightBracket") output = "]";
        if (output == "Backslash") output = "\\";
        if (output == "Caret") output = "^";
        if (output == "Underscore") output = "_";
        if (output == "Backquote") output = "`";
        if (output == "LeftCurlyBracket") output = "{";
        if (output == "RightCurlyBracket") output = "}";
        if (output == "Tilde") output = "~";
        if (output == "Pipe") output = "|";
        return output;
    }


    public KeyCode StringToKeyCode(string key)
    {
        string output = "";
        output = key;
        output = output.Replace("Left Mouse", "Mouse0");
        output = output.Replace("Right Mouse", "Mouse1");
        output = output.Replace("Middle Mouse", "Mouse2");
        if (output == "0") output = "Alpha0";
        if (output == "1") output = "Alpha1";
        if (output == "2") output = "Alpha2";
        if (output == "3") output = "Alpha3";
        if (output == "4") output = "Alpha4";
        if (output == "5") output = "Alpha5";
        if (output == "6") output = "Alpha6";
        if (output == "7") output = "Alpha7";
        if (output == "8") output = "Alpha8";
        if (output == "9") output = "Alpha9";
        if (output == "!") output = "Exclaim";
        if (output == "\"") output = "DoubleQuote";
        if (output == "#") output = "Hash";
        if (output == "$") output = "Dollar";
        if (output == "%") output = "Percent";
        if (output == "&") output = "Ampersand";
        if (output == "\'") output = "Quote";
        if (output == "(") output = "LeftParen";
        if (output == ")") output = "RightParen";
        if (output == "*") output = "Asterisk";
        if (output == "+") output = "Plus";
        if (output == "-") output = "Minus";
        if (output == ",") output = "Comma";
        if (output == ".") output = "Peroid";
        if (output == "/") output = "Slash";
        if (output == ":") output = "Colon";
        if (output == ";") output = "Semicolon";
        if (output == "<") output = "Less";
        if (output == ">") output = "Greater";
        if (output == "?") output = "Question";
        if (output == "@") output = "At";
        if (output == "[") output = "LeftBracket";
        if (output == "]") output = "RightBracket";
        if (output == "\\") output = "Backslash";
        if (output == "^") output = "Caret";
        if (output == "_") output = "Underscore";
        if (output == "`") output = "Backquote";
        if (output == "{") output = "LeftCurlyBracket";
        if (output == "}") output = "RightCurlyBracket";
        if (output == "~") output = "Tilde";
        if (output == "|") output = "Pipe";

        KeyCode temp;
        System.Enum.TryParse(output, out temp);
        return temp;
    }

    /// <summary>
    /// UI 표기용.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string TranslateKeyName(string key)
    {
        string output = "";
        output = key.ToString();
        output = output.Replace("Mouse0", "Left Mouse");
        output = output.Replace("Mouse1", "Right Mouse");
        output = output.Replace("Mouse2", "Middle Mouse");
        if (output == "Alpha0") output = "0";
        if (output == "Alpha1") output = "1";
        if (output == "Alpha2") output = "2";
        if (output == "Alpha3") output = "3";
        if (output == "Alpha4") output = "4";
        if (output == "Alpha5") output = "5";
        if (output == "Alpha6") output = "6";
        if (output == "Alpha7") output = "7";
        if (output == "Alpha8") output = "8";
        if (output == "Alpha9") output = "9";
        if (output == "Exlaim") output = "!";
        if (output == "DoubleQuote") output = "\"";
        if (output == "Hash") output = "#";
        if (output == "Dollar") output = "$";
        if (output == "Percent") output = "%";
        if (output == "Ampersand") output = "&";
        if (output == "Quote") output = "\'";
        if (output == "LeftParen") output = "(";
        if (output == "RightParen") output = ")";
        if (output == "Asterisk") output = "*";
        if (output == "Plus") output = "+";
        if (output == "Minus") output = "-";
        //if (output == "Comma") output = ",";
        //if (output == "Peroid") output = ".";
        if (output == "Slash") output = "/";
        if (output == "Colon") output = ":";
        if (output == "Semicolon") output = ";";
        if (output == "Less") output = "<";
        if (output == "Greater") output = ">";
        if (output == "Question") output = "?";
        if (output == "At") output = "@";
        if (output == "LeftBracket") output = "[";
        if (output == "RightBracket") output = "]";
        if (output == "Backslash") output = "\\";
        if (output == "Caret") output = "^";
        if (output == "Underscore") output = "_";
        if (output == "Backquote") output = "`";
        if (output == "LeftCurlyBracket") output = "{";
        if (output == "RightCurlyBracket") output = "}";
        if (output == "Tilde") output = "~";
        if (output == "Pipe") output = "|";
        return output;
    }

    public string GetKeyStringFromInfo(EInputType eInputType)
    {
        string output = "";
        output = GetUseInputKey(eInputType);
        output = TranslateKeyName(output);
        return output;
    }

    public KeyCode GetKeyFromInfo(EInputType eInputType)
    {
        string output = "";
        output = GetUseInputKey(eInputType);
        KeyCode temp = StringToKeyCode(output);
        return temp;
    }

    public string GetUseInputKey(EInputType eInputType)
    {
        if (inputInfos.ContainsKey(eInputType) == false)
            return string.Empty;

        return inputInfos[eInputType].useKey;
    }

    public void SetInfoTempKey(EInputType eInputType, KeyCode kCode)
    {
        if (inputInfos.ContainsKey(eInputType) == false)
            return;

        inputInfos[eInputType].tempKey = kCode.ToString();
    }

    public void SetInputBlock(bool isBlock)
    {
        isInputBlock = isBlock;
    }
}

public class InputKeyInfo
{
    public string defaultKey = string.Empty;
    public string useKey = string.Empty;
    public string tempKey = string.Empty;
}
