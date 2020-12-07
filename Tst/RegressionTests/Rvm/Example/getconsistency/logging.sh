#!/bin/bash

progress () { echo "====== $@" ; }

# Executables
# -----------

repo_dir="$(cd "$(dirname "$0")"; pwd)"
rvmonitor_bin="$repo_dir/../ext/rv-monitor/target/release/rv-monitor/bin"
gen_src_dir="$repo_dir/target/generated-sources"

progress "Copy Logger"

(   ( mkdir -p "$gen_src_dir/aspectJ" && mkdir -p "$gen_src_dir/java" && \
      cd "$repo_dir/logger" && \
      cp *.aj "$gen_src_dir/aspectJ" && \
      cp ./dep/*.java "$gen_src_dir/java" ) \
 || ( rm -f "$gen_src_dir/aspectJ" ; rm -f "$gen_src_dir/java/*.java" ))
