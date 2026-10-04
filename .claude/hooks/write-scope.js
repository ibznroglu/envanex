'use strict';

// PreToolUse guard for file tools: allows writes only under one project-relative directory.
// Usage: node write-scope.js <relative dir>. A missing argument or a path outside the project blocks.

const { runGuard, normalizePath, block } = require('./lib/guard-common');

const scopeArg = process.argv[2];

function decide(input, ctx) {
  if (typeof scopeArg !== 'string' || scopeArg === '') {
    block(ctx, 'missing scope argument');
  }
  const scope = normalizePath(scopeArg, ctx.projectDir);
  if (!scope.startsWith(`${ctx.projectDir}/`)) {
    block(ctx, `scope "${scopeArg}" is outside the project`);
  }
  const normalized = normalizePath(ctx.target, ctx.projectDir);
  if (!normalized.startsWith(`${ctx.projectDir}/`)) {
    block(ctx, 'target is outside the project');
  }
  // The trailing '/' rejects sibling directories such as reviews-evil.
  if (!normalized.startsWith(`${scope}/`)) {
    block(ctx, `target is outside the write scope "${scopeArg}"`);
  }
}

runGuard(`write-scope:${scopeArg ?? ''}`, decide);
