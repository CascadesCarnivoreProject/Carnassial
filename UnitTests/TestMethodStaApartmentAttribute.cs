using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Windows.Threading;

namespace Carnassial.UnitTests
{
    public class TestMethodStaApartmentAttribute : TestMethodAttribute
    {
        private static TestResult? UITestResult;
        private static readonly Thread UITestThread;

        static TestMethodStaApartmentAttribute()
        {
            TestMethodStaApartmentAttribute.UITestResult = null;
            TestMethodStaApartmentAttribute.UITestThread = new((object? testMethodAsObject) =>
            {
                if (testMethodAsObject == null)
                {
                    TestMethodStaApartmentAttribute.UITestResult = new TestResult()
                    {
                        TestFailureException = new ArgumentNullException(nameof(testMethodAsObject))
                    };
                    return;
                }

                try
                {
                    TestMethodStaApartmentAttribute.UITestResult = ((ITestMethod)testMethodAsObject).Invoke(null);
                }
                catch (Exception exception)
                {
                    TestMethodStaApartmentAttribute.UITestResult = new TestResult()
                    {
                        TestFailureException = exception
                    };
                }
            });
            TestMethodStaApartmentAttribute.UITestThread.SetApartmentState(ApartmentState.STA);
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            // ideally the dispatcher thread would be STA by the time Execute() is called but it's MTA
            // Therefore, the approach below isn't viable even though the app and dispatcher have been created.
            //TestResult result = App.Current.Dispatcher.Invoke(() => { return testMethod.Invoke(null); });
            //return [result];

            // workaround: schedule test onto an STA thread
            // Problem with this approach is only a single unit test can be run; once the thread is Join()ed it can't be restarted.
            // However, creating an STA thread per test also fails as WPF requires only a single thread per app.
            // TODO: Implement an STA dispatcher capable of running multiple tests on the same thread (https://github.com/microsoft/testfx/issues/551).
            TestMethodStaApartmentAttribute.UITestThread.Start(testMethod);
            TestMethodStaApartmentAttribute.UITestThread.Join();

            if (TestMethodStaApartmentAttribute.UITestResult == null)
            {
                throw new InvalidOperationException(testMethod.TestMethodName + " returned null.");
            }
            return [TestMethodStaApartmentAttribute.UITestResult];
        }
    }
}
