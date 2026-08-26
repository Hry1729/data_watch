using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpcDaPushService
{
    // Cache selection regression test. Compile with the service source and select
    // this Main via /main:OpcDaPushService.PushCacheSelectionSmoke.
    internal static class PushCacheSelectionSmoke
    {
        private static int Main()
        {
            AppConfig config = new AppConfig
            {
                PushMode = "changed_only",
                FullSnapshotIntervalMs = 60000,
            };
            OpcDaClient client = new OpcDaClient(config);

            DeviceSpec device = new DeviceSpec
            {
                DeviceId = "agv801",
                OpcDevice = "AGV801",
            };
            GroupSpec groupSpec = new GroupSpec { Name = "ViewMain" };
            ItemBinding item = new ItemBinding
            {
                ItemId = "item-1",
                ClientHandle = 1,
                Point = new PointSpec { Tag = "Tag1", Address = "1", DataType = "Boolean" },
            };
            GroupBinding group = new GroupBinding
            {
                Device = device,
                Group = groupSpec,
            };
            group.ClientHandleMap[1] = item;
            group.Cache[1] = new CachedPoint
            {
                ClientHandle = 1,
                Value = true,
                Quality = 0xC0,
                QualityGood = true,
                Dirty = true,
                OpcTimestamp = DateTime.Now,
            };

            FieldInfo groupsField = typeof(OpcDaClient).GetField(
                "_groups", BindingFlags.NonPublic | BindingFlags.Instance);
            if (groupsField == null)
            {
                return 10;
            }
            List<GroupBinding> groups = (List<GroupBinding>)groupsField.GetValue(client);
            groups.Add(group);

            if (client.GetChangedCachedPoints(true).Count != 1)
            {
                return 11;
            }
            if (client.GetChangedCachedPoints(true).Count != 0)
            {
                return 12;
            }

            // A failed HTTP push restores the dirty marker so the next cycle retries.
            client.MarkAllCachedPointsDirty();
            if (client.GetChangedCachedPoints(true).Count != 1)
            {
                return 13;
            }

            SubscriptionStaRunner runner = new SubscriptionStaRunner(config);
            MethodInfo selector = typeof(SubscriptionStaRunner).GetMethod(
                "ShouldUseFullSnapshot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (selector == null)
            {
                return 14;
            }
            DateTime now = DateTime.Now;
            if (!(bool)selector.Invoke(runner, new object[] { now })
                || (bool)selector.Invoke(runner, new object[] { now.AddMilliseconds(1) }))
            {
                return 15;
            }

            client.Dispose();
            return 0;
        }
    }
}
