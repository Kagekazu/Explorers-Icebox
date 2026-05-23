using ExplorersIcebox.Util;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ExplorersIcebox.Ui.DebugWindowTabs;

internal class MJIShopUi
{
    public static void Draw()
    {
        if (TryGetAddonMaster<MJIDisposeShop>("MJIDisposeShop", out MJIDisposeShop mjishop) && mjishop.IsAddonReady)
        {
            if (ImGui.BeginTable("MJI Shop Item Demo", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("ItemId");
                ImGui.TableSetupColumn("Item Name");
                ImGui.TableSetupColumn("Value");
                ImGui.TableSetupColumn("Inventory Amount");
                ImGui.TableSetupColumn("Allocated Amount");
                ImGui.TableSetupColumn("Select Item");

                ImGui.TableHeadersRow();

                for(int i = 0; i < mjishop.NumEntries; i++)
                {
                    MJIDisposeShop.ExportShopInfo entry = mjishop.ExportItems[i];
                    string itemName = entry.ItemName;
                    uint Value = entry.Value;
                    uint InventoryAmount = entry.Inventory;
                    uint allocatedAmount = entry.Allocated;
                    int itemId = OnPluginLoad.IslandItemInfo.Where(x => x.Value == itemName).FirstOrDefault().Key;

                    ImGui.TableNextRow();
                    ImGui.PushID(itemName);

                    ImGui.TableSetColumnIndex(0);
                    if (itemId != 0)
                    {
                        ImGui.Text($"{itemId}");
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text($"{itemName}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{Value}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{InventoryAmount}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{allocatedAmount}");

                    ImGui.TableNextColumn();
                    if (ImGui.Button("Select Item"))
                    {
                        entry.Select();
                    }
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }
    }
}
