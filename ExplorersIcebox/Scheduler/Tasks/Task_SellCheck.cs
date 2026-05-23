using ExplorersIcebox.Enums;
using ExplorersIcebox.Util;
namespace ExplorersIcebox.Scheduler.Tasks;

internal static class Task_SellCheck
{
    internal static bool SellToShop;

    public static void Enqueue()
    {
        P.taskManager.Enqueue(() => SellCheck(), "Checking if need to sell to vendor");
    }

    internal static bool? SellCheck()
    {
        Svc.Log.Information("Starting Sell Check");
        IslandHelper.SellItems.Clear();
        SellToShop = false;
        var LoopCount = Math.Min(IslandHelper.GoalLoopAmount, IslandHelper.MaxRouteLoops);
        if (C.RunMaxLoops)
            LoopCount = IslandHelper.MaxRouteLoops;


        IslandHelper.UpdateNumbers();
        foreach (var item in IslandHelper.RouteItems)
        {
            // IgnoreNode only affects loop count calculations, not sell eligibility.
            // Items like Islewort that appear in multi-item nodes should still be
            // sold when they exceed the keep limit.
            if (ItemData.AlwaysIgnoreSell.Contains(item.Value.ItemId))
                continue;

            var itemName = item.Key;
            var gatherAmount = IslandHelper.RouteItems[itemName].Amount;
            var itemId = item.Value.ItemId;

            var ItemSell = IslandHelper.SellAmount(LoopCount, gatherAmount, itemId);
            if (ItemSell > 0)
            {
                IslandHelper.SellItems.Add(itemId, ItemSell);
                SellToShop = true;
            }
        }

        if (C.SkipSell || !SellToShop)
        {
            Svc.Log.Debug($"Skip Sell Enabled? {C.SkipSell}");
            Svc.Log.Debug($"Sell to Shop? {SellToShop}");
            Svc.Log.Debug("Changing state to run route");
            SchedulerMain.State = IceBoxState.RunRoute;
        }
        else if (SellToShop)
        {
            Svc.Log.Debug("Items were found to be sold, swapping to NPC Sell");
            SchedulerMain.State = IceBoxState.SellToNpc;
        }
        else if (C.DryTest)
        {
            Svc.Log.Debug("Dry test was enabled, switching back to idle mode");
            SchedulerMain.State = IceBoxState.Idle;
        }
        else
        {
            Svc.Log.Debug("this shouldn't of happen. Swapping to idle");
            SchedulerMain.State = IceBoxState.Idle;
        }

        Svc.Log.Information($"Sell check is complete. State is: {SchedulerMain.State}");
        return true;
    }
}
