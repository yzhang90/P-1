using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Plang.Compiler;
using System.IO;

namespace Plang.PChecker
{
    /// <summary>
    /// Result of parsing commandline options for PChecker
    /// </summary>
    public enum CommandLineParseResult
    {
        Success,
        Failure,
        HelpRequested
    }

    internal class CommandLineOptions
    {
        /// <summary>
        /// Command line output: default is on console.
        /// </summary>
        private static readonly DefaultCompilerOutput CommandlineOutput =
            new DefaultCompilerOutput(new DirectoryInfo(Directory.GetCurrentDirectory()));

        public static CommandLineParseResult ParseArguments(IEnumerable<string> args, out PCheckerJobConfiguration job)
        {
            job = new PCheckerJobConfiguration(CommandlineOutput);

            try
            { 
                foreach (string x in args)
                {
                    string arg = x;
                    string colonArg = null;
                    if (arg[0] == '-')
                    {
                        int colonIndex = arg.IndexOf(':');
                        if (colonIndex >= 0)
                        {
                            arg = x.Substring(0, colonIndex);
                            colonArg = x.Substring(colonIndex + 1);
                        }
                        if(colonArg == null)
                        {
                            throw new CommandlineParsingError($"Missing argument with the option {arg}, use -h or -help to see all options");
                        }
                        switch (arg.Substring(1).ToLowerInvariant())
                        {
                            case "r":
                            case "-replay":
                                {
                                    job.IsReplay = true;
                                    if(!File.Exists(colonArg))
                                    {
                                        throw new CommandlineParsingError($"Invalid path to error schedule: {colonArg}");
                                    }
                                    else
                                    {
                                        job.ErrorSchedule = colonArg;
                                    }
                                }
                                break;
                            case "v":
                            case "-verbose":
                                {
                                    try
                                    {
                                        job.IsVerbose = Boolean.Parse(colonArg);
                                    }
                                    catch (FormatException)
                                    {
                                        throw new CommandlineParsingError($"Invalid argument with verbose, expected true or false: {colonArg}");
                                    }
                                }
                                break;
                            case "i":
                            case "-iterations":
                                {
                                    try
                                    {
                                        job.MaxScheduleIterations = uint.Parse(colonArg);
                                    }
                                    catch (FormatException)
                                    {
                                        throw new CommandlineParsingError($"Invalid argument with {arg}, expected unsigned integer: {colonArg}");
                                    }
                                }
                                break;
                            case "ms":
                            case "-maxsteps":
                                {
                                    try
                                    {
                                        job.MaxStepsPerExecution = uint.Parse(colonArg);
                                    }
                                    catch (FormatException)
                                    {
                                        throw new CommandlineParsingError($"Invalid argument with {arg}, expected unsigned integer: {colonArg}");
                                    }
                                }
                                break;
                            case "fs":
                            case "-fail-after-steps":
                                {
                                    try
                                    {
                                        job.ErrorOutAtMaxSteps = uint.Parse(colonArg);
                                    }
                                    catch (FormatException)
                                    {
                                        throw new CommandlineParsingError($"Invalid argument with {arg}, expected unsigned integer: {colonArg}");
                                    }
                                }
                                break;
                            case "p":
                            case "-parallelism":
                                {
                                    try
                                    {
                                        job.Parallelism = int.Parse(colonArg);
                                    }
                                    catch (FormatException)
                                    {
                                        throw new CommandlineParsingError($"Invalid argument with {arg}, expected integer: {colonArg}");
                                    }
                                }
                                break;
                            case "tc":
                            case "-test-case":
                                {
                                    job.TestCase = colonArg;
                                }
                                break;
                            case "o":
                            case "-output-dir":
                                {
                                    
                                    job.OutputDirectoryPath = colonArg;
                                    
                                }
                                break;
                            default:
                                {
                                    PrintUsage();
                                    throw new CommandlineParsingError($"Illegal Command {arg.Substring(1)}");
                                }
                        }
                    }
                    else
                    {
                        // this must be the path to test dll
                        if(job.PathToTestDll != null)
                        {
                            throw new CommandlineParsingError($"Multiple input dlls, only one allowed. Found: {job.PathToTestDll} and {arg}");
                        }
                        else
                        {
                            if(!File.Exists(arg))
                            {
                                throw new CommandlineParsingError($"Invalid path to dll: {arg}");
                            }
                            else
                            {
                                job.PathToTestDll = arg;
                            }
                        }
                    }
                }

                // the test dll should always be passed
                if(job.PathToTestDll == null)
                {
                    throw new CommandlineParsingError("Missing input: please provide the dll to be checked");
                }
                return CommandLineParseResult.Success;
            }
            catch (CommandlineParsingError ex)
            {
                CommandlineOutput.WriteError($"<Error parsing commandline>:\n{ex.Message}");
                return CommandLineParseResult.Failure;
            }
            catch (Exception other)
            {
                CommandlineOutput.WriteError($"<Internal Error>:\n {other.Message}\n <Please report to the P team (p-devs@amazon.com) or create a issue on GitHub, Thanks!>");
                return CommandLineParseResult.Failure;
            }
        }

        internal static void PrintUsage()
        {
            CommandlineOutput.WriteInfo("------------------------------------------");
            CommandlineOutput.WriteInfo("Recommended usage:");
            CommandlineOutput.WriteInfo("For running checker:");
            CommandlineOutput.WriteInfo(">> pmc <pathToDll> -i:<numberofschedules> -tc:<testcase> [options]");
            CommandlineOutput.WriteInfo("For replaying error schedule:");
            CommandlineOutput.WriteInfo(">> pmc <pathToDll> -tc:<testcase> -replay:<pathToSchedule> [options]");
            CommandlineOutput.WriteInfo("------------------------------------------");
            CommandlineOutput.WriteInfo("For details about all the [options] see below");
            CommandlineOutput.WriteInfo("------------------------------------------");
            CommandlineOutput.WriteInfo("    -t:[target project name]   -- name of project (as well as the generated file); if not supplied then file1");
            CommandlineOutput.WriteInfo("------------------------------------------");
        }

        private class CommandlineParsingError : Exception
        {
            public CommandlineParsingError()
            {
            }

            public CommandlineParsingError(string message) : base(message)
            {
            }

            public CommandlineParsingError(string message, Exception innerException) : base(message, innerException)
            {
            }

            protected CommandlineParsingError(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}