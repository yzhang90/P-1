using System;
using System.IO;
using System.Linq;
using Plang.Compiler;

namespace Plang.PChecker
{
    /// <summary>
    /// Implements the configuration of the P Checker Job
    /// </summary>
    public class PCheckerJobConfiguration
    {
        public ICompilerOutput Output;
        /// <summary>
        /// Path to the DLL of the program under test
        /// </summary>
        public string PathToTestDll = null;

        /// <summary>
        /// Output directory path where the generated output is dumped
        /// </summary>
        public string OutputDirectoryPath = GetNextOutputDirectoryName("PCheckerOutput");

        /// <summary>
        /// Number of parallel instances of the checker
        /// </summary>
        public int Parallelism = 1;

        /// <summary>
        /// Test case to run the PChecker on
        /// </summary>
        public string TestCase = "DefaultImpl";

        /// <summary>
        /// Maximum number of schedules to be explored
        /// </summary>
        public uint MaxScheduleIterations = 10000;

        /// <summary>
        /// Max steps or depth per execution explored
        /// </summary>
        public uint MaxStepsPerExecution = 5000;

        /// <summary>
        /// Generate an error after the maximum steps
        /// </summary>
        public uint ErrorOutAtMaxSteps = 10000;

        /// <summary>
        /// Is verbose ON
        /// </summary>
        public bool IsVerbose = false;

        /// <summary>
        /// Is test mode or replay mode
        /// </summary>
        public bool IsReplay = false;

        /// <summary>
        /// Error schedule to be replayed
        /// </summary>
        public string ErrorSchedule = null;

        public PCheckerJobConfiguration(ICompilerOutput output)
        {
            Output = output;
        }
        private static string GetNextOutputDirectoryName(string v)
        {
            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), v);
            string folderName = Directory.Exists(directoryPath) ? Directory.GetDirectories(directoryPath).Count().ToString() : "0";
            return Path.Combine(directoryPath, folderName);
        }
    }
}