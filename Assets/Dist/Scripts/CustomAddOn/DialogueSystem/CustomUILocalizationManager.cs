using PixelCrushers;
using PixelCrushers.DialogueSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomUILocalizationManager : UILocalizationManager
{
    public TextTable ActorTable => (TextTable)additionalTextTables[1];
    public TextTable KeywordTable => (TextTable)additionalTextTables[0];

    private static CustomUILocalizationManager s_instance = null;
    private static bool s_isQuitting = false;

    /// <summary>
    /// Current global instance of UILocalizationManager. If one doesn't exist,
    /// a default one will be created.
    /// </summary>
    new public static CustomUILocalizationManager instance
    {
        get
        {
            if (s_instance == null && !s_isQuitting)
            {
                s_instance = GameObjectUtility.FindFirstObjectByType<CustomUILocalizationManager>();
                if (s_instance == null && Application.isPlaying)
                {
                    var globalTextTable = GameObjectUtility.FindFirstObjectByType<GlobalTextTable>();
                    s_instance = (globalTextTable != null) ? globalTextTable.gameObject.AddComponent<CustomUILocalizationManager>()
                        : new GameObject("UILocalizationManager").AddComponent<CustomUILocalizationManager>();
                }
            }
            return s_instance;
        }
        set
        {
            s_instance = value;
        }
    }
    public string AddLoc(string text)
    {
        if (Localization.language == Localization.GetLanguage(SystemLanguage.Korean))
        {
            //Debug.Log("lang!");
            text = Myevan.Korean.ReplaceJosa(text);
            //Debug.Log(text);
            return text;
        }
        else
        {
            return text;
        }
    }
    private void Awake()
    {
        if (textTable == null) return;
        currentLanguage = Localization.GetLanguage(SystemLanguage.Korean);
        TextTable.currentLanguageID = textTable.GetLanguageID(currentLanguage);
    }
    public string GetLocText(string locTablekey, TextTable textTable)
    {
        int locid = Localization.GetCurrentLanguageID(this.textTable);
        var field = textTable.GetField(locTablekey);
        if (field == null) return $"{Localization.language}: Not exist Localization Field Data";
        else return field.HasTextForLanguage(locid) ? field.GetTextForLanguage(locid) : $"{Localization.language}: Not exist Localization Text Data";
    }
    public string GetLocText(string locTablekey)
    {
        TextTable textTable = null;
        if (locTablekey.Contains("Actor."))
        {
            textTable = ActorTable;
        }
        else if (locTablekey.Contains("Keyword."))
        {
            textTable = KeywordTable;
        }
        else
        {
            textTable = (TextTable)this.textTable;
        }
        return GetLocText(locTablekey, textTable);
    }
}
