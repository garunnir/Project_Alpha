
//사용되는 부분: 사용자 정보 입력 UI
using Garunnir;
using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//정보를 받아서 보내기만 한다.
public class UISender : MonoBehaviour
{
    UIController mother;   
    public enum Mode
    {
        inputfield, dropdown
    }
    public Mode mode;
    public TMP_Text title;
    public Form0 form0;
    public Form form;
    public TMP_InputField field;
    public TMP_Dropdown dropdown;
    public TMP_Text GetText() { return title; }
    public TMP_InputField GetInput() { return field; }
    public string GetTitle() => title.text;
    public string GetField() => field.text;
    public string[] dropList;
    private void Start()
    {
        if(form==Form.gender&&mode==Mode.dropdown)
        {
            dropdown.options.Clear();
            string[] str=System.Enum.GetNames(typeof(Gender));
            foreach(string str2 in str)
            {
                TMP_Dropdown.OptionData data = new TMP_Dropdown.OptionData();
                data.text= UILocalizationManager.instance.textTable.GetFieldText(GameManager.Instance.GetFormDic(Form.gender,(Gender)System.Enum.Parse(typeof(Gender),str2)));
                dropdown.options.Add(data);
            }
        }
    }
    private void OnEnable()
    {
        title.text = UILocalizationManager.instance.textTable.GetFieldText(GameManager.Instance.GetFormDic(form0, form));
        //mother = FindAnyObjectByType<UIController>();
        //if(mother != null)
        //mother.Act_field += FieldReturn;
        //보낼 대상에 고정한다.
    }
    public string GetValue()
    {
        string rtn=string.Empty;
        switch (mode)
        {
            case Mode.inputfield:
                rtn += Garunnir.Utillity.TextSerializeBuffer.TupleSingle(GetTitle(), true, GetField());
                break;
            case Mode.dropdown:
                rtn += Garunnir.Utillity.TextSerializeBuffer.TupleSingle(GetTitle(), true, dropdown.value);
                break;
        }
        return rtn;
    }
    //void FieldReturn(object o, UIEventArgs e)
    //{

    //    switch (mode)
    //    {
    //        case Mode.inputfield:
    //            //텍스트 테이블로 번역
    //            e.field.Add(title.text, (true,GetField()));
    //            e.sfield += Utillity.TupleSigleConv(GetTitle(), true, GetField());
    //            break;
    //        case Mode.dropdown:
    //            e.field.Add(title.text,(true,dropdown.value));
    //            e.sfield += Utillity.TupleSigleConv(GetTitle(), true, dropdown.value);
    //            break;
    //    }
    //}
    //값을 요청하면 보낸다. 
}
