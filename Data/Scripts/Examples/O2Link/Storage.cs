using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace TSUT.O2Link
{
    public static class Storage
    {
        public static void SaveBlockState(IMyFunctionalBlock block, bool enabled)
        {
            if (block.Storage == null)
            {
                block.Storage = new MyModStorageComponent();
            }
            // MyAPIGateway.Utilities.ShowMessage("O2Link", $"Saving block {block.CustomName} Enabled state as {(enabled ? "1" : "0")}");
            block.Storage.SetValue(Config.EnabledStorageGuid, enabled ? "1" : "0");
        }

        public static bool LoadBlockState(IMyFunctionalBlock block)
        {
            if (block.Storage == null)
            {
                // MyAPIGateway.Utilities.ShowMessage("O2Link", $"Block {block.CustomName} has no storage, returning Enabled={block.Enabled}");
                return block.Enabled;
            }
            string value;
            if (block.Storage.TryGetValue(Config.EnabledStorageGuid, out value))
            {
                // MyAPIGateway.Utilities.ShowMessage("O2Link", $"Block {block.CustomName} loaded stored Enabled value: {value}");
                return value == "1";
            }
            return block.Enabled;
        }
    }
}