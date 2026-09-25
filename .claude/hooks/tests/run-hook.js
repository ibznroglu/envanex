'use strict';

// Test helper: spawns a hook script with JSON on stdin and an isolated hook-log dir,
// so no test ever writes to the project's hook log.

const childProcess = require('child_process');
const fs = require('fs');
const os = require('os');

function runHook(scriptPath, args, stdinText, env) {
  const childEnv = { ...process.env };
  const overrides = env || {};
  if (!Object.prototype.hasOwnProperty.call(overrides, 'ENVANEX_HOOK_LOG_DIR')) {
    childEnv.ENVANEX_HOOK_LOG_DIR = fs.mkdtempSync(`${os.tmpdir()}/envanex-hooklog-`);
  }
  for (const [key, value] of Object.entries(overrides)) {
    if (value === undefined) {
      delete childEnv[key];
    } else {
      childEnv[key] = value;
    }
  }
  const result = childProcess.spawnSync(process.execPath, [scriptPath, ...args], {
    input: stdinText,
    env: childEnv,
    encoding: 'utf8',
  });
  if (result.error) {
    throw result.error;
  }
  return {
    status: result.status,
    stdout: result.stdout,
    stderr: result.stderr,
    logDir: childEnv.ENVANEX_HOOK_LOG_DIR,
  };
}

module.exports = { runHook };
