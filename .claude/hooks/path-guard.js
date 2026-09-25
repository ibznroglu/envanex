'use strict';

// PreToolUse guard for file tools: blocks build output, IDE state, VCS internals and secret files.

const { runGuard, normalizePath, block } = require('./lib/guard-common');

const PROTECTED_SEGMENTS = new Set(['bin', 'obj', 'packages', '.vs', '.idea', '.git']);
const PROTECTED_SUFFIXES = ['.env', '.user', '.pfx', '.snk', '.local.json'];

function protectedFileName(basename) {
  if (basename === '.env' || basename === 'secrets.json') {
    return true;
  }
  if (basename.startsWith('.env.') && basename !== '.env.example') {
    return true;
  }
  return PROTECTED_SUFFIXES.some((suffix) => basename.endsWith(suffix));
}

function decide(input, ctx) {
  const normalized = normalizePath(ctx.target, ctx.projectDir);
  const segments = normalized.split('/');
  for (const segment of segments) {
    if (PROTECTED_SEGMENTS.has(segment)) {
      block(ctx, `protected segment "${segment}" (build output, IDE state and VCS internals are off-limits)`);
    }
  }
  const basename = segments[segments.length - 1];
  if (protectedFileName(basename)) {
    block(ctx, 'protected file name (secret and local configuration files are off-limits)');
  }
}

runGuard('path-guard', decide);
