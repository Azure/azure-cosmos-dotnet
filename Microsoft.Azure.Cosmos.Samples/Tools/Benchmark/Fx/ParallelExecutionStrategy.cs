﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ParallelExecutionStrategy : IExecutionStrategy
    {
        private readonly Func<IBenchmarkOperatrion> benchmarkOperation;

        private volatile int pendingExecutorCount;

        public ParallelExecutionStrategy(
            Func<IBenchmarkOperatrion> benchmarkOperation)
        {
            this.benchmarkOperation = benchmarkOperation;
        }

        public async Task<RunSummary> ExecuteAsync(
            int serialExecutorConcurrency,
            int serialExecutorIterationCount,
            bool traceFailures,
            double warmupFraction)
        {
            IExecutor warmupExecutor = new SerialOperationExecutor(
                        executorId: "Warmup",
                        benchmarkOperation: this.benchmarkOperation());
            await warmupExecutor.ExecuteAsync(
                    (int)(serialExecutorIterationCount * warmupFraction),
                    isWarmup: true,
                    traceFailures: traceFailures,
                    completionCallback: () => { });

            IExecutor[] executors = new IExecutor[serialExecutorConcurrency];
            for (int i = 0; i < serialExecutorConcurrency; i++)
            {
                executors[i] = new SerialOperationExecutor(
                            executorId: i.ToString(),
                            benchmarkOperation: this.benchmarkOperation());
            }

            this.pendingExecutorCount = serialExecutorConcurrency;
            for (int i = 0; i < serialExecutorConcurrency; i++)
            {
                _ = executors[i].ExecuteAsync(
                        iterationCount: serialExecutorIterationCount,
                        isWarmup: false,
                        traceFailures: traceFailures,
                        completionCallback: () => Interlocked.Decrement(ref this.pendingExecutorCount));
            }

            return await this.LogOutputStats(executors);
        }

        private async Task<RunSummary> LogOutputStats(IExecutor[] executors)
        {
            const int outputLoopDelayInSeconds = 1;
            IList<int> perLoopCounters = new List<int>();
            Summary lastSummary = new Summary();

            Stopwatch watch = new Stopwatch();
            watch.Start();

            bool isLastIterationCompleted = false;
            do
            {
                isLastIterationCompleted = this.pendingExecutorCount <= 0;

                Summary currentTotalSummary = new Summary();
                for (int i = 0; i < executors.Length; i++)
                {
                    IExecutor executor = executors[i];
                    Summary executorSummary = new Summary()
                    {
                        succesfulOpsCount = executor.SuccessOperationCount,
                        failedOpsCount = executor.FailedOperationCount,
                        ruCharges = executor.TotalRuCharges,
                    };

                    currentTotalSummary += executorSummary;
                }

                // In-theory summary might be lower than real as its not transactional on time
                currentTotalSummary.elapsedMs = watch.Elapsed.TotalMilliseconds;

                Summary diff = currentTotalSummary - lastSummary;
                lastSummary = currentTotalSummary;

                diff.Print(currentTotalSummary.failedOpsCount + currentTotalSummary.succesfulOpsCount);
                perLoopCounters.Add((int)diff.Rps());

                await Task.Delay(TimeSpan.FromSeconds(outputLoopDelayInSeconds));
            }
            while (!isLastIterationCompleted);

            using (ConsoleColorContext ct = new ConsoleColorContext(ConsoleColor.Green))
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine("--------------------------------------------------------------------- ");
                lastSummary.Print(lastSummary.failedOpsCount + lastSummary.succesfulOpsCount);

                // Skip first 5 and last 5 counters as outliers
                IEnumerable<int> exceptFirst5 = perLoopCounters.Skip(5);
                int[] summaryCounters = exceptFirst5.Take(exceptFirst5.Count() - 5).OrderByDescending(e => e).ToArray();

                RunSummary runSummary = new RunSummary();

                if (summaryCounters.Length > 0)
                {

                    Console.WriteLine();
                    Utility.TeeTraceInformation("After Excluding outliers");

                    runSummary.Top10PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.1 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top20PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.2 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top30PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.3 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top40PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.4 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top50PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.5 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top60PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.6 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top70PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.7 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top80PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.8 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top90PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.9 * summaryCounters.Length)).Average(), 0);
                    runSummary.Top95PercentAverageRps = Math.Round(summaryCounters.Take((int)(0.95 * summaryCounters.Length)).Average(), 0);
                    runSummary.AverageRps = Math.Round(summaryCounters.Average(), 0);

                    string summary = JsonHelper.ToString(runSummary);
                    Utility.TeeTraceInformation(summary);
                }

                Console.WriteLine("--------------------------------------------------------------------- ");

                return runSummary;
            }
        }
    }
}
