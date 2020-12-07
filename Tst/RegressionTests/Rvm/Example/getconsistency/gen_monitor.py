#!/usr/bin/env python

import glob
import os.path
import shutil
import sys

if __name__ == "__main__":
    sys.path.append(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

import tools

def readFile(name):
    with open(name, "rt") as f:
        return ''.join(f)

def writeFile(name, contents):
    with open(name, "wt") as f:
        f.write(contents)

def runPc(pcompiler_dir, arguments):
    sep = " "
    cmdStr = sep.join(["dotnet", os.path.join(pcompiler_dir, "Bld", "Drops", "Release", "Binaries", "Pc.dll")] + arguments)
    tools.runNoError(cmdStr)

def translate(pcompiler_dir, p_spec_dir, gen_monitor_dir):
    tools.progress("Run the PCompiler...")
    p_spec_paths = glob.glob(os.path.join(p_spec_dir, "*.p"))
    if len(p_spec_paths) != 1:
        raise Exception("Expected a single p spec")
    p_spec_path = p_spec_paths[0]
    runPc(pcompiler_dir, [p_spec_path, "-g:RVM", "-o:%s" % gen_monitor_dir])

def runMonitor(rvmonitor_bin, generated_file_dir):
    tools.progress("Run RVMonitor")
    monitor_binary = os.path.join(rvmonitor_bin, "rv-monitor")
    rvm_file_paths = glob.glob(os.path.join(generated_file_dir, "*.rvm"))
    if len(rvm_file_paths) != 1:
        raise Exception("Expected a single rvm spec")
    rvm_file_path = rvm_file_paths[0]
    sep = " "
    cmdStr = sep.join([monitor_binary, "-merge", rvm_file_path])
    tools.runNoError(cmdStr)

def copyRuntime(runtime_dir, generated_file_dir):
    tools.progress("Copy Runtime")
    for f in glob.glob(os.path.join(runtime_dir, "*.java")):
        shutil.copy(f, generated_file_dir)

def copyDep(dep_dir, generated_file_dir):
    tools.progress("Copy Dep")
    for f in glob.glob(os.path.join(dep_dir, "*.java")):
        shutil.copy(f, generated_file_dir)

def compileJava(generated_file_dir):
    sep = " "
    cmdStr = sep.join(["javac", os.path.join(generated_file_dir, "*.java"), "-d", "./"])
    tools.runNoError(cmdStr) 

def build(pcompiler_dir, rvmonitor_bin, p_spec_dir, runtime_dir, generated_file_dir, dep_dir):
    translate(pcompiler_dir, p_spec_dir, generated_file_dir)
    runMonitor(rvmonitor_bin, generated_file_dir)
    copyRuntime(runtime_dir, generated_file_dir)
    copyDep(dep_dir, generated_file_dir)
    compileJava(generated_file_dir)

def removeAll(pattern):
    for f in glob.glob(pattern):
        os.remove(f)

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    pcompiler_dir = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(script_dir)))))
    runtime_dir = os.path.join(pcompiler_dir, "Src", "PRuntimes", "RvmRuntime")
    rvmonitor_bin = os.path.join(os.path.dirname(os.path.dirname(script_dir)), "ext", "rv-monitor", "target", "release", "rv-monitor", "bin")
    p_spec_dir = os.path.join(script_dir, "monitor")
    dep_dir = os.path.join(p_spec_dir, "dep")

    generated_file_dir = os.path.join(p_spec_dir, "generated")
    if not os.path.exists(generated_file_dir):
        os.makedirs(generated_file_dir)

    try:
        tools.runInDirectory(
            script_dir,
            lambda: build(pcompiler_dir, rvmonitor_bin, p_spec_dir, runtime_dir, generated_file_dir, dep_dir))
    except BaseException as e:
        raise e

if __name__ == "__main__":
    main()
