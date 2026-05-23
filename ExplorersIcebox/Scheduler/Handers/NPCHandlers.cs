using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ExplorersIcebox.Util;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
namespace ExplorersIcebox.Scheduler.Handers;

internal static unsafe class NPCHandlers
{
    internal static bool? InteractShopNpc()
    {
        string OpenedShopAddonName = "ShopExchangeItem";
        IGameObject? target = Svc.Targets.Target;
        if (target != default)
        {
            if (AddonHelper.IsAddonActive("SelectString") || AddonHelper.IsAddonActive("SelectIconString") || AddonHelper.IsAddonActive(OpenedShopAddonName))
                return true;
            TargetSystem.Instance()->InteractWithObject(target.Struct(), false);
        }
        return false;
    }
}
