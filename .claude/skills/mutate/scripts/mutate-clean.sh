#!/usr/bin/env bash
# Checks that every given file equals HEAD in both the working tree and the index, and appends
# the raw record to TestResults/<slug>/mutations/<label>.txt, where <slug> is the current branch
# with "/" replaced by "-". The records are printed as well.
#
# Usage: bash .claude/skills/mutate/scripts/mutate-clean.sh <label> <file>...
#
# Each <file> is a path from the repository root. The log records, in order,
# `git diff --exit-code HEAD -- <files>` (working tree against HEAD) and
# `git diff --cached --exit-code -- <files>` (index against HEAD), each with its raw output and
# an `exit=` line. The script never changes the files, the index or HEAD.
#
# Exit status:
#   0  both diffs exit 0: the files equal HEAD in the index and in the working tree
#   1  a diff exits 1, and neither git call failed otherwise
#   2  an argument or start-up failure (nothing written), or a git call exiting with another code
#   3  a log I/O failure
#
# Every exit-2 or exit-3 path prints exactly one line to stderr, "mutate-clean.sh: <reason>".

set -u

fail() {
  printf 'mutate-clean.sh: %s\n' "$2" >&2
  exit "$1"
}

usage() {
  fail 2 'usage: mutate-clean.sh <label> <file>...'
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

[ "$#" -ge 2 ] || usage
label=$1
shift
valid_label "$label" || usage

# --- Start-up: exit 2, nothing written. -------------------------------------------------------

top=$(git rev-parse --show-toplevel 2>/dev/null) && [ -n "$top" ] || fail 2 'not inside a git work tree'
cd -- "$top" >/dev/null 2>&1 || fail 2 "cannot cd to $top"
branch=$(git branch --show-current 2>/dev/null) && [ -n "$branch" ] || fail 2 'no current branch'
for file in "$@"; do
  git ls-files --error-unmatch -- "$file" >/dev/null 2>&1 || fail 2 "untracked file $file"
done
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

# record <git diff arguments...>: one `$ git diff ...` record. Sets RC to git's exit code.
record() {
  local before
  put "\$ git diff $*"
  read_size
  before=$SIZE
  git diff "$@" </dev/null >&3 2>&3
  RC=$?
  fix_newline "$before"
  put "exit=$RC"
}

# --- Records, then print them. ----------------------------------------------------------------

read_size
start=$SIZE
record --exit-code HEAD -- "$@"
tree_rc=$RC
record --cached --exit-code -- "$@"
index_rc=$RC
tail -c +"$((start + 1))" -- "$LOG" 2>/dev/null || io_fail read

for rc in "$tree_rc" "$index_rc"; do
  case $rc in
    0 | 1) ;;
    *) fail 2 "git diff failed with exit $rc" ;;
  esac
done
[ "$tree_rc" -eq 0 ] && [ "$index_rc" -eq 0 ] && exit 0
exit 1
