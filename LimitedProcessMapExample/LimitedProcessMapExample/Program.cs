using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Example usage
        int N = 3;
        int timeoutMs = 5000;

        List<Func<Task<object>>> functions = new List<Func<Task<object>>>
        {
             async () => { await Task.Delay(1000); throw new Exception("Function 1 failed"); }, // throw error
            async () => { await Task.Delay(8000); return "Result 2"; }, // should be time out
            async () => { await Task.Delay(7000); return "Result 3"; }, // should be time out
            async () => { await Task.Delay(2000); return "Result 4"; }, // success result should be 3
            async () => { await Task.Delay(4000); return "Result 5"; }, // success result should be 4
            async () => { await Task.Delay(6000); return "Result 6"; }, // should be time out 
           
        };

        try
        {
            var results = await PMapN(N, functions, timeoutMs);
            Console.WriteLine("Results:");
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine("The operation timed out.");
        }
    }

    public static async Task<List<object>> PMapN(int N, List<Func<Task<object>>> functions, int timeoutMs)
    {


        using var cts = new CancellationTokenSource();
      
      
        var results = new object[functions.Count];
        var tasks = new List<Task>();

        using (var semaphore = new SemaphoreSlim(N))
        {
            for (int i = 0; i < functions.Count; i++)
            {
                int index = i;

              
                await semaphore.WaitAsync(cts.Token);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var taskCts = new CancellationTokenSource(timeoutMs);
                        var functionTask = functions[index]();
                        if (await Task.WhenAny(functionTask, Task.Delay(timeoutMs, taskCts.Token)) == functionTask)
                        {
                            results[index] = await functionTask;
                        }
                        else
                        {
                            results[index] = "Task timed out";
                        }
                    }
                    catch (Exception ex)
                    {
                       
                        results[index] = ex.Message;
                       
                    }
                    finally
                    {
                        semaphore.Release();
                       
                    }
                }, cts.Token));
            }

            try
            {
                await Task.WhenAll(tasks);
                
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The operation timed out.");
            }
        }

        return results.ToList();
    }
}
