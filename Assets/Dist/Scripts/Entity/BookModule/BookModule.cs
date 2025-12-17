using Garunnir;
using PixelCrushers.DialogueSystem.Wrappers;
using PixelCrushers.DialogueSystem;
using PixelCrushers.Wrappers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
public class BookModule : MonoBehaviour,ILuaUIAcitate
{
    [SerializeField] CanvasGroupController m_controller;
    [SerializeField] private RectTransform m_descriptionBody;
    [SerializeField] private RectTransform m_elementTemplet;
    [SerializeField] private RectTransform m_keywordsParentLeft;
    [SerializeField] private RectTransform m_keywordsParentRight;
    [Header("Buttons")]
    [SerializeField] private Button m_btn_open;
    [SerializeField] private Button m_btn_close;
    private TMP_Text m_text_description;
    List<KeywordInfo> keywords = new List<KeywordInfo>();

    List<KeywordBox> keywordsSlot = new List<KeywordBox>();

    int currentPage = 0;

    TMP_FontAsset m_fontAsset;
    private void OnEnable()
    {
        ILuaUIAcitate.Active += SetActive;
    }
    public void SetActive(string commend, bool activation)
    {
        if (commend=="KeywordBook")
        {
            if (activation) m_controller.Show();
            else m_controller.Hide();
        }
        else if (commend == "KeywordBookBtn")
        {
            m_btn_open.gameObject.SetActive(activation);
        }
    }
    class KeywordInfo
    {
        public bool IsHighlight = true;
        public string head;
        public string body;
    }
    class KeywordBox
    {
        private static int Curid = 0;
        private RectTransform m_mother;
        private RectTransform m_Descriptionbody;
        private TMP_Text m_keyword;
        private TMP_Text m_description;
        private Button m_btn_ask;
        private Button m_btn_opendescription;
        private Image m_highlight;
        private readonly int id;
        
        public RectTransform GetMother() => m_mother;
        public KeywordBox(RectTransform mother, RectTransform descriptionBody, TMP_Text head, TMP_Text description, Button askBtn, Button activation, Image highLight)
        {
            id = Curid++;
            m_mother= mother;
            m_description = description;
            m_btn_opendescription = activation;
            m_highlight = highLight;
            m_btn_ask= askBtn;
            m_keyword = head;
            m_Descriptionbody=descriptionBody;
        }
        public void SetDescription(KeywordInfo value)
        {
            m_highlight.gameObject.SetActive(value.IsHighlight);
            m_keyword.text = value.head;
            m_btn_opendescription.onClick.RemoveAllListeners();
            m_btn_opendescription.onClick.AddListener(OpenDiscription(value));
        }
        public void SetAskTo(UnityAction<string> action)
        {
            m_btn_ask.onClick.RemoveAllListeners();
            m_btn_ask.onClick.AddListener(()=>action?.Invoke(m_keyword.text));
        }
        UnityAction OpenDiscription(KeywordInfo value)
        {
            return () =>
            {
                if (m_Descriptionbody.gameObject.activeSelf && Curid == id)//캔버스가 켜져있고 이게 열려있으면 닫음
                {
                    m_Descriptionbody.gameObject.SetActive(false);
                    Curid = -1;
                    m_description.text = "null";
                }
                else if (m_Descriptionbody.gameObject.activeSelf && Curid != id)//캔버스가 켜져있고 이게 꺼져있으면 켬
                {
                    value.IsHighlight = false;
                    m_description.text = value.body;
                    Curid = id;
                }
                else
                {
                    value.IsHighlight = false;
                    m_Descriptionbody.gameObject.SetActive(true);
                    m_description.text = value.body;
                    Curid = id;
                }
                m_highlight.gameObject.SetActive(value.IsHighlight);
            };
        }
    }
  
    private void Awake()
    {
        m_fontAsset = UILocalizationManager.instance.localizedFonts.GetTextMeshProFont(Localization.language);
        m_btn_close?.onClick.AddListener(m_controller.Hide);
        m_btn_open?.onClick.AddListener(m_controller.Show);
        m_controller.AddHideAct += AddHideAct;
        m_controller.AddShowAct += AddShowAct;
    }
    private void Start()
    {
        //tmp.font=textStyle.font; 
        //tmp.fontSize=textStyle.fontSize;
        //int maxCharacterCount = CalculateMaxCharacterCount(text, textStyle, rect);

        //GUILayout.BeginArea(rect);
        //GUILayout.Label("Max Characters: " + maxCharacterCount, textStyle);
        //GUILayout.EndArea();
        ////
        //Rect fittingRect = CalculateFittingRect(text, textStyle, new Rect(10, 10, 200, 200));
        //discriptionBody.offsetMax = new Vector2( fittingRect.x+fittingRect.width,fittingRect.y+fittingRect.height);
        //discriptionBody.offsetMin = new Vector2(fittingRect.x,fittingRect.y);
        //GUI.Label(fittingRect, text, textStyle);
        //구현할 것. 텍스트가 쓰여지고 머리에 해당하는 부분에 
        //설명을 넣을 공간이 좀 애매한데........
        //대화칸 옆에 화살표를 만들어서 상세 디스크립션을 나타내도록 하는게 좋을것같음!
        //디스크립션 표시를 위해서는 자신의 부모렉트의 영향을 벗어나는
        //절대랙트값을 가져올수 있어야 한다.
        //어떻게 개별로 디스크립션을 지정할 것인가?
        m_text_description = m_descriptionBody.GetComponentInChildren<TMP_Text>();
        InitBookKeys(20);
        //AddKeyWord("ddddd", "AAaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        //AddKeyWord("dsdfdddd", "AAaaaaaaaaaaaaaaaaaaaaaaaaaasdfaaaaaaaaaaaaaaaaaaaa");
        //AddKeyWord("1", "1");
        //AddKeyWord("2", "2");
        //AddKeyWord("13", "3");
        //AddKeyWord("13", "14");
        //AddKeyWord("dsdffdffddd", "AAaaaaaaaaaaaafaaffaaaaaaaaaaaasdfaaaaaaaaaaaaaaaaaaaa");
        //AddKeyWord("12", "1123");
        //AddKeyWord("11", "1123");
        //AddKeyWord("132", "231");
        //AddKeyWord("1123213", "1231231");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("dsdffdddd", "AAaaaaaaaaaaaafaaaaaaaaaaaaaasdfaaaaaaaaaaaaaaaaaaaa");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123213", "1231231");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("1123", "1123");
        //AddKeyWord("11", "131");
        //AddKeyWord("31", "13");
        ShowPage(0);
        m_controller.HideForce();
    }
    public void NextPage()
    {
        currentPage++;
        if (currentPage * keywordsSlot.Count > keywords.Count)
        {
            currentPage = 0;
        }
        ShowPage(currentPage);
    }
    public void PrevPage()
    {
        currentPage--;
        if (currentPage < 0)
        {
            int testpage = 0;
            while (testpage * keywordsSlot.Count < keywords.Count && testpage < 100)
            {
                testpage++;
            }
            currentPage = Mathf.Max(0, testpage - 1);
        }
        ShowPage(currentPage);
    }

    public void ShowPage(int page)
    {
        int slotcount = keywordsSlot.Count;

        for (int i = 0; i < slotcount; i++)
        {
            if (keywords.Count > i + slotcount * page)
            {
                keywordsSlot[i].GetMother().gameObject.SetActive(true);
                keywordsSlot[i].SetDescription(keywords[i + slotcount * page]);
                keywordsSlot[i].SetAskTo(CustomDialogueSystemController.instance.AskToChar);
            }
            else
            {
                keywordsSlot[i].GetMother().gameObject.SetActive(false);
            }
        }

    }
    public void RePaint()
    {
        ShowPage(currentPage);
    }
    public void ToggleBook()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
    public void AddKeyWord(string keyWord, string description,bool overWrite)
    {
        if(!overWrite)
        {
            KeywordInfo info = keywords.FirstOrDefault(x => x.head == keyWord);

            if (info != null)
            {
                info.body += '\n' + description;
                info.IsHighlight = true;
            }
            else
            {
                keywords.Add(new KeywordInfo() { head = keyWord, body = description, IsHighlight = true });
            }
        }
        else
        {
            keywords.Add(new KeywordInfo() { head = keyWord, body = description, IsHighlight = true });
        }

    }
    private void InitBookKeys(int count)
    {
        RectTransform CreateItem(RectTransform parent)
        {
            RectTransform item = Instantiate(m_elementTemplet);
            item.gameObject.SetActive(true);
            Button askBtn = item.GetComponentsInChildren<Button>()[0];
            Button btn = item.GetComponentsInChildren<Button>()[1];
            TMP_Text text = item.GetComponentInChildren<TMP_Text>();
            Image img = item.Find("Img_Highlight").GetComponent<Image>();
            text.font = m_fontAsset;
            text.text = "null";
            InitDiscription();
            //btn.onClick.AddListener(() => InitDiscription());
            item.transform.SetParent(parent);
            item.transform.localScale = Vector3.one;

            keywordsSlot.Add(new KeywordBox(item, m_descriptionBody, text, m_text_description, askBtn, btn, img));
            return item;
        }
        for (int i = 0; i < count; i++)
        {
            //bool stop=(headTemplet.rect.height * keywordsParentLeft.childCount > keywordsParentLeft.rect.height) 
            //    ? ((headTemplet.rect.height * keywordsParentLeft.childCount > keywordsParentLeft.rect.height)? true: CreateItem(keywordsParentLeft)) : CreateItem(keywordsParentLeft);

            //if (stop) return;
            if (m_elementTemplet.rect.height * m_keywordsParentLeft.childCount > m_keywordsParentLeft.rect.height)
            {
                if (m_elementTemplet.rect.height * m_keywordsParentRight.childCount > m_keywordsParentRight.rect.height)
                {
                    Debug.Log("CreateFailed");
                    return;
                }
                else
                {
                    CreateItem(m_keywordsParentRight);
                }
            }
            else
            {
                CreateItem(m_keywordsParentLeft);
            }
        }
    }
    void InitDiscription()
    {
        //m_descriptionBody.gameObject.SetActive(true);
        RectTransform rect = GameManager.Instance.GetUIManager().GetUpperRT();
        // Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(transform.parent.GetComponent<RectTransform>(),rect );
        //Rect r=RectTransformUtility.(rect, GetComponentInParent<Canvas>());
        //discriptionBody.rect.Set(r.x,r.y,r.width,r.height);
        //Utillity.CopyDifParentRect(rect,discriptionBody);
        UIManager.CopyValuesCover(m_descriptionBody, rect);
        m_text_description.font = UILocalizationManager.instance.localizedFonts.GetTextMeshProFont(Localization.language);
    }
    private int CalculateMaxCharacterCount(string text, GUIStyle style, Rect rect)
    {
        GUIContent content = new GUIContent(text);
        float textHeight = style.CalcHeight(content, rect.width);
        int maxCharacterCount = Mathf.FloorToInt(text.Length * (rect.height / textHeight));
        return maxCharacterCount;
    }

    private Rect CalculateFittingRect(string text, GUIStyle style, Rect baseRect)
    {
        GUIContent content = new GUIContent(text);
        float textHeight = style.CalcHeight(content, baseRect.width);
        int lines = Mathf.CeilToInt(textHeight / style.lineHeight);

        Rect fittingRect = new Rect(baseRect.x, baseRect.y, baseRect.width, style.lineHeight * lines);
        return fittingRect;
    }
    public IEnumerator AddHideAct()
    {
        print("hide");
        m_btn_open?.gameObject.SetActive(true);
        m_btn_close?.gameObject.SetActive(false);
        yield return null;
    }

    public IEnumerator AddShowAct()
    {
        print("show");
        m_btn_open?.gameObject.SetActive(false);
        m_btn_close?.gameObject.SetActive(true);
        yield return null;
    }

}
