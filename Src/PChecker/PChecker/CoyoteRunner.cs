using Microsoft.Coyote.SystematicTesting;
using Microsoft.Coyote.Runtime;
using Microsoft.Coyote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Coyote.Actors;
using CoyoteTasks = Microsoft.Coyote.Tasks;

namespace Plang.PChecker
{
    /// <summary>
    /// CoyoteRunner is a wrapper class for running Coyote in a separate process
    /// </summary>
    internal class CoyoteRunner
    {
        private readonly PCheckerJobConfiguration job;
        public CoyoteRunner(PCheckerJobConfiguration _job)
        {
            job = _job;
        }

        public int Run()
        {
            // Optional: increases verbosity level to see the Coyote runtime log.
            Configuration configuration = Configuration.Create()
                                            .WithTestingIterations(job.MaxScheduleIterations)
                                            .WithActivityCoverageEnabled()
                                            .WithMaxSchedulingSteps(job.MaxStepsPerExecution)
                                            .WithVerbosityEnabled(job.IsVerbose)
                                            .WithRandomStrategy();

            // load the test cases
            var testDll = Assembly.LoadFrom(job.PathToTestDll);
            // load all the test cases in the dll
            var allTestCases = GetTestMethod(testDll, job.TestCase);
            TestingEngine engine = TestingEngine.Create(configuration, (Action)allTestCases.testMethod);
            engine.Run();
            string bug = engine.TestReport.BugReports.FirstOrDefault();
            if (bug != null)
            {
                Console.WriteLine(bug);
                return 1;
            }
            return 0;

                        // for debugging:
            /* For replaying a bug and single stepping
            Configuration configuration = Configuration.Create();
            configuration.WithVerbosityEnabled(true);
            // update the path to the schedule file.
            configuration.WithReplayStrategy(""AfterNewUpdate.schedule"");
            TestingEngine engine = TestingEngine.Create(configuration, DefaultImpl.Execute);
            engine.Run();
            string bug = engine.TestReport.BugReports.FirstOrDefault();
            if (bug != null)
            {
                Console.WriteLine(bug);
            }*/
        }

        /// <summary>
        /// Finds the test methods with the specified attribute in the given assembly.
        /// Returns an empty list if no such methods are found.
        /// </summary>
        private List<MethodInfo> FindTestMethodsWithAttribute(Type attribute, BindingFlags bindingFlags, Assembly assembly)
        {
            List<MethodInfo> testMethods = null;

            try
            {
                testMethods = assembly.GetTypes().SelectMany(t => t.GetMethods(bindingFlags)).
                    Where(m => m.GetCustomAttributes(attribute, false).Length > 0).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var le in ex.LoaderExceptions)
                {
                    job.Output.WriteError(le.Message);
                }

                throw;
            }
            catch (Exception ex)
            {
                job.Output.WriteError(ex.Message);
                throw;
            }

            return testMethods;
        }
        private (Delegate testMethod, string testName) GetTestMethod(Assembly assembly, string methodName)
        {
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod;
            List<MethodInfo> testMethods = FindTestMethodsWithAttribute(typeof(TestAttribute), flags, assembly);

            if (testMethods.Count > 0)
            {
                List<MethodInfo> filteredTestMethods = null;
                string error = null;

                if (!string.IsNullOrEmpty(methodName))
                {
                    // Filter by test method name.
                    filteredTestMethods = testMethods.FindAll(mi => string.Format("{0}.{1}",
                        mi.DeclaringType.FullName, mi.Name).Contains($"{methodName}"));
                    if (filteredTestMethods.Count is 0)
                    {
                        error = $"Cannot detect a test case containing {methodName}.";
                    }
                    else if (filteredTestMethods.Count > 1)
                    {
                        error = $"The test case name '{methodName}' is ambiguous. Please specify the full test case name.";
                    }
                }
                else if (testMethods.Count > 1)
                {
                    error = $"Found '{testMethods.Count}' test cases' " + "Provide --test-case (-tc)  to qualify the test case you wish to check.";
                }

                if (!string.IsNullOrEmpty(error))
                {
                    error += " Possible test cases are:" + Environment.NewLine;

                    var possibleMethods = filteredTestMethods?.Count > 1 ? filteredTestMethods : testMethods;
                    for (int idx = 0; idx < possibleMethods.Count; idx++)
                    {
                        var mi = possibleMethods[idx];
                        error += string.Format("  {0}", mi.DeclaringType.Name);
                        if (idx < possibleMethods.Count - 1)
                        {
                            error += Environment.NewLine;
                        }
                    }

                    throw new InvalidOperationException(error);
                }

                if (!string.IsNullOrEmpty(methodName))
                {
                    testMethods = filteredTestMethods;
                }
            }
            else if (testMethods.Count is 0)
            {
                throw new InvalidOperationException("Cannot detect a P test case declared in the dll");
            }

            MethodInfo testMethod = testMethods[0];
            ParameterInfo[] testParams = testMethod.GetParameters();

            bool hasVoidReturnType = testMethod.ReturnType == typeof(void);
            bool hasTaskReturnType = typeof(Task).IsAssignableFrom(testMethod.ReturnType);
            bool hasControlledTaskReturnType = typeof(CoyoteTasks.Task).IsAssignableFrom(testMethod.ReturnType);

            bool hasNoInputParameters = testParams.Length is 0;
            bool hasActorInputParameters = testParams.Length is 1 && testParams[0].ParameterType == typeof(IActorRuntime);
            bool hasTaskInputParameters = testParams.Length is 1 && testParams[0].ParameterType == typeof(ICoyoteRuntime);

            if (!((hasVoidReturnType || hasTaskReturnType || hasControlledTaskReturnType) &&
                (hasNoInputParameters || hasActorInputParameters || hasTaskInputParameters) &&
                !testMethod.IsAbstract && !testMethod.IsVirtual && !testMethod.IsConstructor &&
                !testMethod.ContainsGenericParameters && testMethod.IsPublic && testMethod.IsStatic))
            {
                throw new InvalidOperationException("Incorrect test method declaration. Please " +
                    $"make sure your [{typeof(TestAttribute).FullName}] methods have:\n\n" +
                    $"Parameters:\n" +
                    $"  ()\n" +
                    $"  (ICoyoteRuntime runtime)\n" +
                    $"  (IActorRuntime runtime)\n\n" +
                    $"Return type:\n" +
                    $"  void\n" +
                    $"  {typeof(Task).FullName}\n" +
                    $"  {typeof(Task).FullName}<T>\n" +
                    $"  {typeof(CoyoteTasks.Task).FullName}\n" +
                    $"  {typeof(CoyoteTasks.Task).FullName}<T>\n" +
                    $"  async {typeof(Task).FullName}\n" +
                    $"  async {typeof(Task).FullName}<T>\n" +
                    $"  async {typeof(CoyoteTasks.Task).FullName}\n" +
                    $"  async {typeof(CoyoteTasks.Task).FullName}<T>\n");
            }

            Delegate test;
            if (hasTaskReturnType)
            {
                if (hasActorInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Func<IActorRuntime, Task>));
                }
                else if (hasTaskInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Func<ICoyoteRuntime, Task>));
                }
                else
                {
                    test = testMethod.CreateDelegate(typeof(Func<Task>));
                }
            }
            else if (hasControlledTaskReturnType)
            {
                if (hasActorInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Func<IActorRuntime, CoyoteTasks.Task>));
                }
                else if (hasTaskInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Func<ICoyoteRuntime, CoyoteTasks.Task>));
                }
                else
                {
                    test = testMethod.CreateDelegate(typeof(Func<CoyoteTasks.Task>));
                }
            }
            else
            {
                if (hasActorInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Action<IActorRuntime>));
                }
                else if (hasTaskInputParameters)
                {
                    test = testMethod.CreateDelegate(typeof(Action<ICoyoteRuntime>));
                }
                else
                {
                    test = testMethod.CreateDelegate(typeof(Action));
                }
            }

            return (test, $"{testMethod.DeclaringType}.{testMethod.Name}");
        }
    }
}