using PixelCrushers;
using System;

using System.Collections.Generic;

using Garunnir.CharacterAppend.BodySystem;



namespace Garunnir
{
    public class CharactorSaver : Saver
    {
        private Data m_data = new Data();
        [Serializable]
        public class Data
        {
            public List<string> characterData = new List<string>();
        }

        public override void ApplyData(string s)
        {
            if (string.IsNullOrEmpty(s)) return;

            var data = SaveSystem.Deserialize<Data>(s, m_data);
            if (data == null)
                m_data = data;
            print("char::" + data.characterData[0]);
            //Character cha = SplitSolution(data.characterData[0]);
            //print(cha.bodyCore.FindInner("Organ.Breast.1"));
            //GameManager.Instance.characters.Add(cha);
            //SaveSystem.currentSavedGameData.charactors.Add(data.charactor);
            //ComponentUtility.SetComponentEnabled(componentToWatch, data.enabled);
        }
        BodyParts CreateBody(string st)
        {
            return null;
        }
        class SavedLink
        {
            public string[] innerlinks;
            public string[] prevlinks;
            public string[] nextlinks;
        }
        //Character SplitSolution(string s)
        //{
        //    Character tmpchar = null;
        //    Core core = null;
        //    int id = -1;
        //    string[] tmpst = s.Split("<Inner>");
        //    string[] strings = tmpst[0].Split("==");
        //    Dictionary<string, SavedLink> link = new Dictionary<string, SavedLink>();
        //    Dictionary<string, BodyParts> partsDic = new Dictionary<string, BodyParts>();
        //    Dictionary<string, InnerParts> innerDic = new Dictionary<string, InnerParts>();
        //    string[] inn = tmpst[1].Split('\n');
        //    foreach (var item in inn)
        //    {
        //        string[] inner = item.Split(':');
        //        if (inner == null || inner.Length == 0) continue;

        //        if (inner[0].Contains("Organ.Breast"))
        //        {
        //            Breast breast = new Breast(inner[0].Split('.').Last());
        //            innerDic.Add(breast.name, breast);
        //            string[] mems = inner[1].Split(',');
        //            breast.FromJson(mems);

        //        }
        //        else if (inner[0].Contains("Organ.Womb"))
        //        {
        //            Womb womb = new Womb(inner[0].Split('.').Last());
        //            innerDic.Add(womb.name, womb);
        //            string[] mems = inner[1].Split(',');
        //            womb.FromJson(mems);
        //        }
        //    }
        //    for (int i = 0; i < strings.Length; i++)
        //    {
        //        BodyParts parts = null;
        //        SavedLink savedLink = new SavedLink();
        //        string[] innerstrings = strings[i].Split('\n');

        //        for (int j = 0; j < innerstrings.Length; j++)
        //        {
        //            if (innerstrings[j].Contains('<') || innerstrings[j] == string.Empty) continue;
        //            if (i == 0)
        //            {
        //                if (tmpchar == null && id != -1)
        //                {
        //                    tmpchar = new Character("", id);
        //                }
        //                if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.character,Form.id)))
        //                {
        //                    id = int.Parse(innerstrings[j].Split(':')[1]);
        //                }
        //                if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.character, Form.field)))
        //                {
        //                    string[] tmpStrings = innerstrings[j].Split(':')[1].Split(',');
        //                    foreach (var item in tmpStrings)
        //                    {
        //                        string[] strs = item.Split('=');
        //                        string[] strrs = strs[1].Split("|");
        //                        tmpchar.SetField(strs[0], strrs[1], Utillity.ObjectParser<bool>(strrs[0]));
        //                    }
        //                }
        //            }
        //            if (innerstrings[j].Contains(GameManager.Instance.GetTypeDic(typeof(HumanoidBody))))
        //            {
        //                string[] items = innerstrings[j].Split(':')[1].Split(',');
        //                parts = new HumanoidBody(items[0], core);
        //                partsDic.Add(items[0], parts);
        //                core = parts.core;
        //                parts.durability = int.Parse(items[1]);
        //                link.Add(parts.name, savedLink);
        //            }
        //            else if (innerstrings[j].Contains(GameManager.Instance.GetTypeDic(typeof(MechBody))))
        //            {
        //                string[] items = innerstrings[j].Split(':')[1].Split(',');
        //                parts = new MechBody(items[0], core);
        //                partsDic.Add(items[0], parts);
        //                core = parts.core;
        //                parts.durability = int.Parse(items[1]);
        //                link.Add(parts.name, savedLink);
        //            }
        //            if (parts == null)
        //            {
        //                //Debug.LogError("SaveFileError : Can't Find BodyData");
        //            }
        //            else if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.field)))
        //            {
        //                string[] items = innerstrings[j].Split(':')[1].Split(',');
        //                foreach (string part in items)
        //                {
        //                    string[] tmpstr = part.Split("=");
        //                    if (parts.field.ContainsKey(tmpstr[0]))
        //                    {
        //                        parts.field[tmpstr[0]] = float.Parse(tmpstr[1]);
        //                    }
        //                    else
        //                    {
        //                        parts.field.Add(tmpstr[0], float.Parse(tmpstr[1]));
        //                    }
        //                }
        //            }

        //            else if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.inner)))
        //            {
        //                savedLink.innerlinks = innerstrings[j].Split(':')[1].Split(',');
        //            }
        //            else if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.prev)))
        //            {
        //                savedLink.prevlinks = innerstrings[j].Split(':')[1].Split(',');
        //            }
        //            else if (innerstrings[j].Contains(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.next)))
        //            {
        //                savedLink.prevlinks = innerstrings[j].Split(':')[1].Split(',');
        //            }
        //        }
        //    }
        //    if (tmpchar != null)
        //    {
        //        tmpchar.bodyCore = core;
        //        foreach (var item in partsDic.Values)
        //        {
        //            SavedLink tmplink = link[item.name];
        //            if (tmplink.innerlinks != null)
        //                foreach (string v in tmplink.innerlinks)
        //                {
        //                    if (v != string.Empty)
        //                    {
        //                        item.innerParts.Add(innerDic[v]);
        //                        innerDic[v].Init(item);
        //                    }
        //                }
        //            if (tmplink.prevlinks != null)

        //                foreach (string v in tmplink.prevlinks)
        //                {
        //                    if (v != string.Empty) item.prev.Add(partsDic[v]);
        //                }
        //            if (tmplink.nextlinks != null)

        //                foreach (string v in tmplink.nextlinks)
        //                {
        //                    if (v != string.Empty) item.next.Add(partsDic[v]);
        //                }
        //        }
        //    }
        //    return tmpchar;
        //}
        //Character SubStringSolution(string s)
        //{
        //    return null;
        //}
        public override string RecordData()
        {
            //var value = (componentToWatch != null) ? ComponentUtility.IsComponentEnabled(componentToWatch) : false;
            //m_data.enabled = value;
            m_data = new Data();
            foreach (var item in CharacterManager.Instance.characters)
            {
                m_data.characterData.Add(Utillity.GetJsonConvert(item));
            }
            print("convert//" + m_data.characterData);
            return SaveSystem.Serialize(m_data);
        }
    }
}


