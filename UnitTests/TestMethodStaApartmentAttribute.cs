using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace Carnassial.UnitTests
{
    public class TestMethodStaApartmentAttribute : TestMethodAttribute
    {
        public override TestResult[] Execute(ITestMethod testMethod)
        {
            TestResult? result = null;
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                result = testMethod.Invoke(null);
            }
            else
            {
                Thread thread = new(() =>
                {
                    try
                    {
                        result = testMethod.Invoke(null);
                    }
                    catch (Exception exception)
                    {
                        result = new TestResult()
                        {
                            TestFailureException = exception
                        };
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }

            if (result == null)
            {
                throw new InvalidOperationException(testMethod.TestMethodName + " returned null.");
            }
            return new TestResult[] { result };
        }
    }
}
