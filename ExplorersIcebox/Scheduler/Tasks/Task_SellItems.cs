using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Throttlers;
using ExplorersIcebox.Enums;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;
using Callback = ECommons.Automation.Callback;

namespace ExplorersIcebox.Scheduler.Tasks;

internal static class Task_SellItems
{
    // Item we last fired a shipping-dialog confirmation for. Used to keep us from
    // firing the next item's amount into the previous item's still-closing dialog.
    private static int lastFiredItemId;
    private static bool quantitySet;

    public static void Enqueue()
    {
        var baseDict = EmbedRoutes.BaseRoutes["Base -> Shopkeep"];
        var waypoints = baseDict.Waypoints;
        var dataId = baseDict.TargetId;

        P.taskManager.Enqueue(() => MoveToNpc(waypoints), "Moving to NPC");
        P.taskManager.Enqueue(() => TargetV2(dataId), $"Target task: {dataId}");
        P.taskManager.Enqueue(() => InteractShopKeep(dataId), "Interacting w/ the material seller vendor");
        P.taskManager.Enqueue(() => SellToNpcV2(), "Selling all the items to the npc");
        P.taskManager.EnqueueDelay(16, true);
        P.taskManager.Enqueue(() => LeaveNPC(waypoints), "Leaving the NPC");
    }

    internal static bool? MoveToNpc(List<Vector3> List)
    {
        // Insert the logic here post return to move to NPC
        var LastWP = List.Count - 1;
        if (PlayerHelper.GetDistanceToPlayer(List[LastWP]) < 0.5f)
        {
            return true;
        }
        if (!P.navmesh.IsRunning())
        {
            if (EzThrottler.Throttle("Telling navmesh to move to spot"))
            {
                P.navmesh.MoveTo(new(List), false);
            }
        }

        return false;
    }

    internal static unsafe bool? InteractShopKeep(ulong dataId)
    {
        if (TryGetAddonByName<AtkUnitBase>("MJIDisposeShop", out var mjiShop) && IsAddonReady(mjiShop))
        {
            return true;
        }
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var menu) && IsAddonReady(menu))
        {
            if (EzThrottler.Throttle("Selecting Export Materials"))
            {
                Callback.Fire(menu, true, 0);
            }
        }
        else
        {
            IGameObject? gameObject = null;
            Utils.TryGetObjectByDataId(dataId, out gameObject);
            if (EzThrottler.Throttle($"Interacting w/ shop seller {dataId}"))
            {
                Utils.InteractWithObject(gameObject);
            }
        }

        return false;
    }

        internal static bool? TargetV2(ulong dataId)
        {
            Utils.TryGetObjectByDataId(dataId, out var gameObject);

            if (gameObject == null || gameObject.IsTarget() || !gameObject.IsTargetable)
                return true;

            if (EzThrottler.Throttle($"Targeting: {dataId}"))
                Utils.TargetgameObject(gameObject);

            return false;
        }
    internal static bool? SellToNpcV2()
    {
        foreach (var item in IslandHelper.SellItems)
        {
            var itemId = item.Key;
            var sellAmount = item.Value;
            if (ItemData.AlwaysIgnoreSell.Contains(itemId))
            {
                continue;
            }
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
        if (agent == null || agent->Data == null || !agent->Data->DataInitialized)
            return false;

        // Shipping dialog is open — confirm the sale
        if (agent->Data->SelectCountAddonHandle != 0 &&
            TryGetAddonByName<AtkUnitBase>("MJIDisposeShopShipping", out var mjiShip) &&
            IsAddonReady(mjiShip))
        {
            if (lastFiredItemId != 0 && lastFiredItemId != itemId)
                return true;

            // Two-phase sell: set quantity on one tick, click confirm on the next.
            // The game needs a frame to process the NumericInput value change.
            if (!quantitySet)
            {
                if (EzThrottler.Throttle($"SetQty {itemId}"))
                {
                    var numNode = mjiShip->GetNodeById(11);
                    if (numNode != null)
                    {
                        var numInput = (AtkComponentNumericInput*)((AtkComponentNode*)numNode)->Component;
                        numInput->SetValue(amount);
                    }
                    agent->Data->CurShipQuantity = amount;
                    quantitySet = true;
                }
            }
            else
            {
                if (EzThrottler.Throttle($"Selling {itemId}"))
                {
                    var confirmNode = mjiShip->GetNodeById(18);
                    if (confirmNode != null)
                    {
                        var evt = confirmNode->AtkEventManager.Event;
                        mjiShip->ReceiveEvent((AtkEventType)25, 4, evt);
                    }
                    IslandHelper.SellItems[itemId] = 0;
                    lastFiredItemId = itemId;
                    quantitySet = false;
                }
            }
            return true;
        }
        // Main shop is open — select the next item to ship
        if (TryGetAddonMaster<MJIDisposeShop>("MJIDisposeShop", out var mjiShop) && mjiShop.IsAddonReady)
        {
            lastFiredItemId = 0;

            // Look up item name directly from the agent's own item data
            // instead of relying on the separate OnPluginLoad dictionary.
            var itemName = FindItemNameInAgent(agent, (uint)itemId);
            if (itemName == null)
            {
                Svc.Log.Warning($"Item {itemId} not found in dispose shop agent data, skipping");
                IslandHelper.SellItems[itemId] = 0;
                return true;
            }

            var entry = mjiShop.ExportItems.Where(x => x.ItemName == itemName).FirstOrDefault();
            if (EzThrottler.Throttle("Selecting the item to ship", 1500))
            {
                if (entry != null)
                    entry.Select();
            }
            return true;
        }

        return false;
    }

    private static unsafe string? FindItemNameInAgent(AgentMJIDisposeShop* agent, uint itemId)
    {
        for (long i = 0; i < agent->Data->Items.LongCount; i++)
        {
            ref var item = ref agent->Data->Items[i];
            if (item.ItemId == itemId)
                return item.Name.ToString();
        }
        return null;
    }

    internal static unsafe bool? LeaveNPC(List<Vector3> list)
    {
        if (PlayerHelper.GetDistanceToPlayer(IslandHelper.BaseStart) < 0.5f)
        {
            Svc.Log.Information("Leave NPC will complete after this");
            SchedulerMain.State = IceBoxState.RunRoute;
            return true;
        }
        if (TryGetAddonByName<AtkUnitBase>("MJIDisposeShop", out var mjiShop) && IsAddonReady(mjiShop))
        {
            if (EzThrottler.Throttle("Closing shop"))
                Callback.Fire(mjiShop, true, 1);
            return false;
        }
        if (!P.navmesh.IsRunning())
        {
            List<Vector3> reverseWp = new(list);

            reverseWp.Reverse();

            if (EzThrottler.Throttle("Telling navmesh to move to spot"))
            {
                P.navmesh.MoveTo(new(reverseWp), false);
            }
            return false;
        }
        return false;
    }
}
