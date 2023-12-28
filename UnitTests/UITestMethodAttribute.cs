using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Carnassial.UnitTests
{
    public class UITestMethodAttribute : TestMethodAttribute
    {
        private static readonly Thread UXTestThread;

        private static ITestMethod? CurrentTest;
        private static readonly object Lock;
        private static readonly AutoResetEvent TestAvailableOrExitRequested;
        private static TaskCompletionSource<TestResult>? TestCompletionSource;
        private static bool WaitForNextTest;

        static UITestMethodAttribute()
        {
            UITestMethodAttribute.CurrentTest = null;
            UITestMethodAttribute.Lock = new();
            UITestMethodAttribute.TestAvailableOrExitRequested = new(initialState: false);
            UITestMethodAttribute.TestCompletionSource = null;
            UITestMethodAttribute.UXTestThread = new(() =>
            {
                while (UITestMethodAttribute.WaitForNextTest)
                {
                    UITestMethodAttribute.TestAvailableOrExitRequested.WaitOne();
                    if ((UITestMethodAttribute.CurrentTest != null) && (UITestMethodAttribute.TestCompletionSource != null))
                    {
                        try
                        {
                            UITestMethodAttribute.TestCompletionSource.SetResult(UITestMethodAttribute.CurrentTest.Invoke(null));
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

        public override TestResult[] Execute(ITestMethod testMethod)
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
            TestResult testResult;
            lock (UITestMethodAttribute.Lock)
            {
                UITestMethodAttribute.CurrentTest = testMethod;
                UITestMethodAttribute.TestCompletionSource = new();
                UITestMethodAttribute.TestAvailableOrExitRequested.Set();

                Task<TestResult> test = UITestMethodAttribute.TestCompletionSource.Task;
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

            return [testResult];
        }

        public static void ClassCleanup()
        {
            UITestMethodAttribute.WaitForNextTest = false;
            UITestMethodAttribute.TestAvailableOrExitRequested.Set();
            UITestMethodAttribute.UXTestThread.Join();
        }
    }
}
