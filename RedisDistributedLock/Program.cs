using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RedisDistributedLock
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect("redislocal:6379");

            IDatabase conn = muxer.GetDatabase();

            var Tasks = new List<Task>();

            for (var I = 0; I < 100; I++)
            {
                Tasks.Add(DoSomethingWithLockAsync(conn, I));
            }

            await Task.WhenAll(Tasks);

            Console.WriteLine("End");
        }

        private static async Task DoSomethingWithLockAsync(IDatabase conn, int index)
        {
            const string LOCK_NAME = "MyNewLock";

            var token = Guid.NewGuid();

            CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

            if (index == 5)
            {
                //Simulate a cancelation request for index 5
                CancellationTokenSource.Cancel();
            }

            if (await WaitForLockAsync(conn, LOCK_NAME, token.ToString(),
                CancellationTokenSource.Token, TimeSpan.FromSeconds(20)))
            {
                try
                {
                    Console.WriteLine($"Got lock for {index}");

                    //Hold lock
                    await Task.Delay(500);
                }
                finally
                {
                    await conn.LockReleaseAsync(LOCK_NAME, token.ToString());
                }
            }
            else
            {
                Console.WriteLine($"Could not get lock for {index}");
            }
        }

        private static async Task<bool> WaitForLockAsync(IDatabase conn,
            string lockName, string token, CancellationToken cancellationToken, TimeSpan waitTime)
        {
            CancellationTokenSource TimeoutCancellationTokenSource = new CancellationTokenSource(waitTime);

            using (CancellationTokenSource LinkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                  TimeoutCancellationTokenSource.Token, cancellationToken))
            {
                if (await WaitForLockAsync(conn, lockName, token, LinkedCts.Token))
                {
                    // Task completed within timeout.
                    return true;
                }
                else
                {
                    // task timed out/cancelled
                    return false;
                }
            }
        }

        private static async Task<bool> WaitForLockAsync(IDatabase conn,
            string lockName, string token, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await conn.LockTakeAsync(lockName, token, TimeSpan.MaxValue))
                {
                    return true;
                }
                else
                {
                    await Task.Delay(100);
                }
            }

            await conn.LockReleaseAsync(lockName, token);

            return false;
        }
    }
}