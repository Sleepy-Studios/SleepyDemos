namespace Hotfix.HowToFish
{
    /// 首领共用的 HUD 状态；具体动作和物理仍由各首领自己负责。
    public interface IHowToFishBoss
    {
        HowToFishWorldItem Item { get; }
        bool IsFighting { get; }
        float EscapeFraction { get; }
        string Hint { get; }
    }
}
