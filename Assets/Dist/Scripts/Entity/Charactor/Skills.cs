using System.Collections;
using System.Collections.Generic;
using UnityEngine;

abstract public class Skills
{

}
public class CharacterSkill : Skills
{
    public string skillName;
    public int dmg;
    public int speed;//속도
    public int hitNum;

    public int useSp;
    public int useHp;
    public int useMp;

    //공격
    public int protectnum;//보호

    public int counter;//반격횟수
    
    
    //회복
    //반격

}
//public class Character
//{
    
//    public string belong;//소속팀
//    public int relationship;//관계
//    public CharacterSkill[] skills;
//    public int hp;
//    public int sp;
//    public int mp;
//}

//public class BattleSimulate
//{

//    void BattleStart(Character[] character)
//    {

//    }
//}
