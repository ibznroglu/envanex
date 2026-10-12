'use strict';

// PreToolUse guard for the tester's and explainer's Bash calls: a token-wise allowlist per profile.
// Usage: node bash-allowlist.js <profile>. A missing or unknown profile blocks.
// The pre-check and the pipe split ignore quotes on purpose, so they fail closed.

const { runGuard, getCommand, block } = require('./lib/guard-common');

const profileName = process.argv[2];

const PIPE_COMMANDS = new Set(['grep', 'head', 'tail', 'wc']);
const READ_TOOLS = ['grep', 'cat', 'ls', 'wc', 'head', 'tail'];

const NODE_TEST_FILE = /^\.claude\/hooks\/tests\/([A-Za-z0-9_-]+|\*)\.test\.js$/;

const DOTNET_FLAGS = new Set(['-warnaserror', '--no-restore', '2>&1']);
const DOTNET_VERBOSITY = new Set(['q', 'quiet', 'm', 'minimal', 'n', 'normal', 'd', 'detailed', 'diag', 'diagnostic']);
const DOTNET_PROJECT_PATH = /^(src|tests)\/[A-Za-z0-9_./-]+$/;

// Entry kinds:
// - exact: no further tokens
// - prefix: any further tokens that pass the pre-check and the --output rule
// - dotnet: only the further tokens of the fixed dotnet list
// - node-test: only test files under .claude/hooks/tests
function entry(kind, command) {
  return { kind, tokens: command.split(' ') };
}

const exact = (command) => entry('exact', command);
const prefix = (command) => entry('prefix', command);
const dotnet = (command) => entry('dotnet', command);
const NODE_TEST = entry('node-test', 'node --test');

const PROFILES = {
  tester: [
    exact('bash .claude/skills/gate/scripts/gate.sh'),
    prefix('git status'),
    prefix('git diff'),
    prefix('git log'),
    prefix('git show'),
    prefix('git ls-files'),
    prefix('git rev-parse'),
    exact('git branch --show-current'),
    dotnet('dotnet build'),
    dotnet('dotnet test'),
    dotnet('dotnet format --verify-no-changes'),
    NODE_TEST,
    prefix('docker compose ps'),
    ...READ_TOOLS.map(prefix),
  ],
  explainer: [
    prefix('git status'),
    prefix('git diff'),
    prefix('git log'),
    prefix('git show'),
    prefix('dotnet list package'),
    ...READ_TOOLS.map(prefix),
  ],
};

// Quote-unaware on purpose: characters inside quotes count too.
function precheck(command) {
  if (/[\r\n]/.test(command)) {
    return 'a newline is not allowed';
  }
  if (command.includes(';')) {
    return 'chaining with ";" is not allowed';
  }
  if (command.includes('&&')) {
    return 'chaining with "&&" is not allowed';
  }
  if (command.includes('||')) {
    return 'chaining with "||" is not allowed';
  }
  if (command.includes('`')) {
    return 'command substitution with a backtick is not allowed';
  }
  if (command.includes('$(')) {
    return 'command substitution with "$(" is not allowed';
  }
  if (command.includes('<(') || command.includes('>(')) {
    return 'process substitution is not allowed';
  }
  const withoutStderrMerge = command
    .split(/\s+/)
    .filter((token) => token !== '2>&1')
    .join(' ');
  if (withoutStderrMerge.includes('&')) {
    return 'a background "&" is not allowed';
  }
  if (withoutStderrMerge.includes('>')) {
    return 'redirection with ">" is not allowed (only the token 2>&1)';
  }
  return null;
}

function startsWithTokens(tokens, entryTokens) {
  return tokens.length >= entryTokens.length && entryTokens.every((token, i) => tokens[i] === token);
}

function checkNodeTestArgs(args) {
  if (args.length === 0) {
    return 'node --test needs at least one test file';
  }
  for (const arg of args) {
    if (arg.includes('..') || !NODE_TEST_FILE.test(arg)) {
      return `node --test argument "${arg}" is not a test file under .claude/hooks/tests`;
    }
  }
  return null;
}

function checkDotnetArgs(args) {
  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (DOTNET_FLAGS.has(arg)) {
      continue;
    }
    if (arg === '--filter') {
      const value = args[i + 1];
      if (value === undefined || value.startsWith('-') || value.startsWith('/')) {
        return '--filter needs exactly one argument that does not start with - or /';
      }
      i += 1;
      continue;
    }
    if (arg === '-v' || arg === '--verbosity') {
      const value = args[i + 1];
      if (!DOTNET_VERBOSITY.has(value)) {
        return `${arg} needs one of ${[...DOTNET_VERBOSITY].join(', ')}`;
      }
      i += 1;
      continue;
    }
    if (DOTNET_PROJECT_PATH.test(arg)) {
      if (arg.split('/').includes('..')) {
        return `dotnet project path "${arg}" has a .. segment`;
      }
      continue;
    }
    return `dotnet token "${arg}" is not allowed`;
  }
  return null;
}

function checkFurtherTokens(matched, args) {
  switch (matched.kind) {
    case 'exact':
      return args.length === 0 ? null : `exact entry "${matched.tokens.join(' ')}" takes no further tokens`;
    case 'prefix':
      return null;
    case 'node-test':
      return checkNodeTestArgs(args);
    case 'dotnet':
      return checkDotnetArgs(args);
    default:
      return `unknown entry kind "${matched.kind}"`;
  }
}

function decide(input, ctx) {
  if (typeof profileName !== 'string' || profileName === '') {
    block(ctx, 'missing profile argument');
  }
  if (!Object.prototype.hasOwnProperty.call(PROFILES, profileName)) {
    block(ctx, `unknown profile "${profileName}"`);
  }
  const entries = PROFILES[profileName];
  const command = ctx.target;

  const preReason = precheck(command);
  if (preReason !== null) {
    block(ctx, preReason);
  }

  // Split on every `|`, quote-unaware, so `grep -E "a|b"` blocks (documented, fails closed).
  const segments = command.split('|').map((segment) => segment.split(/\s+/).filter((token) => token !== ''));
  for (const tokens of segments) {
    if (tokens.length === 0) {
      block(ctx, 'an empty pipe segment is not allowed');
    }
    for (const token of tokens) {
      if (token.startsWith('--output')) {
        block(ctx, `option "${token}" is not allowed`);
      }
    }
  }

  const [first, ...later] = segments;
  const matched = entries.find((candidate) => startsWithTokens(first, candidate.tokens));
  if (matched === undefined) {
    block(ctx, `command is not in the ${profileName} allowlist`);
  }
  const furtherReason = checkFurtherTokens(matched, first.slice(matched.tokens.length));
  if (furtherReason !== null) {
    block(ctx, furtherReason);
  }

  for (const tokens of later) {
    if (!PIPE_COMMANDS.has(tokens[0])) {
      block(ctx, `piping into "${tokens[0]}" is not allowed (only grep, head, tail or wc)`);
    }
  }
}

runGuard(`bash-allowlist:${profileName ?? ''}`, decide, getCommand);
