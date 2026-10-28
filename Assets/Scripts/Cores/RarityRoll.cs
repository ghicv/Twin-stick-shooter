using UnityEngine;

// Which rarity the next card pick is (silver, gold, diamond), with luck protection instead of pure chance:
// - two silver picks in a row: the next one is gold at least;
// - every gold since the last diamond raises the diamond chance (Diamond Per Gold each: after two golds it is likely);
// - otherwise silver or gold, Silver Share of the time silver.
// One per match (LAN, the host's MatchManager) or per run (single player, the player's CoreBridge).
public class RarityRoll
{
    private readonly float diamondChance;
    private readonly float diamondPerGold;
    private readonly float silverShare;
    private int silversInARow;
    private int goldsSinceDiamond;

    public RarityRoll(float diamondChance, float diamondPerGold, float silverShare)
    {
        this.diamondChance = diamondChance;
        this.diamondPerGold = diamondPerGold;
        this.silverShare = silverShare;
    }

    // A new match or run: no luck saved up.
    public void Reset()
    {
        silversInARow = 0;
        goldsSinceDiamond = 0;
    }

    public CoreInventory.Rarity Next()
    {
        float diamond = Mathf.Min(0.9f, diamondChance + diamondPerGold * goldsSinceDiamond);
        CoreInventory.Rarity rarity;
        if (Random.value < diamond)
            rarity = CoreInventory.Rarity.Diamond;
        else if (silversInARow >= 2 || Random.value >= silverShare)
            rarity = CoreInventory.Rarity.Gold;
        else
            rarity = CoreInventory.Rarity.Silver;

        switch (rarity)
        {
            case CoreInventory.Rarity.Silver:
                silversInARow++;
                break;
            case CoreInventory.Rarity.Gold:
                silversInARow = 0;
                goldsSinceDiamond++;
                break;
            default:
                silversInARow = 0;
                goldsSinceDiamond = 0;
                break;
        }
        return rarity;
    }
}
