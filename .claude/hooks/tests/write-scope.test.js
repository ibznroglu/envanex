'use strict';

const { test, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const { runHook: spawnHook } = require('./run-hook');

const SCRIPT = path.join(__dirname, '..', 'write-scope.js');
const DEFAULT_PROJECT_DIR = 'C:\\projects\\envanex';
const GIT_BASH_PROJECT_DIR = '/c/projects/envanex';
const REVIEWS = 'thoughts/shared/reviews';

const logDirs = [];

function runHook(...args) {
  const result = spawnHook(...args);
  logDirs.push(result.logDir);
  return result;
}

after(() => {
  for (const dir of logDirs) {
    if (dir === undefined) {
      continue;
    }
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

function runWrite(scopeArgs, filePath, projectDir = DEFAULT_PROJECT_DIR) {
  const stdin = JSON.stringify({ tool_name: 'Write', tool_input: { file_path: filePath } });
  return runHook(SCRIPT, scopeArgs, stdin, { CLAUDE_PROJECT_DIR: projectDir });
}

function readLogEntries(logDir) {
  return fs
    .readFileSync(path.join(logDir, 'hooks.jsonl'), 'utf8')
    .trim()
    .split('\n')
    .map((line) => JSON.parse(line));
}

function assertBlocked(result, hook, reason) {
  assert.equal(result.status, 2, `expected a block, got ${result.status}; stderr: ${result.stderr}`);
  assert.ok(result.stderr.includes(`Blocked by ${hook}:`), result.stderr);
  assert.ok(result.stderr.includes(reason), `expected reason "${reason}" in stderr: ${result.stderr}`);
}

function assertAllowed(result, scope) {
  assert.equal(result.status, 0, `expected an allow, got ${result.status}; stderr: ${result.stderr}`);
  const entries = readLogEntries(result.logDir);
  assert.equal(entries.length, 1);
  assert.equal(entries[0].decision, 'allow');
  assert.equal(entries[0].hook, `write-scope:${scope}`);
}

function assertBlockedInReviews(filePath, reason) {
  assertBlocked(runWrite([REVIEWS], filePath), `write-scope:${REVIEWS}`, reason);
}

const OUTSIDE_REVIEWS = `target is outside the write scope "${REVIEWS}"`;

// Allows

test('write-scope: allows an absolute backslash target under thoughts/shared/reviews', () => {
  assertAllowed(runWrite([REVIEWS], 'C:\\projects\\envanex\\thoughts\\shared\\reviews\\2026-09-25_x.md'), REVIEWS);
});

test('write-scope: allows a relative target under thoughts/shared/reviews', () => {
  assertAllowed(runWrite([REVIEWS], 'thoughts/shared/reviews/x.md'), REVIEWS);
});

test('write-scope: allows a mixed-case target under thoughts/shared/reviews', () => {
  assertAllowed(runWrite([REVIEWS], 'Thoughts\\Shared\\Reviews\\x.md'), REVIEWS);
});

test('write-scope: allows a git-bash-style target with a git-bash-style project dir', () => {
  assertAllowed(
    runWrite([REVIEWS], '/c/projects/envanex/thoughts/shared/reviews/x.md', GIT_BASH_PROJECT_DIR),
    REVIEWS,
  );
});

test('write-scope: allows a backslash target with a git-bash-style project dir', () => {
  assertAllowed(
    runWrite([REVIEWS], 'C:\\projects\\envanex\\thoughts\\shared\\reviews\\x.md', GIT_BASH_PROJECT_DIR),
    REVIEWS,
  );
});

// Blocks

test('write-scope: blocks src\\x.cs', () => {
  assertBlockedInReviews('src\\x.cs', OUTSIDE_REVIEWS);
});

test('write-scope: blocks thoughts\\shared\\reviews-evil\\x.md', () => {
  assertBlockedInReviews('thoughts\\shared\\reviews-evil\\x.md', OUTSIDE_REVIEWS);
});

test('write-scope: blocks thoughts/shared/reviews/../plans/x.md', () => {
  assertBlockedInReviews('thoughts/shared/reviews/../plans/x.md', OUTSIDE_REVIEWS);
});

test('write-scope: blocks a target outside the project', () => {
  assertBlockedInReviews('C:\\Users\\jesus\\AppData\\Local\\Temp\\x.md', 'target is outside the project');
});

test('write-scope: blocks a missing argument', () => {
  assertBlocked(runWrite([], 'thoughts/shared/reviews/x.md'), 'write-scope:', 'missing scope argument');
});

test('write-scope: blocks on malformed JSON', () => {
  const result = runHook(SCRIPT, [REVIEWS], '{not json', { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
  assertBlocked(result, `write-scope:${REVIEWS}`, 'guard error');
  assert.ok(result.stderr.includes('invalid hook input'), result.stderr);
});

// Other scopes

test('write-scope: allows docs/journal/0015-x.md under docs/journal', () => {
  assertAllowed(runWrite(['docs/journal'], 'docs/journal/0015-x.md'), 'docs/journal');
});

test('write-scope: allows TestResults/chore-agents-hardening/tester-verdict.md under TestResults', () => {
  assertAllowed(runWrite(['TestResults'], 'TestResults/chore-agents-hardening/tester-verdict.md'), 'TestResults');
});
