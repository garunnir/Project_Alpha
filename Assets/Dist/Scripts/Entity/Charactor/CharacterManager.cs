using PixelCrushers.DialogueSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Garunnir
{
    //[Serializable]
    //public class Character
    //{
    //    public Actor dialogueActor;
    //    public Core bodyCore;
    //    public Guid guid;
    //    public Dictionary<string, (bool, object)> field { get; private set; }

    //    #region cache
    //    public Texture2D img_profile;
    //    string key_name="Name";
    //    #endregion


    //    public Character(string name,int id)
    //    {
    //        field = new Dictionary<string, (bool, object)>();
    //        guid= Guid.NewGuid();
    //        Debug.Log(guid);
    //    }
    //    public void DeleteField(string key)
    //    {
    //        if(field.ContainsKey(key)) field.Remove(key);
    //    }
    //    public void CreateDefault()
    //    {
    //        bodyCore = BodyFactory.CreateDefault();
    //    }

    //}
    public class CharacterManager:Singleton<CharacterManager>
    {
        public List<Actor> characters = new List<Actor>();
        public static event UnityAction Act_CharLoaded;
        public void Init()
        {
            characters = DialogueManager.databaseManager.masterDatabase.actors;
            //Garam();
            //Act_CharLoaded?.Invoke();
        }
        private void Start()
        {

        }
        Actor MainC()
        {
            Actor cha = characters.Find(x => x.id == ConstDataTable.Actor.PlayerID);
            if (cha != null) return cha;
            cha ??= new Actor();
            //cha.bodyCore ??= BodyFactory.CreateDefault();
            return cha;
        }
        Actor Garam()
        {
            Actor cha = characters.Find(x => x.Name=="Garam");
            cha ??= new Actor();
            //cha.bodyCore ??= BodyFactory.CreateDefault();
            //스테이터스 표시구분은 따로 결정해주는게 좋을것 같음.
            cha.fields.Add(new Field(GameManager.Instance.GetFormDic(Form0.character, Form.profile),"Garam", FieldType.Files));
            cha.fields.Add(new Field(GameManager.Instance.GetFormDic(Form0.character, Form.firstName), "오르카릇트 비돌돌 가람", FieldType.Text));
            cha.fields.Add(new Field(GameManager.Instance.GetFormDic(Form0.character, Form.outerAge), "15", FieldType.Text));
            cha.fields.Add(new Field(GameManager.Instance.GetFormDic(Form0.status, Form.exp), "34/50", FieldType.Text));
            //들어간 순서가 출력순서가 된다.
            return cha;
        }
    }
}
namespace Garunnir.CharacterAppend.BodySystem
{
    public abstract class BodyFactory
    {
        //public abstract void build();
        public abstract Core CreateDefault();
    }
    public class DefaultBodyFactory : BodyFactory
    {
        public override Core CreateDefault()
        {
            HumanoidBody body = new HumanoidBody("head",null);
            BodyParts upperbody = body.SetNext("neck").SetNext<HumanoidBody>("upperBody", new Inner.Organs.Breast("1"));
            BodyParts lhand = upperbody.SetNext("leftshoulder").SetNext("leftupperarm").SetNext("leftelbow").SetNext("leftwrist").SetNext("lhand");
            BodyParts rhand = upperbody.SetNext("rightshoulder").SetNext("rightupperarm").SetNext("rightelbow").SetNext("rightwrist").SetNext("rhand");
            BodyParts pelvis = upperbody.SetNext("velly").SetNext<MechBody>("waist").SetNext<HumanoidBody>("pelvis", new Inner.Organs.Womb("1"));
            BodyParts rfoot = pelvis.SetNext("rupperleg").SetNext("rknee").SetNext("rfoot");
            BodyParts lfoot = pelvis.SetNext("lupperleg").SetNext("lknee").SetNext("lfoot");
            lhand.SetNext("lthumb", "lindexfinger", "lmiddlefinger", "lringfinger", "lpinky");
            rhand.SetNext("rthumb", "rindexfinger", "rmiddlefinger", "rringfinger", "rpinky");
            rfoot.SetNext("rbigtoe", "rsecondtoe", "rthirdtoe", "rforthtoe", "rlittletoe");
            lfoot.SetNext("lbigtoe", "lsecondtoe", "lthirdtoe", "lforthtoe", "llittletoe");


            //조건 파츠교체가 가능할것
            //허리교체를 시도한다
            //Debug.Log(body.Find("waist"));
            //BodyParts mpart= new MechBody("waist");
            //body.Find("waist").Swap(mpart);
            //BodyParts tmpb = body.Find("waist");
            //tmpb.GetField().ContainsKey("DiseaseRate");
            //Debug.Log("" + tmpb.GetField().ContainsKey("DiseaseRate") + tmpb.GetField().ContainsKey("EnergyRate") + tmpb.GetField().ContainsKey("DamageRate"));
            //교체해도 이름은 동일할 것이므로 타입을 넣어서 바꾸는 방법을 고려해봐도 될것같다.
            return body.core;
        }
    }


    public class CoreEventArgs : EventArgs
    {
        public int time { get; set; }
        public DateTime TimeReached { get; set; }
    }
    public class Core
    {
        public List<BodyParts> corelist = new List<BodyParts>();
        public List<BodyParts> partslist = new List<BodyParts>();
        private Dictionary<string, InnerParts> innerDic = new Dictionary<string, InnerParts>();
        public Dictionary<string, InnerParts> GetInner() => innerDic;
        public event EventHandler<CoreEventArgs> coreChanged;
        Guid guid;
        #region Method
        public Core(BodyParts parts)
        {
            guid = Guid.NewGuid();
            corelist.Add(parts);
            Debug.Log(guid);
        }
        private void Core_coreChanged(object sender, CoreEventArgs e)
        {
            //Debug.LogError("core subupdate" +instanceID);
            int orginTime = e.time;
            int atime = GetMinEventTime(e.time);
            e.time = atime;
            coreChanged?.Invoke(sender, e);
            if (atime != e.time)
            {
                e.time = orginTime - atime;
                coreChanged?.Invoke(sender, e);
            }
        }
        public void Update(object obj, CoreEventArgs e)
        {
            //Debug.LogError("coreupdate" + instanceID);
            Core_coreChanged(obj, e);
        }
        public void CoreCommend(string commend)//다른 파츠에 명령전달
        {

        }
        public InnerParts FindInner(string name)
        {
            return innerDic[name];
        }

        public int GetMinEventTime(int time)
        {
            int min = int.MaxValue;
            foreach (var item in innerDic)
            {
                int newv = item.Value.CheckMinEventTime(time);
                if (newv < min)
                {
                    min = newv;
                }
            }
            if (int.MaxValue == min)
            {
                Debug.LogWarning("innerDic is empty");
                return time;
            }
            return min;
        }
        #endregion
        public string GetJsonConvert()
        {
            Utillity.stringBuilder.Append("<Core>");
            Utillity.stringBuilder.Append("</Core>");
            Utillity.stringBuilder.Append("<Parts>\n");
            foreach (var item in partslist)
            {
                item.ToJson();
            }
            Utillity.stringBuilder.Append("</Parts>");
            Utillity.stringBuilder.Append("<Inner>\n");
            foreach (var item in innerDic.Values)
            {
                item.ToJson();
            }
            Utillity.stringBuilder.Append("</Inner>");
            return Utillity.stringBuilder.ToString();
        }
    }
    public abstract class Shape
    {
        public string name { get { return _name; } protected set { /*Debug.Log(_name);*/ _name = value; } }//부위명
        public string _name;
        abstract public void ToJson();
        abstract public void FromJson(string[] strings);
        //public void ObjectParser(ref bool obj,string str)
        //{
        //    obj = bool.Parse(str);
        //}
        //public void ObjectParser(ref float obj,string str)
        //{
        //    obj = float.Parse(str);
        //}
        //public void ObjectParser(ref int obj,string str)
        //{
        //    obj = int.Parse(str);
        //}        
        //public void ObjectParser(ref string obj,string str)
        //{
        //    obj = str;    
        //}
        public void ObjectParser<T>(ref T obj, string str)
        {
            if (obj is bool)
            {
                obj = (T)(object)bool.Parse(str);
            }
            else if (obj is float)
            {
                obj = (T)(object)float.Parse(str);
            }
            else if (obj is int)
            {
                obj = (T)(object)int.Parse(str);
            }
            else
            {
                obj = (T)(object)str;
            }
        }
    }
    public abstract class BodyParts : Shape
    {
        //public Action<int> update;
        public Core core { get; private set; }
        public Dictionary<string, float> field = new Dictionary<string, float>();
        public int durability;
        public List<BodyParts> next = new List<BodyParts>();
        public List<BodyParts> prev = new List<BodyParts>();
        public List<InnerParts> innerParts = new List<InnerParts>();

        #region Method
        public BodyParts()
        {

        }
        public BodyParts(Core core)
        {
            AddCore(core);
        }
        public BodyParts(string name, Core core) : this(core)
        {
            this.name = name;
        }
        //public BodyParts(string name) : this()
        //{
        //    this.name = name;
        //    if(core==null)core= new Core(this);
        //}
        public static BodyParts operator +(BodyParts a, BodyParts b)
        {
            return ConnectLink(a,b);
        }
        public BodyParts(string name, InnerParts inner) : this()
        {
            innerParts.Add(inner);
        }
        void AddCore(Core core)
        {
            this.core = core ?? new Core(this);
            this.core.partslist.Add(this);
        }
        public static BodyParts ConnectLink(BodyParts from,BodyParts to)
        {
            from.next.Add(to);
            to.prev.Add(from);
            return from;
        }
        public T SetNext<T>(string name) where T : BodyParts, new()
        {
            T parts = new T();
            parts.name = name;
            parts.AddCore(this.core);
            ConnectLink(this, parts);
            return parts;
        }
        public T SetNext<T>(string name, InnerParts inner) where T : BodyParts, new()
        {
            T parts = new T();
            parts.core = core;
            parts.name = name;
            parts.AddCore(this.core);
            parts.innerParts.Add(inner);
            inner.Init(parts);
            ConnectLink(this, parts);

            return parts;
        }
        public abstract BodyParts SetNext(string name);
        public abstract BodyParts[] SetNext(params string[] name);
        //BodyParts parts= new BodyParts(name);
        //next.Add(parts);
        //parts.prev.Add(this);
        //return parts;
        //}
        public BodyParts SetLink(BodyParts parts)
        {
            next.Add(parts);
            parts.prev.Add(this);
            return parts;
        }
        public List<BodyParts> GetNext()
        {
            return next;
        }
        public List<BodyParts> GetPrev() => prev;
        public BodyParts Find(string name)
        {
            if (name == this.name)
            {
                return this;
            }
            else
            {
                BodyParts parts;
                //if(next.Count == 0)return null;
                foreach (var part in next)
                {
                    if (part == null)
                    {
                        next.Remove(part);
                    }
                    else
                    {
                        parts = part.Find(name);
                        if (parts != null) return parts;
                    }
                }
                return null;
            }
        }
        public void Swap(BodyParts parts)
        {
            parts.next = this.next;
            parts.prev = this.prev;
            foreach (var part in prev)//전노드의 연결과 쌍방통행이므로 전노드와 다음노드의 참조도 바꿔줘야 한다.
            {
                part.next.Remove(this);
                part.next.Add(parts);
            }
            foreach (var part in next)
            {
                part.prev.Remove(this);
                part.prev.Add(parts);
            }
            next.Clear();
            prev.Clear();
        }
        public Dictionary<string, float> GetField()
        {
            return field;
        }
        #endregion
        public override void ToJson()
        {
            //string json="prev: {";
            //foreach(var part in prev)
            //{
            //    json += part.name;
            //    json += ",";
            //}
            //json += "}";
            //json += "next: {";
            //foreach (var part in next)
            //{
            //    json += part.name;
            //    json += ",";
            //}
            //json += "}";
            Utillity.ConvertToSaver(GameManager.Instance.GetTypeDic(this.GetType()), name, durability);
            Utillity.DicConverter(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.field), field);
            Utillity.ListConverter(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.inner), innerParts);
            Utillity.ListConverter(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.prev), prev);
            Utillity.ListConverter(GameManager.Instance.GetFormDic(Form0.bodyparts, Form.next), next);
            Utillity.stringBuilder.Append(Utillity.divider + Utillity.lf);
        }
        public override void FromJson(string[] strings)
        {
            throw new NotImplementedException();
        }
    }
    [Serializable]
    public class HumanoidBody : BodyParts
    {
        //public List<Organ> organs = new List<Organ>();
        #region constructor
        public HumanoidBody() : base()
        {
            field.Add("DiseaseRate", 0);
            field.Add("DamageRate", 0);
        }
        //start at head
        //public HumanoidBody(string name) : base(name)
        //{
        //    field.Add("DiseaseRate", 0);
        //    field.Add("DamageRate", 0);
        //}
        public HumanoidBody(string name, Core core) : base(name, core)
        {
            field.Add("DiseaseRate", 0);
            field.Add("DamageRate", 0);
        }
        //public HumanoidBody(string name,Organ organ) : this(name)
        //{
        //    SetOrgan(organ);
        //}
        #endregion
        #region method

        public override BodyParts SetNext(string name)
        {
            return SetNext<HumanoidBody>(name);
        }

        public override BodyParts[] SetNext(params string[] name)
        {
            BodyParts[] rtn = new BodyParts[name.Length];
            for (int i = 0; i < name.Length; i++)
            {
                HumanoidBody parts = new HumanoidBody(name[i],core);
                rtn[i] = parts;
                next.Add(parts);
                parts.prev.Add(this);
            }
            return rtn;
        }
        //public HumanoidBody SetNext(string name,Organ organ) 
        //{
        //    HumanoidBody parts = new HumanoidBody(name, organ);
        //    next.Add(parts);
        //    parts.prev.Add(this);
        //    return parts;
        //}
        //public void SetOrgan(Organ organ)
        //{
        //    organs.Add(organ);
        //}
        #endregion
        //public override void ToJson()
        //{
        //    Utillity.ConvertToSaver(GameManager.Instance.GetTypeDic(this.GetType()), name, durability);
        //    base.ToJson();
        //}
    }
    [Serializable]
    public class MechBody : BodyParts
    {
        #region Constructor
        public MechBody() : base()
        {
            field.Add("EnergyRate", 1);        
            field.Add("DamageRate", 0);
        }
        //public MechBody(string name) : base(name)
        //{
        //    field.Add("EnergyRate", 1);
        //    field.Add("DamageRate", 0);
        //}

        public MechBody(string name, Core core) : base(name, core)
        {
            field.Add("EnergyRate", 1);
            field.Add("DamageRate", 0);
        }
        #endregion
        #region Method
        public override BodyParts SetNext(string name)
        {
            BodyParts parts = new HumanoidBody(name,core);
            next.Add(parts);
            parts.prev.Add(this);
            return parts;
        }

        public override BodyParts[] SetNext(params string[] name)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        //public override void ToJson()
        //{
        //    Utillity.ConvertToSaver("BodyParts.Human", name, durability);
        //    base.ToJson();
        //}
    }

    public abstract class InnerParts : Shape
    {
        public BodyParts outerBody;
        public abstract void Init(BodyParts outerBody);
        public abstract int CheckMinEventTime(int time);
        abstract public void CoreAddInnerDic();

        public void AddUpdate(EventHandler<CoreEventArgs> e)
        {
            outerBody.core.coreChanged += e;//통상적용
        }
        public void RemoveUpdate(EventHandler<CoreEventArgs> e)
        {
            outerBody.core.coreChanged -= e;
        }

    }
    public abstract class Organ : InnerParts
    {
        public Organ(string name)
        {
            this.name = "Organ."+GetType().Name + "." + name;
        }
        //public Organ(BodyParts parts):this()
        //{
        //    outerBody = parts;
        //}
        public override void Init(BodyParts outerBody)
        {
            this.outerBody = outerBody;
            outerBody.core.coreChanged += CoreUpdate;
            CoreAddInnerDic();
        }
        public abstract void CoreUpdate(object obj, CoreEventArgs e);

    }
    namespace Inner.Organs
    {
        public class Womb : Organ
        {
            //혈류만 공급되면 작동한다. 장기 단위로 시스템 작동한다.
            public float bloodLv;
            public int level;
            public bool isEnable;
            public float readyFragRate;
            public int menstruationRate = 1;
            public bool actMenstruation;
            public bool canfrag;
            public float semenRate;
            public bool isfrag;
            public Womb(string name) : base(name)
            {

            }
            #region Method
            public override int CheckMinEventTime(int time)//이걸 실행하면 오버시 최소 시간을 반환한다.
            {
                return Mathf.Min(time, actMenstruation ? (int)Mathf.Ceil(readyFragRate / menstruationRate * 2880) : (int)Mathf.Ceil((1-readyFragRate) / menstruationRate * 43200));
            }
            public void Menstruation(int time)
            {
                if (actMenstruation)
                {
                    readyFragRate -= 1f / 2880 * (time) * menstruationRate;
                    canfrag = false;
                    if (readyFragRate < 0) actMenstruation = false;
                }
                else
                {
                    readyFragRate += 1f / 43200 * (time) * menstruationRate;
                    if (canfrag == false)
                    {
                        canfrag = (readyFragRate > 0.5f && readyFragRate < 1);
                    }
                    if (readyFragRate > 1) actMenstruation = true;
                }
                if (semenRate > 0)
                {
                    semenRate -= 1f / 5760 * time * menstruationRate;
                }
                else
                {
                    semenRate = 0;
                }
                if (canfrag)
                    CheckFragnant();
            }
            public void CheckFragnant()
            {
                Breast breast = (Breast)outerBody.core.FindInner("Breast");
                if (breast != null)
                {
                    Debug.Log("~!@!");
                    breast.StartFillUp();
                }

                //if (semenRate > 0||canfrag)
                //{
                //    isfrag = true;
                //    AddUpdate(FragSequence);
                //    //브레이크를 걸면 작동하지 않고 최소시간을 반환한다. 그리고 다시 작동
                //    //이벤트시간을 미리 예측하려면..

                //    Breast breast = (Breast)outerBody.core.FindInner("breast");
                //    if (breast != null)
                //    {
                //        Debug.Log("~!@!");
                //        breast.StartFillUp();
                //    }
                //}
            }
            public void EndFrag()
            {
                if (isfrag)
                {
                    isfrag = false;
                    RemoveUpdate(FragSequence);
                    Breast breast = (Breast)outerBody.core.FindInner("breast");
                    if (breast != null)
                    {
                        Debug.Log("~!@!");
                        breast.EndFillUp();
                    }
                }
            }
            public void FragSequence(object obj, CoreEventArgs e)
            {
                //outerBody.core.corelist[0]
                //breast 검색 코어한테 피드백
                //그냥 직접 명령하자......
                //검색을 어떻게 할지 생각한다.

            }

            public override void CoreAddInnerDic()
            {
                outerBody.core.GetInner().Add(name, this);
            }

            public override void CoreUpdate(object obj, CoreEventArgs e)
            {
                Menstruation(e.time);
            }
            #endregion
            public override void ToJson()
            {
                Utillity.ConvertToSaver(name, bloodLv, level, isEnable, readyFragRate, menstruationRate, actMenstruation, canfrag, semenRate, isfrag);
            }
            public override void FromJson(string[] strings)
            {
                int idx=0;
                ObjectParser(ref bloodLv, strings[idx++]);
                ObjectParser(ref level, strings[idx++]);
                ObjectParser(ref isEnable, strings[idx++]);
                ObjectParser(ref readyFragRate, strings[idx++]);
                ObjectParser(ref menstruationRate, strings[idx++]);
                ObjectParser(ref actMenstruation, strings[idx++]);
                ObjectParser(ref canfrag, strings[idx++]);
                ObjectParser(ref semenRate, strings[idx++]);
                ObjectParser(ref isfrag, strings[idx++]);
                //bloodLv = float.Parse(strings[idx++]);
                //level = float.Parse(strings[idx++]);
                //isEnable = float.Parse(strings[idx++]);
                //readyFragRate = float.Parse(strings[idx++]);
                //menstruationRate = float.Parse(strings[idx++]);
                //actMenstruation = bool.Parse(strings[idx++]);
                //canfrag = bool.Parse(strings[idx++]);
                //semenRate = float.Parse(strings[idx++]);
                //isfrag = bool.Parse(strings[idx++]);

            }
        }
        public class Breast : Organ
        {
            float milkRate;
            int milkml;
            int milkmlMax;
            public int lv;
            public Breast(string name) : base(name)
            {

            }
            #region Method
            public void StartFillUp()
            {
                Debug.LogWarning("fillup");
                AddUpdate(FillUp);
                lv++;
            }
            public void EndFillUp()
            {
                RemoveUpdate(FillUp);
                lv--;
            }

            public override int CheckMinEventTime(int time)
            {
                return time;
            }

            public override void CoreAddInnerDic()
            {
                outerBody.core.GetInner().Add(name, this);
            }
            public override void CoreUpdate(object obj, CoreEventArgs e)
            {
            }
            public void FillUp(object obj, CoreEventArgs e)
            {
                milkRate += 0.001f*e.time;
            }
            #endregion

            public override void ToJson()
            {
                Utillity.ConvertToSaver(name, milkRate, milkml, milkmlMax, lv);
            }

            public override void FromJson(string[] strings)
            {
                int idx = 0;
                ObjectParser(ref milkRate, strings[idx++]);
                ObjectParser(ref milkml, strings[idx++]);
                ObjectParser(ref milkmlMax, strings[idx++]);
            }
        }
    }

}


