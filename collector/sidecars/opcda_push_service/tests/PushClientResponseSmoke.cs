using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpcDaPushService
{
    // Minimal protocol regression test. Compile this file together with the service
    // and select this Main via /main:OpcDaPushService.PushClientResponseSmoke.
    internal static class PushClientResponseSmoke
    {
        private static int Main()
        {
            MethodInfo parser = typeof(PushClient).GetMethod(
                "ParseResponse",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (parser == null)
            {
                return 10;
            }

            List<int> partial = (List<int>)parser.Invoke(
                null,
                new object[]
                {
                    "{\"batch_id\":\"b1\",\"accepted\":1,\"rejected\":1," +
                    "\"rejected_indices\":[1],\"errors\":[\"failed\"]}",
                    2,
                    "b1",
                });
            if (partial.Count != 1 || partial[0] != 1)
            {
                return 11;
            }

            // A 2xx response carrying errors must not be treated as a full success.
            List<int> errored = (List<int>)parser.Invoke(
                null,
                new object[]
                {
                    "{\"batch_id\":\"b1\",\"accepted\":2,\"rejected\":0," +
                    "\"errors\":[\"unexpected\"]}",
                    2,
                    "b1",
                });
            return errored.Count == 2 && errored[0] == 0 && errored[1] == 1 ? 0 : 12;
        }
    }
}
