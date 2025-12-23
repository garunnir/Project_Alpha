using Garunnir.CharacterAppend.BodySystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Garunnir;
using PixelCrushers.Wrappers;
using System.IO;
using System.Linq;
using System.Text;
using PixelCrushers.DialogueSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.Events;


namespace Garunnir
{
    public enum Form
    {
        none,
        id, field, inner, prev, next,

        profile, firstName, outerAge, exp, nickName, gender, lastName, age

    }
    public enum Form0
    {
        none,
        status,
        character, bodyparts,
    }
    public enum Gender
    {
        none,
        male, female
    }

    public class GameManager : Singleton<GameManager>
    {
        #region DataConfig

        private Dictionary<Type, string> TypeDic;
        public Dictionary<(Enum, Enum), string> FormStrDic { get; private set; }
        //public const string form_cha_name = "Name";
        //public const string form_cha_id = "Id";
        //public const string form_parts_field = "Field";
        //public const string form_parts_inner = "Inner";
        //public const string form_parts_prev = "prev";
        //public const string form_parts_next = "next";
        //public const string form_cha_profile_syntax_show  = "[show]";
        //public const string form_cha_profile_syntax_hide  = "[hide]";
        public static string charProfleImg { get; private set; }
        void DataConfig()
        {
            TypeDic = new Dictionary<Type, string>();
            TypeDic.Add(typeof(MechBody), "Bodyparts.Mech");
            TypeDic.Add(typeof(HumanoidBody), "Bodyparts.Human");

            FormStrDic = new Dictionary<(Enum, Enum), string>();
            FormStrDic.Add((Form0.character, Form.id), "Id");
            FormStrDic.Add((Form0.character, Form.field), "Field");
            FormStrDic.Add((Form0.bodyparts, Form.field), "Field");
            FormStrDic.Add((Form0.bodyparts, Form.inner), "Inner");
            FormStrDic.Add((Form0.bodyparts, Form.prev), "prev");
            FormStrDic.Add((Form0.bodyparts, Form.next), "next");
            FormStrDic.Add((Form0.character, Form.profile), "Character.Profile");
            FormStrDic.Add((Form0.character, Form.firstName), "Character.FirstName");
            FormStrDic.Add((Form0.character, Form.outerAge), "Character.OuterAge");
            FormStrDic.Add((Form0.status, Form.exp), "Status.Exp");
            FormStrDic.Add((Form0.none, Form.none), "Null");
            FormStrDic.Add((Form0.character, Form.nickName), "Character.Profile");
            FormStrDic.Add((Form0.character, Form.lastName), "Character.LastName");
            FormStrDic.Add((Form0.character, Form.gender), "Character.Gender");
            FormStrDic.Add((Form0.character, Form.age), "Character.Age");
            FormStrDic.Add((Form.gender, Gender.male), "Gender.Male");
            FormStrDic.Add((Form.gender, Gender.none), "Gender.None");
            FormStrDic.Add((Form.gender, Gender.female), "Gender.female");
            charProfleImg = Utillity.PathUtility.CombinePath(Application.persistentDataPath, "Char", "CharProfile");
            Utillity.PathUtility.EnsureDirectory(charProfleImg, false);
        }

        public string GetTypeDic(Type type)
        {
            return TypeDic[type];
        }
        public string GetFormDic(Enum form0, Enum form)
        {
            return FormStrDic[(form0, form)];
        }

        #endregion
        #region SO
#if UNITY_EDITOR
        [SerializeField] TextTable localizeTable;
        public TextTable GetLoTable() => localizeTable;
        private void UpdateSO()
        {
            UpdataLocTextTable();
        }
        void UpdataLocTextTable()
        {
            if (UILocalizationManager.instance.textTable != null)
            {
                localizeTable = UILocalizationManager.instance.textTable as TextTable;
            }
            AddTableField(Form0.none, Form.none, "없음");
            AddTableField(Form0.character, Form.firstName, "이름");
            AddTableField(Form0.character, Form.nickName, "별명");
            AddTableField(Form0.character, Form.outerAge, "외관나이");
            AddTableField(Form0.status, Form.exp, "경험치");
            AddTableField(Form0.character, Form.lastName, "성");
            AddTableField(Form0.character, Form.age, "나이");
            AddTableField(Form0.character, Form.gender, "성별");
            AddTableField(Form.gender, Gender.male, "남");
            AddTableField(Form.gender, Gender.female, "여");
            AddTableField(Form.gender, Gender.none, "무성");
        }
        void AddTableField(Enum arg0, Enum arg, string value, string lang = "ko")
        {
            localizeTable.AddField(GetFormDic(arg0, arg));
            localizeTable.SetFieldTextForLanguage(GetFormDic(arg0, arg), lang, value);
        }
#endif
        #endregion
        #region Managers
        [SerializeField] CharacterManager _charactorManager;
        [SerializeField] DialogueSystemController _dialogueSystemController;
        [SerializeField] UIManager _uiManager;
        [SerializeField] ResourceManager _resourceManager;
        [SerializeField] InputManager _inputManager;
        public UIManager GetUIManager() => _uiManager;
        public ResourceManager GetResourceManager() => _resourceManager;
        public InputManager GetInputManager() => _inputManager;
        #endregion
        public static event UnityAction InitDone;



        void LoadCharPicture()
        {
            if (_charactorManager.characters.Count == 0) return;
            var player = _charactorManager.characters.Find(x => x.Name == "Player");
            player.portrait = Utillity.TextureIO.LoadImage(GameManager.charProfleImg + player.id);
        }
        private void LoadChar()
        {
            //SaveSystem.LoadFromSlot(0);
        }
        private void ResourceLoad()
        {
            if (GetResourceManager() == null) return;

            GetResourceManager().ResourceLoadDoneEvent += () => FindAnyObjectByType<DialogueSystemTrigger>()?.OnUse();
            GetResourceManager().LoadAllImg();
            //로드시 시간이 걸리는 데이터들을 미리 로드한다.
            //만들어진 캐릭터 프로필사진들을 로드한다.
            LoadCharPicture();
        }
        private void Init()
        {
            _charactorManager?.Init();
            _uiManager?.Init();

            SetDontDistroy();
            InitDone?.Invoke();
        }
        private void Awake()
        {
            DataConfig();
            Debug.Log("StartGameManagerInit");
            //Init();
#if UNITY_EDITOR
            UpdateSO();
#endif
            LoadChar();
            ResourceLoad();
            Debug.Log("EndGameManagerInit");
        }


        private void StarterInit()
        {
        }

    }

    /// <summary>
    /// 문자열 파싱, 경로 처리, 커스텀 직렬화, 텍스처 로드 등
    /// 프로젝트 전반에서 재사용되는 유틸리티 모음 클래스
    /// </summary>
public static class ExUtillity
{
    public const string divider = Utillity.TextSerializeBuffer.Divider;
    public const string lf = Utillity.TextSerializeBuffer.LF;

    // 기존 접근 유지
    public static StringBuilder stringBuilder => Utillity.TextSerializeBuffer.SB;

    public static T ObjectParser<T>(string str) => Utillity.ParseUtility.ObjectParser<T>(str);

    public static string CombinePath(params string[] path) => Utillity.PathUtility.CombinePath(path);

    public static string CheckFolderInPath(string path, bool isOnlyDir = true)
        => Utillity.PathUtility.EnsureDirectory(path, isOnlyDir);

    public static Texture2D LoadImage(string path) => Utillity.TextureIO.LoadImage(path);

    public static void ConvertToSaver(string head, params object[] objects)
        => Utillity.TextSerializeBuffer.AppendValues(head, objects);

    public static void ListConverter<T>(string head, List<T> list) where T : Shape
    {
        // 기존 로직 유지: Organ이면 Organ으로 찍기
        if (list != null && list.Count > 0 && list[0] is Organ)
        {
            Utillity.TextSerializeBuffer.SB.Append($"{typeof(Organ).Name}.{head}/List:");
        }
        else
        {
            Utillity.TextSerializeBuffer.SB.Append($"{typeof(T).Name}.{head}/List:");
        }

        foreach (var obj in list)
        {
            Utillity.TextSerializeBuffer.SB.Append(obj.name);
            if (obj != list[^1])
                Utillity.TextSerializeBuffer.SB.Append(",");
        }
        Utillity.TextSerializeBuffer.SB.Append(Utillity.TextSerializeBuffer.LF);
    }
}

        
    }

