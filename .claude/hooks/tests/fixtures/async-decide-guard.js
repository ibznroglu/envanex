'use strict';

// Test fixture: a guard whose decide is async, so the tests can prove that runGuard awaits it.
// Usage: node async-decide-guard.js <mode>
//   reject  decide rejects after a microtask
//   block   decide blocks after a microtask
// Any other mode rejects with "unknown fixture mode".

const { runGuard, block } = require('../../lib/guard-common');

const mode = process.argv[2];

async function decide(input, ctx) {
  await Promise.resolve();
  if (mode === 'reject') {
    throw new Error('async decide rejected');
  }
  if (mode === 'block') {
    block(ctx, 'async decide blocked');
    return;
  }
  throw new Error('unknown fixture mode');
}

runGuard('async-decide-fixture', decide);
