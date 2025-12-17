#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using System.ComponentModel;
using Garunnir;
using UnityEngine.UI;

namespace PixelCrushers.CustomExtention
{
    public static class Extention
    {
        public static Vector2 GetAlignment(this Actor actor)
        {
            if(Field.FieldExists(actor.fields, ConstDataTable.Actor.Status.Alignment))
            {
                string[] value = Field.LookupValue(actor.fields, ConstDataTable.Actor.Status.Alignment).Split('/');
                // '/'로 구분한다.
                return new Vector2(float.Parse(value[0]), float.Parse(value[1]));
            }
            else
            {
                Field.SetValue(actor.fields, ConstDataTable.Actor.Status.Alignment, "0/0");
                return Vector2.zero;
            }

        }
        public static void SetAlignment(this Actor actor, Vector2 vector)
        {
            string inputvalue = $"{vector.x}/{vector.y}";
            Field.SetValue(actor.fields, ConstDataTable.Actor.Status.Alignment, inputvalue);
        }
    }
    public static class DialogueDatabaseExtention
    {
        //기존 데이터베이스를 확장하고 싶은데.
        //리소스 통합했던곳에다가 삽입하는게 좋을것 같음.
        //리소스매니저에 등록할수있게끔 하자. 가저가거나 가져올땐 인덱스로.
        //맵이 다이얼로그 매니저와 같이 있을 이유가 있어?=>어..아니...


        //public static MapContainer GetMap(this DialogueDatabase data)
        //{
        //    var vari=data.GetVariable(ConstDataTable.Map.designation);
        //    Field.LookupInt(vari.fields, ConstDataTable.Map.designation);
        //    return GameManager.Instance.GetResourceManager().GetMap();
        //}
    }

}
#endif
