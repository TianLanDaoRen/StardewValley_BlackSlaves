using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Objects;

namespace BlackSlaves;

public class AutoHarvester : JunimoHarvester
{
    public int Exp;
    public GameLocation Home;
    private Item? lastHarvestedItem;
    private bool success;

    public AutoHarvester(GameLocation home, int exp)
    {
        Home = home;
        Exp = exp;
        success = false;
    }

    public bool GetAddItemSuccess()
    {
        return success;
    }

    public override void tryToAddItemToHut(Item i)
    {
        lastHarvestedItem = i;
        using var enumerator = Home.Objects.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var kvp = enumerator.Current;
            if ((from item in kvp where item.Value.GetType() == typeof(Chest) select item.Value as Chest)
                .Select(chest => new { chest, color = chest.playerChoiceColor.Value })
                .Where(t =>
                    t.color == DiscreteColorPicker.getColorFromSelection(ModEntry.Config!.CropsChestColorIdx))
                .Select(t => t.chest).All(chest => chest.addItem(i) != null)) continue;
            Game1.player.gainExperience(2, Exp);
            success = true;
            return;
        }
    }

    public Item? GetLastHarvestedItem()
    {
        return lastHarvestedItem;
    }
}