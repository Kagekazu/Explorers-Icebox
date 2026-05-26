using ECommons.Throttlers;
using ExplorersIcebox.Enums;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Collections.Generic;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;
using Callback = ECommons.Automation.Callback;

namespace ExplorersIcebox.Scheduler.Tasks;

internal static class Task_SellItems
{
    private const string DisposeShopAddon = "MJIDisposeShop";
    private const string ShippingAddon = "MJIDisposeShopShipping";
    private const string SelectStringAddon = "SelectString";

    // Item we last confirmed via the shipping callback; cleared when the shipping addon closes.
    private static int lastShippedItemId;

    public static void Enqueue()
    {
        lastShippedItemId = 0;
        var baseDict = EmbedRoutes.BaseRoutes["Base -> Shopkeep"];
        var waypoints = baseDict.Waypoints;
        var dataId = baseDict.TargetId;

        P.taskManager.Enqueue(() => MoveToNpc(waypoints), "Moving to NPC");
        P.taskManager.Enqueue(() => TargetShopkeeper(dataId), $"Target task: {dataId}");
        P.taskManager.Enqueue(() => OpenExportShop(dataId), "Interacting w/ the material seller vendor");
        P.taskManager.Enqueue(() => SellAllItems(), "Selling all the items to the npc");
        P.taskManager.EnqueueDelay(16, true);
        P.taskManager.Enqueue(() => LeaveNPC(waypoints), "Leaving the NPC");
    }

    internal static bool? MoveToNpc(List<Vector3> waypoints)
    {
        var lastWp = waypoints.Count - 1;
        if (PlayerHelper.GetDistanceToPlayer(waypoints[lastWp]) < 0.5f)
            return true;

        if (!P.navmesh.IsRunning() && EzThrottler.Throttle("Telling navmesh to move to spot"))
            P.navmesh.MoveTo(new(waypoints), false);

        return false;
    }

    internal static unsafe bool? OpenExportShop(ulong dataId)
    {
        if (IsDisposeShopReady(AgentMJIDisposeShop.Instance()))
            return true;

        if (AddonHelper.TryGetActiveAddon(SelectStringAddon, out var menu))
        {
            if (EzThrottler.Throttle("Selecting Export Materials"))
                Callback.Fire(menu, true, 0);
        }
        else if (EzThrottler.Throttle($"Interacting w/ shop seller {dataId}"))
        {
            Utils.TryGetObjectByDataId(dataId, out var gameObject);
            Utils.InteractWithObject(gameObject);
        }

        return false;
    }

    internal static bool? TargetShopkeeper(ulong dataId)
    {
        Utils.TryGetObjectByDataId(dataId, out var gameObject);

        if (gameObject == null || gameObject.IsTarget() || !gameObject.IsTargetable)
            return true;

        if (EzThrottler.Throttle($"Targeting: {dataId}"))
            Utils.TargetgameObject(gameObject);

        return false;
    }

    internal static bool? SellAllItems()
    {
        foreach (var (itemId, sellAmount) in IslandHelper.SellItems)
        {
            if (sellAmount == 0)
                continue;

            if (TryToSellItem(itemId, sellAmount))
                return false;
        }

        return true;
    }

    private static unsafe bool TryToSellItem(int itemId, int amount)
    {
        var agent = AgentMJIDisposeShop.Instance();
        if (!IsDisposeShopReady(agent))
            return false;

        var data = agent->Data;
        if (!TryFindAgentItem(agent, (uint)itemId, out var agentItem))
        {
            Svc.Log.Warning($"Item {itemId} not found in dispose shop agent data, skipping");
            IslandHelper.SellItems[itemId] = 0;
            return true;
        }

        var shippingOpen = data->SelectCountAddonHandle != 0
            && AddonHelper.IsAddonActive(ShippingAddon);

        // Wait for the previous item's shipping addon to close before starting the next one.
        if (lastShippedItemId != 0 && lastShippedItemId != itemId)
            return true;

        if (lastShippedItemId == itemId && !shippingOpen)
        {
            IslandHelper.SellItems[itemId] = 0;
            lastShippedItemId = 0;
            return false;
        }

        if (shippingOpen && AddonHelper.TryGetActiveAddon(ShippingAddon, out var mjiShip))
        {
            if (lastShippedItemId != itemId && EzThrottler.Throttle($"Selling {itemId}"))
            {
                data->CurShipItemIndex = agentItem.ItemIndex;
                data->CurShipQuantity = amount;
                Callback.Fire(mjiShip, true, 11, amount);
                lastShippedItemId = itemId;
            }

            return true;
        }

        if (TryGetAddonMaster<MJIDisposeShop>(DisposeShopAddon, out var mjiShop) && mjiShop.IsAddonReady)
        {
            var itemName = agentItem.Name.ToString();
            var entry = mjiShop.ExportItems.FirstOrDefault(x => x.ItemName == itemName);
            if (EzThrottler.Throttle("Selecting the item to ship", 1500) && entry != null)
                entry.Select();

            return true;
        }

        return false;
    }

    internal static unsafe bool? LeaveNPC(List<Vector3> waypoints)
    {
        if (PlayerHelper.GetDistanceToPlayer(IslandHelper.BaseStart) < 0.5f)
        {
            Svc.Log.Information("Leave NPC will complete after this");
            SchedulerMain.State = IceBoxState.RunRoute;
            return true;
        }

        if (AddonHelper.TryGetActiveAddon(DisposeShopAddon, out var mjiShop))
        {
            if (EzThrottler.Throttle("Closing shop"))
                Callback.Fire(mjiShop, true, 1);
            return false;
        }

        if (!P.navmesh.IsRunning())
        {
            var reverseWp = new List<Vector3>(waypoints);
            reverseWp.Reverse();

            if (EzThrottler.Throttle("Telling navmesh to move to spot"))
                P.navmesh.MoveTo(new(reverseWp), false);
        }

        return false;
    }

    private static unsafe bool IsDisposeShopReady(AgentMJIDisposeShop* agent) =>
        agent != null
        && agent->Data != null
        && agent->Data->InitializationState == 3
        && agent->Data->DataInitialized;

    private static unsafe bool TryFindAgentItem(
        AgentMJIDisposeShop* agent,
        uint itemId,
        out AgentMJIDisposeShop.ItemData agentItem)
    {
        for (var i = 0; i < agent->Data->Items.LongCount; i++)
        {
            ref var item = ref agent->Data->Items[i];
            if (item.ItemId == itemId)
            {
                agentItem = item;
                return true;
            }
        }

        agentItem = default;
        return false;
    }
}
