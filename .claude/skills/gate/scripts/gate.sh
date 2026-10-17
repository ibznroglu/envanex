#!/usr/bin/env bash
# Runs the project's gate and records every step's raw output in TestResults/<slug>/gates.txt,
# where <slug> is the current branch with "/" replaced by "-". Then prints a summary computed
# from that record.
#
# Usage: bash .claude/skills/gate/scripts/gate.sh
#
# Takes no arguments, never fetches, and works on the repository that contains the current
# directory. Each step runs with stdin from /dev/null.
#
# Exit status:
#   0  the record is complete and verified, and its last line is "gate-exit=0 failed="
#   1  the record is complete and verified, and a required step failed
#   2  start-up failure; nothing written
#   3  I/O error on the record
#
# Every exit-2 or exit-3 path prints exactly one line to stderr, "gate.sh: <reason>".

set -u

fail() {
  printf 'gate.sh: %s\n' "$2" >&2
  exit "$1"
}

# --- Start-up: exit 2, nothing written. -------------------------------------------------------

top=$(git rev-parse --show-toplevel 2>/dev/null) && [ -n "$top" ] || fail 2 'not inside a git work tree'
cd -- "$top" >/dev/null 2>&1 || fail 2 "cannot cd to $top"
branch=$(git branch --show-current 2>/dev/null) && [ -n "$branch" ] || fail 2 'no current branch'
slug=${branch//\//-}
dir=TestResults/$slug
mkdir -p -- "$dir" >/dev/null 2>&1 || fail 2 "cannot create $dir"

# --- The record: opened once, truncated, on descriptor 3. --------------------------------------

REC=$dir/gates.txt
{ exec 3>"$REC"; } 2>/dev/null || fail 3 "I/O error: open $REC"

io_fail() {
  fail 3 "I/O error: $1 $REC"
}

# Writes one line to the record.
put() {
  { printf '%s\n' "$1" >&3; } 2>/dev/null || io_fail write
}

# Sets SIZE to the record's size in bytes.
read_size() {
  local s
  s=$(wc -c 2>/dev/null <"$REC") || io_fail read
  s=${s//[!0-9]/}
  [ -n "$s" ] || io_fail read
  SIZE=$s
}

# Ends a step's output with a newline when it is non-empty and lacks one.
# $1 = the record's size before the step ran.
fix_newline() {
  local last
  read_size
  if [ "$SIZE" -gt "$1" ]; then
    { last=$(tail -c 1 -- "$REC" && printf x); } 2>/dev/null || io_fail read
    if [ "$last" != $'\nx' ]; then
      { printf '\n' >&3; } 2>/dev/null || io_fail write
    fi
  fi
}

# Prints the command's output, or "missing" when it fails or prints nothing.
value() {
  local v
  if v=$("$@" 2>/dev/null) && [ -n "$v" ]; then
    printf '%s' "$v"
  else
    printf 'missing'
  fi
}

# --- Steps. -----------------------------------------------------------------------------------

# run_step <name> <required|info> <command string>
run_step() {
  local name=$1 kind=$2 cmd=$3 before rc
  put "\$ $cmd"
  read_size
  before=$SIZE
  eval "$cmd" </dev/null >&3 2>&3
  rc=$?
  fix_newline "$before"
  put "exit=$rc step=$name kind=$kind"
}

tools_check() {
  local name path rc=0
  for name in git node dotnet sha256sum; do
    if path=$(command -v "$name"); then
      printf '%s\n' "$path"
    else
      printf 'missing: %s\n' "$name"
      rc=1
    fi
  done
  return "$rc"
}

# Prints the two merge-bases. Returns 1 with a reason line when origin/main is missing, when
# either git merge-base fails, or when the two differ.
base_check() {
  local base_main base_origin main_rc origin_rc rc=0
  base_main=$(git merge-base main HEAD)
  main_rc=$?
  base_origin=$(git merge-base origin/main HEAD)
  origin_rc=$?
  printf 'base_main=%s\n' "$base_main"
  printf 'base_origin=%s\n' "$base_origin"
  if ! git rev-parse --verify --quiet origin/main >/dev/null; then
    printf 'reason: origin/main is missing\n'
    rc=1
  fi
  if [ "$main_rc" -ne 0 ]; then
    printf 'reason: git merge-base main HEAD failed with exit %s\n' "$main_rc"
    rc=1
  fi
  if [ "$origin_rc" -ne 0 ]; then
    printf 'reason: git merge-base origin/main HEAD failed with exit %s\n' "$origin_rc"
    rc=1
  fi
  if [ "$rc" -eq 0 ] && [ "$base_main" != "$base_origin" ]; then
    printf 'reason: the two merge-bases differ\n'
    rc=1
  fi
  return "$rc"
}

# Reads the required steps' exit lines back from the record and writes the final line.
# Sets FINAL to that line and CODE to 0 or 1.
final_status() {
  local lines st step n ok rest line failed='' code=0
  lines=$(grep -E '^exit=[0-9]+ step=[^ ]+ kind=required$' -- "$REC" 2>/dev/null)
  st=$?
  [ "$st" -le 1 ] || io_fail read
  for step in tools-check base-check node format build test; do
    n=0
    ok=0
    rest=$lines
    while [ -n "$rest" ]; do
      line=${rest%%$'\n'*}
      if [ "$line" = "$rest" ]; then rest=''; else rest=${rest#*$'\n'}; fi
      case $line in
        "exit=0 step=$step kind=required") n=$((n + 1)); ok=1 ;;
        "exit="*" step=$step kind=required") n=$((n + 1)) ;;
      esac
    done
    if [ "$n" -ne 1 ] || [ "$ok" -ne 1 ]; then
      failed=${failed:+$failed,}$step
      code=1
    fi
  done
  FINAL="gate-exit=$code failed=$failed"
  CODE=$code
  put "$FINAL"
}

# section <command string> <step name> <all|tail5|tests|node>
# Reads the lines between the step's "$ <command>" line and its "exit=" line, and prints them
# all, the last 5, the "Passed!"/"Failed!" lines with the passed total, or the node pass/fail
# summary lines.
section() {
  awk -v start="\$ $1" -v stop="^exit=[0-9]+ step=$2 kind=(required|info)\$" -v mode="$3" '
    $0 == start { f = 1; c = 0; next }
    f && $0 ~ stop { f = 0; next }
    !f { next }
    mode == "all" { print; next }
    mode == "tail5" { b[c % 5] = $0; c++; next }
    mode == "tests" && /Passed!|Failed!/ {
      print
      if (match($0, /Passed:[[:space:]]*[0-9]+/)) {
        p = substr($0, RSTART, RLENGTH)
        gsub(/[^0-9]/, "", p)
        total += p
      }
      next
    }
    mode == "node" && /^(# |ℹ )(pass|fail) [0-9]+$/ { print }
    END {
      if (mode == "tail5") for (i = (c > 5 ? c - 5 : 0); i < c; i++) print b[i % 5]
      if (mode == "tests") printf "dotnet test passed total: %d\n", total
    }' "$REC" 2>/dev/null
}

# --- Header and steps, in order. --------------------------------------------------------------

ts=$(date -u +%Y-%m-%dT%H:%M:%SZ 2>/dev/null)
head_sha=$(value git rev-parse HEAD)
main_sha=$(value git rev-parse --verify --quiet main)
origin_sha=$(value git rev-parse --verify --quiet origin/main)
base_main=$(value git merge-base main HEAD)
base_origin=$(value git merge-base origin/main HEAD)
put "gate.sh $ts branch=$branch head=$head_sha main=$main_sha origin_main=$origin_sha base_main=$base_main base_origin=$base_origin"

run_step tools-check required 'tools_check'
run_step status info 'git status --short'
run_step base-check required 'base_check'
run_step files info 'git diff --name-only main...HEAD'
run_step log info 'git log --oneline main..HEAD'
run_step docker info 'docker compose ps'
run_step node required 'node --test .claude/hooks/tests/*.test.js'
run_step format required 'dotnet format --verify-no-changes'
run_step build required 'dotnet build -warnaserror'
run_step test required 'dotnet test'
final_status

# --- Final verification: close, then re-read the last line. -----------------------------------

{ exec 3>&-; } 2>/dev/null || io_fail close
{ last_line=$(tail -n 1 -- "$REC" && printf x); } 2>/dev/null || io_fail read
[ "$last_line" = "$FINAL"$'\nx' ] || io_fail verify

# --- Summary, computed from the verified record. ----------------------------------------------

printf '%s\n' "$REC"
if command -v sha256sum >/dev/null 2>&1; then
  sha256sum -- "$REC" 2>/dev/null || io_fail read
else
  printf 'sha256=unavailable\n'
fi
printf 'files:\n'
section 'git diff --name-only main...HEAD' files all || io_fail read
printf 'steps:\n'
grep -e '^exit=' -- "$REC" 2>/dev/null
[ "$?" -le 1 ] || io_fail read
printf 'build (last 5 lines):\n'
section 'dotnet build -warnaserror' build tail5 || io_fail read
printf 'tests:\n'
section 'dotnet test' test tests || io_fail read
printf 'node:\n'
section 'node --test .claude/hooks/tests/*.test.js' node node || io_fail read
tail -n 1 -- "$REC" 2>/dev/null || io_fail read

exit "$CODE"
