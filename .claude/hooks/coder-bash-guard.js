'use strict';

// PreToolUse guard for the coder's Bash calls: allows read-only git only, and blocks gh.
// Scans the whole command string, so wrapped calls (bash -c, node -e, $(...), && chains) are seen.
// Any command that names `git` as a word outside a git call is blocked too (documented false positive).

const { runGuard, getCommand, block } = require('./lib/guard-common');

const GIT_WORD = /\bgit(\.exe)?\b/gi;
const GH_WORD = /\bgh\b/i;

const READ_SUBCOMMANDS = new Set([
  'status',
  'diff',
  'log',
  'show',
  'ls-files',
  'ls-tree',
  'rev-parse',
  'merge-base',
  'blame',
  'grep',
  'cat-file',
  'shortlog',
  'describe',
]);

const BRANCH_READ_ARGS = new Set(['--show-current', '--list', '-a', '-r', '-v', '-vv']);

// Characters that end the argument list of a `git branch` call inside a larger command.
const ARGUMENT_LIST_END = /[;&|)`\r\n]/;

function isWhitespace(ch) {
  return /\s/.test(ch);
}

function skipWhitespace(s, pos) {
  let p = pos;
  while (p < s.length && isWhitespace(s[p])) {
    p += 1;
  }
  return p;
}

function tokenEnd(s, pos) {
  let p = pos;
  while (p < s.length && !isWhitespace(s[p])) {
    p += 1;
  }
  return p;
}

function stripQuotes(token) {
  return token.replace(/^["']+|["']+$/g, '');
}

function checkBranchArgs(s, pos) {
  let rest = s.slice(pos);
  const end = ARGUMENT_LIST_END.exec(rest);
  if (end) {
    rest = rest.slice(0, end.index);
  }
  const args = rest
    .split(/\s+/)
    .map(stripQuotes)
    .filter((arg) => arg !== '');
  for (const arg of args) {
    if (!BRANCH_READ_ARGS.has(arg)) {
      return `git branch argument "${arg}" is not allowed (only bare, --show-current, --list, -a, -r, -v or -vv)`;
    }
  }
  return null;
}

// Checks one git call that starts right after a `git` match. Returns a block reason, or null.
function checkGitCall(s, afterMatch) {
  let pos = afterMatch;
  // Step 1: skip one quote right after the match.
  if (s[pos] === '"' || s[pos] === "'") {
    pos += 1;
  }
  // Step 2: bare git, or something glued to the word (.git/hooks, tools/git/x, --git-dir=x).
  if (pos >= s.length) {
    return 'bare git call';
  }
  if (!isWhitespace(s[pos])) {
    return 'unparseable git call (no whitespace after "git")';
  }
  // Step 3: skip whitespace and the allowed global options.
  pos = skipWhitespace(s, pos);
  for (;;) {
    if (pos >= s.length) {
      return 'git call without a subcommand';
    }
    const end = tokenEnd(s, pos);
    const word = stripQuotes(s.slice(pos, end));
    if (word === '-C' || word === '-c') {
      pos = skipWhitespace(s, end);
      if (pos >= s.length) {
        return `git option "${word}" without an argument`;
      }
      pos = skipWhitespace(s, tokenEnd(s, pos));
      continue;
    }
    if (word === '--no-pager' || word === '-P') {
      pos = skipWhitespace(s, end);
      continue;
    }
    if (word.startsWith('-')) {
      return `git global option "${word}" is not allowed`;
    }
    // Step 4: the subcommand must be in the read allowlist.
    if (word === 'branch') {
      return checkBranchArgs(s, end);
    }
    if (READ_SUBCOMMANDS.has(word)) {
      return null;
    }
    return `git subcommand "${word}" is not in the read allowlist`;
  }
}

function decide(input, ctx) {
  const command = ctx.target;
  for (const match of command.matchAll(GIT_WORD)) {
    const reason = checkGitCall(command, match.index + match[0].length);
    if (reason !== null) {
      block(ctx, reason);
    }
  }
  if (GH_WORD.test(command)) {
    block(ctx, 'gh is blocked for the coder');
  }
}

runGuard('coder-bash-guard', decide, getCommand);
