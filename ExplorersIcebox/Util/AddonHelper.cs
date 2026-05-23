using Dalamud.Game.NativeWrapper;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
namespace ExplorersIcebox.Util;

public static class AddonHelper
{
    public static unsafe bool IsAddonActive(string AddonName) // Used to see if the addon is active/ready to be fired on
    {
        AtkUnitBase* addon = RaptureAtkUnitManager.Instance()->GetAddonByName(AddonName);
        return addon != null && addon->IsVisible && addon->IsReady;
    }

    public static unsafe bool IsNodeVisible(string addonName, params int[] ids)
    {
        AtkUnitBasePtr ptr = Svc.GameGui.GetAddonByName(addonName);
        if (ptr == nint.Zero)
            return false;

        AtkUnitBase* addon = (AtkUnitBase*)ptr.Address;
        AtkResNode* node = GetNodeByIDChain(addon->GetRootNode(), ids);
        return node != null && node->IsVisible();
    }

    public static unsafe string GetNodeText(string addonName, params int[] nodeNumbers)
    {
        AtkUnitBasePtr ptr = Svc.GameGui.GetAddonByName(addonName);

        AtkUnitBase* addon = (AtkUnitBase*)ptr.Address;
        AtkUldManager uld = addon->UldManager;

        AtkResNode* node = null;
        string debugString = string.Empty;
        for(int i = 0; i < nodeNumbers.Length; i++)
        {
            int nodeNumber = nodeNumbers[i];

            ushort count = uld.NodeListCount;

            node = uld.NodeList[nodeNumber];
            debugString += $"[{nodeNumber}]";

            // More nodes to traverse
            if (i < nodeNumbers.Length - 1)
            {
                uld = ((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        if (node->Type == NodeType.Counter)
            return ((AtkCounterNode*)node)->NodeText.ToString();

        AtkTextNode* textNode = (AtkTextNode*)node;
        return textNode->NodeText.GetText();
    }
    public static unsafe AtkTextNode* GetAtkTextNode(string addonName, params int[] nodeNumbers)
    {
        AtkUnitBasePtr ptr = Svc.GameGui.GetAddonByName(addonName);

        AtkUnitBase* addon = (AtkUnitBase*)ptr.Address;
        AtkUldManager uld = addon->UldManager;

        AtkResNode* node = null;
        string debugString = string.Empty;
        for(int i = 0; i < nodeNumbers.Length; i++)
        {
            int nodeNumber = nodeNumbers[i];

            ushort count = uld.NodeListCount;

            node = uld.NodeList[nodeNumber];
            debugString += $"[{nodeNumber}]";

            // More nodes to traverse
            if (i < nodeNumbers.Length - 1)
            {
                uld = ((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        AtkTextNode* textNode = (AtkTextNode*)node;
        return textNode;
    }

    private static unsafe AtkResNode* GetNodeByIDChain(AtkResNode* node, params int[] ids)
    {
        if (node == null || ids.Length <= 0)
            return null;

        if (node->NodeId == ids[0])
        {
            if (ids.Length == 1)
                return node;

            List<int> newList = new(ids);
            newList.RemoveAt(0);

            AtkResNode* childNode = node->ChildNode;
            if (childNode != null)
                return GetNodeByIDChain(childNode, [.. newList]);

            if ((int)node->Type >= 1000)
            {
                AtkComponentNode* componentNode = node->GetAsAtkComponentNode();
                AtkComponentBase* component = componentNode->Component;
                AtkUldManager uldManager = component->UldManager;
                childNode = uldManager.NodeList[0];
                return childNode == null ? null : GetNodeByIDChain(childNode, [.. newList]);
            }

            return null;
        }

        //check siblings
        AtkResNode* sibNode = node->PrevSiblingNode;
        return sibNode != null ? GetNodeByIDChain(sibNode, ids) : null;
    }
}
