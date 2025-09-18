using IronJade.Observer.Core;

public enum BattleNewObserverID
{
    None,
    Attack, //공격시작
    Hit, //공격타격시
    Damaged, //피격시 
    Kill,
    TurnStart,
    TurnEnd,

}


// BattleObserverParam.cs 또는 관련 파일에 추가
public class BattleObserverParam : IObserverParam
{
    public BattleActor Attacker { get; set; }
    public BattleActor Victim { get; set; }
    public int Damage { get; set; }

    public BattleObserverParam(BattleActor attacker, BattleActor victim, int damage = 0)
    {
        Attacker = attacker;
        Victim = victim;
        Damage = damage;
    }
}