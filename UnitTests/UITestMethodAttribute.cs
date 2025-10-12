using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Carnassial.UnitTests
{
    public class UITestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) : TestMethodAttribute(callerFilePath, callerLineNumber)
    {
        private static ITestMethod? CurrentTest;
        private static readonly SemaphoreSlim Lock;
        private static readonly AutoResetEvent TestAvailableToRunOrExitRequested;
        private static TaskCompletionSource<TestResult>? TestCompletionSource;
        private static readonly Thread UXTestThread;
        private static bool WaitForNextTest;

        static UITestMethodAttribute()
        {
            UITestMethodAttribute.CurrentTest = null;
            UITestMethodAttribute.Lock = new(initialCount: 1, maxCount: 1);
            UITestMethodAttribute.TestAvailableToRunOrExitRequested = new(initialState: false);
            UITestMethodAttribute.TestCompletionSource = null;
            UITestMethodAttribute.UXTestThread = new(() =>
            {
                while (UITestMethodAttribute.WaitForNextTest)
                {
                    UITestMethodAttribute.TestAvailableToRunOrExitRequested.WaitOne();
                    if ((UITestMethodAttribute.CurrentTest != null) && (UITestMethodAttribute.TestCompletionSource != null))
                    {
                        try
                        {
                            UITestMethodAttribute.TestCompletionSource.SetResult(UITestMethodAttribute.CurrentTest.InvokeAsync(null).GetAwaiter().GetResult());
                        }
                        catch (Exception exception)
                        {
                            UITestMethodAttribute.TestCompletionSource.SetException(exception);
                        }
                    }
                }
            })
            {
                IsBackground = true
            };
            UITestMethodAttribute.UXTestThread.SetApartmentState(ApartmentState.STA);
            UITestMethodAttribute.WaitForNextTest = true;

            UITestMethodAttribute.UXTestThread.Start();
        }

        public async override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            // ideally the dispatcher thread would be STA by the time Execute() is called but it's MTA
            // Therefore, the approach below isn't viable even though the app and dispatcher have been created.
            //TestResult result = App.Current.Dispatcher.Invoke(() => { return testMethod.Invoke(null); });
            //return [result];

            // workaround: schedule test onto an STA thread which listens for tests
            // Since WPF assumes a single UI thread, this approach requires either tests be marked with DoNotParallelizeAttribute or
            // locking within this function. Use of [DoNotParellelize] requires more code, creates more failure points, and reduces
            // potential test harness parallelism but might allow more accurate reporting of test times. Locking is somewhat simpler,
            // robust to missing [DoNotParellelize] attributes, very slightly faster, and (as of mstest 3.1.1) doesn't affect test
            // runtime reporting.
            await UITestMethodAttribute.Lock.WaitAsync(); // could also use a lock object and lock() { } but a CS1998 results as there's no use of await
            TestResult testResult;
            try
            {
                UITestMethodAttribute.CurrentTest = testMethod;
                UITestMethodAttribute.TestCompletionSource = new();
                UITestMethodAttribute.TestAvailableToRunOrExitRequested.Set();

                Task<TestResult> test = UITestMethodAttribute.TestCompletionSource.Task; // ClassCleanup() hangs at UXTestThread.Join() if await is used rather than Wait()
                try
                {
                    test.Wait();
                    testResult = test.Result;
                }
                catch (AggregateException exception)
                {
                    testResult = new()
                    {
                        TestFailureException = exception.InnerException
                    };
                }

                UITestMethodAttribute.CurrentTest = null;
                UITestMethodAttribute.TestCompletionSource = null;
            }
            finally
            {
                UITestMethodAttribute.Lock.Release();
            }

            return [ testResult ];
        }

        public static void ClassCleanup()
        {
            UITestMethodAttribute.WaitForNextTest = false;
            UITestMethodAttribute.TestAvailableToRunOrExitRequested.Set();
            UITestMethodAttribute.UXTestThread.Join();
        }
    }
}
