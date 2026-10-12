'use strict';

const { test, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const { runHook: spawnHook } = require('./run-hook');

const SCRIPT = path.join(__dirname, '..', 'coder-bash-guard.js');
const HOOK = 'coder-bash-guard';
const DEFAULT_PROJECT_DIR = 'C:\\projects\\envanex';

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

function runBash(command) {
  const stdin = JSON.stringify({ tool_name: 'Bash', tool_input: { command }, cwd: DEFAULT_PROJECT_DIR });
  return runHook(SCRIPT, [], stdin, { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
}

function readLogEntries(logDir) {
  return fs
    .readFileSync(path.join(logDir, 'hooks.jsonl'), 'utf8')
    .trim()
    .split('\n')
    .map((line) => JSON.parse(line));
}

function assertBlocked(result, reason) {
  assert.equal(result.status, 2, `expected a block, got ${result.status}; stderr: ${result.stderr}`);
  assert.ok(result.stderr.includes(`Blocked by ${HOOK}:`), result.stderr);
  assert.ok(result.stderr.includes(reason), `expected reason "${reason}" in stderr: ${result.stderr}`);
}

// An allow also proves that runGuard read the target through getCommand: the default
// path extractor throws on a Bash-shaped input, which would block.
function assertAllowed(command) {
  const result = runBash(command);
  assert.equal(result.status, 0, `expected an allow, got ${result.status}; stderr: ${result.stderr}`);
  const entries = readLogEntries(result.logDir);
  assert.equal(entries.length, 1);
  assert.equal(entries[0].decision, 'allow');
  assert.equal(entries[0].hook, HOOK);
  assert.equal(entries[0].target, command);
}

function notInReadAllowlist(subcommand) {
  return `git subcommand "${subcommand}" is not in the read allowlist`;
}

const UNPARSEABLE = 'unparseable git call';
const GH_BLOCKED = 'gh is blocked for the coder';

// Blocks: git writes

test('coder-bash-guard: blocks git commit -m x', () => {
  assertBlocked(runBash('git commit -m x'), notInReadAllowlist('commit'));
});

test('coder-bash-guard: blocks git add .', () => {
  assertBlocked(runBash('git add .'), notInReadAllowlist('add'));
});

test('coder-bash-guard: blocks git push', () => {
  assertBlocked(runBash('git push'), notInReadAllowlist('push'));
});

test('coder-bash-guard: blocks git stash list', () => {
  assertBlocked(runBash('git stash list'), notInReadAllowlist('stash'));
});

test('coder-bash-guard: blocks git reset --soft HEAD~1', () => {
  assertBlocked(runBash('git reset --soft HEAD~1'), notInReadAllowlist('reset'));
});

test('coder-bash-guard: blocks git checkout -b x', () => {
  assertBlocked(runBash('git checkout -b x'), notInReadAllowlist('checkout'));
});

test('coder-bash-guard: blocks git switch main', () => {
  assertBlocked(runBash('git switch main'), notInReadAllowlist('switch'));
});

test('coder-bash-guard: blocks git branch -D x', () => {
  assertBlocked(runBash('git branch -D x'), 'git branch argument "-D" is not allowed');
});

test('coder-bash-guard: blocks git tag v1', () => {
  assertBlocked(runBash('git tag v1'), notInReadAllowlist('tag'));
});

test('coder-bash-guard: blocks git merge x', () => {
  assertBlocked(runBash('git merge x'), notInReadAllowlist('merge'));
});

test('coder-bash-guard: blocks git rebase main', () => {
  assertBlocked(runBash('git rebase main'), notInReadAllowlist('rebase'));
});

test('coder-bash-guard: blocks git restore x', () => {
  assertBlocked(runBash('git restore x'), notInReadAllowlist('restore'));
});

test('coder-bash-guard: blocks git clean -n', () => {
  assertBlocked(runBash('git clean -n'), notInReadAllowlist('clean'));
});

test('coder-bash-guard: blocks git rm x', () => {
  assertBlocked(runBash('git rm x'), notInReadAllowlist('rm'));
});

test('coder-bash-guard: blocks git mv a b', () => {
  assertBlocked(runBash('git mv a b'), notInReadAllowlist('mv'));
});

test('coder-bash-guard: blocks git apply p', () => {
  assertBlocked(runBash('git apply p'), notInReadAllowlist('apply'));
});

test('coder-bash-guard: blocks git cherry-pick h', () => {
  assertBlocked(runBash('git cherry-pick h'), notInReadAllowlist('cherry-pick'));
});

test('coder-bash-guard: blocks git revert h', () => {
  assertBlocked(runBash('git revert h'), notInReadAllowlist('revert'));
});

test('coder-bash-guard: blocks git worktree add w', () => {
  assertBlocked(runBash('git worktree add w'), notInReadAllowlist('worktree'));
});

test('coder-bash-guard: blocks git config user.name x', () => {
  assertBlocked(runBash('git config user.name x'), notInReadAllowlist('config'));
});

// Blocks: global options

test('coder-bash-guard: blocks git -C src commit -m x', () => {
  assertBlocked(runBash('git -C src commit -m x'), notInReadAllowlist('commit'));
});

test('coder-bash-guard: blocks git -c user.name=x commit', () => {
  assertBlocked(runBash('git -c user.name=x commit'), notInReadAllowlist('commit'));
});

test('coder-bash-guard: blocks git --no-pager push', () => {
  assertBlocked(runBash('git --no-pager push'), notInReadAllowlist('push'));
});

test('coder-bash-guard: blocks git --git-dir=.git commit', () => {
  assertBlocked(runBash('git --git-dir=.git commit'), 'git global option "--git-dir=.git" is not allowed');
});

test('coder-bash-guard: blocks git --work-tree=. status', () => {
  assertBlocked(runBash('git --work-tree=. status'), 'git global option "--work-tree=." is not allowed');
});

test('coder-bash-guard: blocks GIT commit -m x (uppercase)', () => {
  assertBlocked(runBash('GIT commit -m x'), notInReadAllowlist('commit'));
});

// Blocks: wrapped calls

test('coder-bash-guard: blocks cd src && git add x', () => {
  assertBlocked(runBash('cd src && git add x'), notInReadAllowlist('add'));
});

test('coder-bash-guard: blocks bash -c "git commit -m x"', () => {
  assertBlocked(runBash('bash -c "git commit -m x"'), notInReadAllowlist('commit'));
});

test('coder-bash-guard: blocks node -e "require(\'child_process\').execSync(\'git push\')"', () => {
  assertBlocked(
    runBash('node -e "require(\'child_process\').execSync(\'git push\')"'),
    'git subcommand "push\')" is not in the read allowlist',
  );
});

// Blocks: gh, bare git, malformed input

test('coder-bash-guard: blocks gh pr create', () => {
  assertBlocked(runBash('gh pr create'), GH_BLOCKED);
});

test('coder-bash-guard: blocks gh pr list', () => {
  assertBlocked(runBash('gh pr list'), GH_BLOCKED);
});

test('coder-bash-guard: blocks git', () => {
  assertBlocked(runBash('git'), 'bare git call');
});

test('coder-bash-guard: blocks on malformed JSON', () => {
  const result = runHook(SCRIPT, [], '{not json', { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
  assertBlocked(result, 'guard error');
  assert.ok(result.stderr.includes('invalid hook input'), result.stderr);
});

// Blocks: documented false positives (fail closed)

test('coder-bash-guard: blocks ls tools/git/x (documented false positive)', () => {
  assertBlocked(runBash('ls tools/git/x'), UNPARSEABLE);
});

test('coder-bash-guard: blocks ls .git/hooks (documented false positive)', () => {
  assertBlocked(runBash('ls .git/hooks'), UNPARSEABLE);
});

test('coder-bash-guard: blocks grep -rn "git" docs (documented false positive)', () => {
  assertBlocked(runBash('grep -rn "git" docs'), notInReadAllowlist('docs'));
});

// Allows

test('coder-bash-guard: allows git status', () => {
  assertAllowed('git status');
});

test('coder-bash-guard: allows git status --short', () => {
  assertAllowed('git status --short');
});

test('coder-bash-guard: allows git diff --stat', () => {
  assertAllowed('git diff --stat');
});

test('coder-bash-guard: allows git diff --exit-code -- src/x.cs', () => {
  assertAllowed('git diff --exit-code -- src/x.cs');
});

test('coder-bash-guard: allows git log --oneline -5', () => {
  assertAllowed('git log --oneline -5');
});

test('coder-bash-guard: allows git show HEAD:x', () => {
  assertAllowed('git show HEAD:x');
});

test('coder-bash-guard: allows git ls-files', () => {
  assertAllowed('git ls-files');
});

test('coder-bash-guard: allows git branch --show-current', () => {
  assertAllowed('git branch --show-current');
});

test('coder-bash-guard: allows git rev-parse HEAD', () => {
  assertAllowed('git rev-parse HEAD');
});

test('coder-bash-guard: allows git merge-base main HEAD', () => {
  assertAllowed('git merge-base main HEAD');
});

test('coder-bash-guard: allows the Phase 1 base-check command line', () => {
  assertAllowed(
    'git rev-parse --verify origin/main && test "$(git merge-base main HEAD)" = "$(git merge-base origin/main HEAD)"; echo "base-check=$?"',
  );
});

test('coder-bash-guard: allows bash -c "git status"', () => {
  assertAllowed('bash -c "git status"');
});

test('coder-bash-guard: allows dotnet build -warnaserror', () => {
  assertAllowed('dotnet build -warnaserror');
});

test('coder-bash-guard: allows dotnet test --filter X', () => {
  assertAllowed('dotnet test --filter X');
});

test('coder-bash-guard: allows grep -rn "digit" src', () => {
  assertAllowed('grep -rn "digit" src');
});

test('coder-bash-guard: allows cat .gitignore', () => {
  assertAllowed('cat .gitignore');
});

test('coder-bash-guard: allows ls .github', () => {
  assertAllowed('ls .github');
});
