#!/usr/bin/env node
import crypto from 'crypto';
import fs from 'fs';
import os from 'os';
import path from 'path';

const SALT = 'ARKAIOS_SECRET_KEY_2026_NEXUS';
const DEFAULT_API = process.env.ARKAIOS_DJ_LICENSE_API || 'http://127.0.0.1:3000';

function arg(name, fallback = '') {
  const prefix = `--${name}=`;
  const found = process.argv.find((item) => item.startsWith(prefix));
  return found ? found.slice(prefix.length) : fallback;
}

function hasFlag(name) {
  return process.argv.includes(`--${name}`);
}

function getHardwareId() {
  const interfaces = os.networkInterfaces();
  for (const entries of Object.values(interfaces)) {
    for (const item of entries || []) {
      if (!item.internal && item.mac && item.mac !== '00:00:00:00:00:00') {
        return item.mac.replace(/:/g, '').toUpperCase();
      }
    }
  }
  return `HWID_NOT_FOUND_${os.hostname()}`;
}

function sha256(input) {
  return crypto.createHash('sha256').update(input).digest('hex');
}

function createKey({ hwid, type, name, phone, timestamp }) {
  const normalizedType = type.toUpperCase();
  if (!['BASIC', 'LIFETIME'].includes(normalizedType)) {
    throw new Error('type must be BASIC or LIFETIME');
  }
  const dataToSign = `${hwid}|${normalizedType}|${name}|${phone}|${timestamp}|${SALT}`;
  const signature = sha256(dataToSign);
  const payload = `${hwid}|${normalizedType}|${name}|${phone}|${timestamp}|${signature}`;
  return Buffer.from(payload, 'utf8').toString('base64');
}

function decodeKey(key) {
  const decoded = Buffer.from(key, 'base64').toString('utf8');
  const [hwid, type, name, phone, timestamp, signature] = decoded.split('|');
  return { hwid, type, name, phone, timestamp, signature, decoded };
}

function validateKey(key) {
  const parts = decodeKey(key);
  const expected = sha256(`${parts.hwid}|${parts.type}|${parts.name}|${parts.phone}|${parts.timestamp}|${SALT}`);
  return parts.signature === expected;
}

async function registerKey(apiBase, key, details) {
  const response = await fetch(`${apiBase.replace(/\/$/, '')}/api/licenses/add`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ key, ...details })
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok || body.success === false) {
    throw new Error(body.message || `registration failed with HTTP ${response.status}`);
  }
  return body;
}

async function validateOnServer(apiBase, key, hwid) {
  const response = await fetch(`${apiBase.replace(/\/$/, '')}/api/licenses/validate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ key, hwid })
  });
  const body = await response.json().catch(() => ({}));
  return { ok: response.ok && body.success !== false, status: response.status, body };
}

function installLocal(key) {
  const appData = process.env.APPDATA || path.join(os.homedir(), 'AppData', 'Roaming');
  const dir = path.join(appData, 'ArkaiosDJNexus');
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, 'license.key');
  fs.writeFileSync(file, key, 'utf8');
  return file;
}

async function main() {
  const command = process.argv[2] || 'help';
  if (command === 'hwid') {
    console.log(getHardwareId());
    return;
  }

  if (command === 'generate') {
    const type = arg('type', 'BASIC').toUpperCase();
    const hwid = arg('hwid', type === 'LIFETIME' ? 'ARKAIOS-LIFETIME-UNIVERSAL' : getHardwareId());
    const name = arg('name', 'Arkaios User');
    const phone = arg('phone', '');
    const timestamp = arg('timestamp', new Date().toISOString());
    const apiBase = arg('api', DEFAULT_API);
    const key = createKey({ hwid, type, name, phone, timestamp });
    const localValid = validateKey(key);
    const decoded = decodeKey(key);

    const result = { key, localValid, license: decoded };

    if (hasFlag('register')) {
      result.registration = await registerKey(apiBase, key, { hwid, type, name, phone });
    }

    if (hasFlag('validate-server')) {
      result.serverValidation = await validateOnServer(apiBase, key, hwid);
    }

    if (hasFlag('install-local')) {
      result.installedAt = installLocal(key);
    }

    console.log(JSON.stringify(result, null, 2));
    return;
  }

  if (command === 'validate') {
    const key = arg('key');
    if (!key) throw new Error('missing --key=');
    const decoded = decodeKey(key);
    console.log(JSON.stringify({ localValid: validateKey(key), license: decoded }, null, 2));
    return;
  }

  console.log(`Usage:
  node tools/dj-license-tool.mjs hwid
  node tools/dj-license-tool.mjs generate --type=BASIC --name="Name" --phone="Phone" [--register] [--validate-server] [--install-local]
  node tools/dj-license-tool.mjs generate --type=LIFETIME --name="Name" --phone="Phone" [--register] [--validate-server] [--install-local]
  node tools/dj-license-tool.mjs validate --key="BASE64_KEY"

Environment:
  ARKAIOS_DJ_LICENSE_API=http://127.0.0.1:3000
`);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});

