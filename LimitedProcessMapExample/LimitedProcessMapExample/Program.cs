using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class Program
{
    static async Task Main(string[] args)
    {
        // Sample lambda functions doing some random work
        List<Func<Task<int>>> functions = new List<Func<Task<int>>>
        {
            async () => { await Task.Delay(500); return 1; },
            async () => { await Task.Delay(1000); return 2; },
            async () => { await Task.Delay(1500); return 3; },
            async () => { await Task.Delay(200); return 4; },
            async () => { await Task.Delay(300); return 5; },
             async () => { await Task.Delay(2000); return 6; },
              async () => { await Task.Delay(2500); return 7; },

        };

        try
        {
            var results = await PMapN(10, functions, 3000);
            Console.WriteLine("Results: " + string.Join(", ", results));
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Execution timed out.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    public static async Task<List<TResult>> PMapN<TResult>(int maxConcurrency, List<Func<Task<TResult>>> functions, int timeoutMs)
    {
        if (functions == null || functions.Count == 0)
            throw new ArgumentException("The functions list cannot be null or empty.");

        //it will be forced to run maxConcurrency  active thread at time.
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        // Create the token source.
        using var cts = new CancellationTokenSource();

        var timeoutTask = Task.Delay(timeoutMs, cts.Token);
        var tasks = new List<Task<TResult>>();

        foreach (var function in functions)
        {
            await semaphore.WaitAsync(cts.Token);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var result = await function();
                    return result;
                }
                catch
                {
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cts.Token));
        }

        var allTasks = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasks, timeoutTask);

        if (completedTask == timeoutTask)
        {
            cts.Cancel();
            throw new TimeoutException("The operation has timed out.");
        }

        return (await allTasks).ToList();
    }
}