#!/usr/bin/env bash
# Runs the test of one mutation proof and appends the raw record to
# TestResults/<slug>/mutations/<label>.txt, where <slug> is the current branch with "/" replaced
# by "-".
#
# Usage:
#   bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> dotnet "<filter>" [<project>]
#   bash .claude/skills/mutate/scripts/mutate-run.sh <label> <file> node <test-file>
#
# <file>, <project> and <test-file> are paths from the repository root. The log records, in
# order, `git diff HEAD -- <file>` (the mutation against the commit) and the test command, each
# with its raw output and an `exit=` line. The script never changes the target file, the index or
# HEAD; the mutation is restored with the inverse Edit.
#
# Exit status:
#   0  both records are complete; red or green is the logged test `exit=`
#   2  an argument or start-up failure; nothing written
#   3  a log I/O failure
#
# Every exit-2 or exit-3 path prints exactly one line to stderr, "mutate-run.sh: <reason>".

set -u

fail() {
  printf 'mutate-run.sh: %s\n' "$2" >&2
  exit "$1"
}

usage() {
  fail 2 'usage: mutate-run.sh <label> <file> dotnet "<filter>" [<project>] | mutate-run.sh <label> <file> node <test-file>'
}

# The label becomes a file name, so it must match ^[A-Za-z0-9][A-Za-z0-9._-]*$. The characters
# are listed out so that no locale widens the ranges.
ALNUM=ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789
valid_label() {
  case $1 in
    '' | [!$ALNUM]* | *[!$ALNUM._-]*) return 1 ;;
  esac
  return 0
}

# --- Argument check: exit 2, nothing written. -------------------------------------------------

[ "$#" -ge 3 ] || usage
label=$1
file=$2
mode=$3
valid_label "$label" || usage
case $mode in
  dotnet) [ "$#" -eq 4 ] || [ "$#" -eq 5 ] || usage ;;
  node) [ "$#" -eq 4 ] || usage ;;
  *) usage ;;
esac

if [ "$mode" = dotnet ]; then
  test_cmd=(dotnet test)
  shown='dotnet test'
  if [ "$#" -eq 5 ]; then
    test_cmd+=("$5")
    shown="$shown $5"
  fi
  test_cmd+=(--filter "$4")
  shown="$shown --filter \"$4\""
else
  test_cmd=(node --test "$4")
  shown="node --test $4"
fi

# --- Start-up: exit 2, nothing written. -------------------------------------------------------

top=$(git rev-parse --show-toplevel 2>/dev/null) && [ -n "$top" ] || fail 2 'not inside a git work tree'
cd -- "$top" >/dev/null 2>&1 || fail 2 "cannot cd to $top"
branch=$(git branch --show-current 2>/dev/null) && [ -n "$branch" ] || fail 2 'no current branch'
git ls-files --error-unmatch -- "$file" >/dev/null 2>&1 || fail 2 "untracked file $file"
slug=${branch//\//-}
dir=TestResults/$slug/mutations
mkdir -p -- "$dir" >/dev/null 2>&1 || fail 2 "cannot create $dir"

# --- The log: opened once, for appending, on descriptor 3. ------------------------------------

LOG=$dir/$label.txt
{ exec 3>>"$LOG"; } 2>/dev/null || fail 3 "I/O error: open $LOG"

io_fail() {
  fail 3 "I/O error: $1 $LOG"
}

# Writes one line to the log.
put() {
  { printf '%s\n' "$1" >&3; } 2>/dev/null || io_fail write
}

# Sets SIZE to the log's size in bytes.
read_size() {
  local s
  s=$(wc -c 2>/dev/null <"$LOG") || io_fail read
  s=${s//[!0-9]/}
  [ -n "$s" ] || io_fail read
  SIZE=$s
}

# Ends a command's output with a newline when it is non-empty and lacks one.
# $1 = the log's size before the command ran.
fix_newline() {
  local last
  read_size
  if [ "$SIZE" -gt "$1" ]; then
    { last=$(tail -c 1 -- "$LOG" && printf x); } 2>/dev/null || io_fail read
    if [ "$last" != $'\nx' ]; then
      { printf '\n' >&3; } 2>/dev/null || io_fail write
    fi
  fi
}

# --- Record 1: the mutation against the commit. -----------------------------------------------

put "\$ git diff HEAD -- $file"
read_size
before=$SIZE
git diff HEAD -- "$file" </dev/null >&3 2>&3
rc=$?
fix_newline "$before"
put "exit=$rc"

# --- Record 2: the test. ----------------------------------------------------------------------

put "\$ $shown"
read_size
before=$SIZE
"${test_cmd[@]}" </dev/null >&3 2>&3
test_rc=$?
fix_newline "$before"
{ test_output=$(tail -c +"$((before + 1))" -- "$LOG"); } 2>/dev/null || io_fail read
put "exit=$test_rc"

# --- Stdout: the log path, the failing-test and assertion lines, the test's exit= line. --------

if [ "$mode" = node ]; then
  pattern='^not ok|✖|AssertionError|^\s+(expected|actual)'
else
  pattern='^\s*Failed |Assert|Shouldly|Expected|Actual|Error Message'
fi
printf '%s\n' "$LOG"
printf '%s\n' "$test_output" | grep -E -e "$pattern"
printf 'exit=%s\n' "$test_rc"
exit 0
