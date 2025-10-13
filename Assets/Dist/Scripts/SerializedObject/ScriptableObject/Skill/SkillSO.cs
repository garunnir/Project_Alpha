
namespace Garunnir.Runtime.ScriptableObject
{
    using UnityEngine;
    using PixelCrushers.DialogueSystem;
    using System.Text;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    public abstract class SkillSO : ScriptableObject
    {
        [Naming]
        public string displayName;
        protected StringBuilder log=new StringBuilder();
#if UNITY_EDITOR
        private void OnDisable()
        {
            EditorUtility.SetDirty(this);//임시
            AssetDatabase.SaveAssetIfDirty(this);
        }
#endif
        //스킬을 담고 실행할때 작동한다.
        public abstract void Excute();
        public void DealDamage(Actor actor,int damage)
        {
            PlusIntValue(actor,ConstDataTable.Actor.Status.Hp, damage);
            RecordLog($"{actor.Name}에 {damage} 피해를 입혔다.");
        }
        public void PlusIntValue(Actor actor,string key,int value)
        {
            int intval = Field.LookupInt(actor.fields, key);
            Field.SetValue(actor.fields, key, intval + value);
        }
        public void GetEquipment()
        {
            //장비정보를 가져온다.
        }
        public void RecordLog(string value)
        {
            //행동정보 기록한다.
            log.AppendLine(value);
        }
    }
    public class NamingAttribute : PropertyAttribute
    {
        public NamingAttribute() { }
    }
#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(NamingAttribute))]
    public class DrawButton : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.stringValue.Length == 0)
            {
                string[] str=property.serializedObject.targetObject.name.Split('.');
                property.stringValue = str.Length > 1 ? str[1] : str[0];
            }
            GUI.Label(position,"Name");
            position.x += position.width / 2;
            position.width -= position.width / 2;
            property.stringValue=GUI.TextField(position,property.stringValue);
        }
    }
#endif
}
