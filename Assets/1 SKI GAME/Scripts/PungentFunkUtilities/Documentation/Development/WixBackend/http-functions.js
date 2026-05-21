import { ok, badRequest, serverError, forbidden } from 'wix-http-functions';
import { fetch } from 'wix-fetch';
import wixData from 'wix-data';
import { getSecret } from 'wix-secrets-backend';

const DISCORD_DEFAULT_SECRET_NAME = 'PFU_DISCORD_WEBHOOK_URL';
const DISCORD_SUPPORT_SECRET_NAME = 'PFU_DISCORD_WEBHOOK_SUPPORT';
const DISCORD_CRITICAL_SECRET_NAME = 'PFU_DISCORD_WEBHOOK_CRITICAL';
const DISCORD_FEATURE_SECRET_NAME = 'PFU_DISCORD_WEBHOOK_FEATURES';
const COLLECTION = 'BugReports';
const MAX_DETAILS_LENGTH = 4000;
const MAX_TITLE_LENGTH = 140;
const MAX_PER_INSTALL_PER_HOUR = 8;

const JSON_HEADERS = {
  'Content-Type': 'application/json',
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type'
};

export async function post_bugReport(request) {
  try {
    const payload = await request.body.json();
    const validation = validatePayload(payload);

    if (!validation.ok) {
      return badRequest({
        headers: JSON_HEADERS,
        body: { ok: false, message: validation.message }
      });
    }

    const rate = await checkRateLimit(payload);
    if (!rate.ok) {
      return forbidden({
        headers: JSON_HEADERS,
        body: { ok: false, message: rate.message }
      });
    }

    const record = await saveReport(payload);
    await sendDiscordNotification(payload, record._id);

    return ok({
      headers: JSON_HEADERS,
      body: {
        ok: true,
        id: record._id,
        message: 'Report received.'
      }
    });
  } catch (error) {
    console.error('PFU bug report relay failed:', error);

    return serverError({
      headers: JSON_HEADERS,
      body: {
        ok: false,
        message: 'Bug report relay failed.'
      }
    });
  }
}

export function use_bugReport(request) {
  if (request.method === 'OPTIONS') {
    return ok({
      headers: JSON_HEADERS,
      body: { ok: true }
    });
  }

  return forbidden({
    headers: JSON_HEADERS,
    body: { ok: false, message: 'Use POST.' }
  });
}

function validatePayload(payload) {
  if (!payload || typeof payload !== 'object') {
    return { ok: false, message: 'Missing JSON payload.' };
  }

  if (!payload.title || String(payload.title).trim().length < 3) {
    return { ok: false, message: 'Title is required.' };
  }

  if (!payload.details || String(payload.details).trim().length < 10) {
    return { ok: false, message: 'Details are required.' };
  }

  if (String(payload.title).length > MAX_TITLE_LENGTH) {
    return { ok: false, message: 'Title is too long.' };
  }

  if (String(payload.details).length > MAX_DETAILS_LENGTH) {
    return { ok: false, message: 'Details are too long.' };
  }

  return { ok: true };
}

async function checkRateLimit(payload) {
  const installId = clean(payload.anonymousInstallId) || clean(payload.installId);
  if (!installId) {
    return { ok: true };
  }

  const oneHourAgo = new Date(Date.now() - 60 * 60 * 1000);

  const result = await wixData.query(COLLECTION)
    .eq('anonymousInstallId', installId)
    .ge('_createdDate', oneHourAgo)
    .limit(MAX_PER_INSTALL_PER_HOUR + 1)
    .find({ suppressAuth: true });

  if (result.items.length >= MAX_PER_INSTALL_PER_HOUR) {
    return {
      ok: false,
      message: 'Too many reports from this install. Please try again later.'
    };
  }

  return { ok: true };
}

async function saveReport(payload) {
  const item = {
    schemaVersion: clean(payload.schemaVersion),
    title: clean(payload.title),
    details: clean(payload.details),
    category: requestCategoryLabel(payload),
    severity: requestSeverityLabel(payload),
    utilityId: clean(payload.utilityId),
    sectionId: clean(payload.sectionId),
    topicId: clean(payload.topicId),
    contextLabel: clean(payload.contextLabel),
    contextPath: clean(payload.contextPath),
    sourceWindow: clean(payload.sourceWindow),
    helpTab: clean(payload.helpTab),
    packageVersion: clean(payload.packageVersion),
    unityVersion: clean(payload.unityVersion),
    editorPlatform: clean(payload.editorPlatform),
    timestampUtc: clean(payload.timestampUtc),
    anonymousInstallId: clean(payload.anonymousInstallId) || clean(payload.installId),
    includeDiagnostics: Boolean(payload.includeDiagnostics),
    includeConsoleSummary: Boolean(payload.includeConsoleSummary),
    contactEmail: clean(payload.contactEmail) || clean(payload.email),
    contactDiscord: clean(payload.contactDiscord),
    parentLocalId: clean(payload.parentLocalId),
    parentRemoteReportId: clean(payload.parentRemoteReportId),
    diagnosticsSummary: clean(payload.diagnosticsSummary),
    recentExceptionSummary: clean(payload.recentExceptionSummary),
    backendStatus: 'Received by Wix relay'
  };

  return wixData.insert(COLLECTION, item, { suppressAuth: true });
}

async function sendDiscordNotification(payload, reportId) {
  try {
    const route = await resolveDiscordRoute(payload);

    if (!route.webhookUrl) {
      console.warn('PFU Discord webhook secret is missing. Report was stored but no Discord notification was sent.');
      return;
    }

    const discordPayload = {
      username: 'PungentFunk Support Requests',
      embeds: [
        {
          title: truncate(clean(payload.title), 256),
          description: truncate(clean(payload.details), 1000),
          color: colorForSeverity(requestSeverityKey(payload)),
          timestamp: new Date().toISOString(),
          fields: compactFields([
            { name: 'Report ID', value: reportId },
            { name: 'Route', value: route.label },
            { name: 'Category', value: requestCategoryLabel(payload) || '(unknown)' },
            { name: 'Priority', value: requestSeverityLabel(payload) || '(unknown)' },
            { name: 'Utility', value: clean(payload.utilityId) || '(none)' },
            { name: 'Topic', value: clean(payload.topicId) || '(none)' },
            { name: 'Context', value: truncate(clean(payload.contextPath), 1024) || '(none)' },
            { name: 'Unity', value: clean(payload.unityVersion) || '(unknown)' },
            { name: 'Platform', value: truncate(clean(payload.editorPlatform), 1024) || '(unknown)' },
            { name: 'Contact Email', value: clean(payload.contactEmail) || clean(payload.email) || '(not provided)' },
            { name: 'Discord Handle', value: clean(payload.contactDiscord) || '(not provided)' },
            { name: 'Follow-up Local ID', value: clean(payload.parentLocalId) || '(none)' },
            { name: 'Follow-up Remote ID', value: clean(payload.parentRemoteReportId) || '(none)' }
          ])
        }
      ]
    };

    const response = await fetch(route.webhookUrl, {
      method: 'post',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(discordPayload)
    });

    if (!response.ok) {
      const body = await response.text();
      console.error(`Discord webhook failed: ${response.status} ${body}`);
    }
  } catch (error) {
    console.error('PFU Discord notification failed. Report was stored successfully:', error);
  }
}

async function resolveDiscordRoute(payload) {
  const severityKey = requestSeverityKey(payload);
  if (severityKey === 'blocking' || severityKey === 'dataloss' || severityKey === '3' || severityKey === '4') {
    return {
      webhookUrl: await getFirstOptionalSecret([DISCORD_CRITICAL_SECRET_NAME, DISCORD_SUPPORT_SECRET_NAME, DISCORD_DEFAULT_SECRET_NAME]),
      label: 'Critical Requests'
    };
  }

  const categoryKey = requestCategoryKey(payload);
  if (categoryKey === 'missingfeature' || categoryKey === 'featurerequest' || categoryKey === '6') {
    return {
      webhookUrl: await getFirstOptionalSecret([DISCORD_FEATURE_SECRET_NAME, DISCORD_SUPPORT_SECRET_NAME, DISCORD_DEFAULT_SECRET_NAME]),
      label: 'Feature Requests'
    };
  }

  return {
    webhookUrl: await getFirstOptionalSecret([DISCORD_SUPPORT_SECRET_NAME, DISCORD_DEFAULT_SECRET_NAME]),
    label: 'Support Requests'
  };
}

async function getFirstOptionalSecret(secretNames) {
  for (const secretName of secretNames) {
    const value = await getOptionalSecret(secretName);
    if (value) {
      return value;
    }
  }

  return '';
}

async function getOptionalSecret(secretName) {
  if (!secretName) {
    return '';
  }

  try {
    return clean(await getSecret(secretName));
  } catch (error) {
    console.warn(`PFU optional Wix secret is not configured: ${secretName}`);
    return '';
  }
}

function routeKey(value) {
  return clean(value).toLowerCase().replace(/[^a-z0-9]/g, '');
}

function requestCategoryKey(payload) {
  return routeKey(clean(payload.categoryLabel) || clean(payload.category));
}

function requestSeverityKey(payload) {
  return routeKey(clean(payload.severityLabel) || clean(payload.severity));
}

function requestCategoryLabel(payload) {
  const key = requestCategoryKey(payload);
  if (key === 'missingfeature' || key === 'featurerequest' || key === '6') {
    return 'Feature Request';
  }

  return 'Bug Report';
}

function requestSeverityLabel(payload) {
  const key = requestSeverityKey(payload);
  switch (key) {
    case '0':
    case 'low':
      return '1 Low';
    case '1':
    case 'normal':
      return '2 Normal';
    case '2':
    case 'high':
      return '3 High';
    case '3':
    case 'blocking':
      return '4 Blocking';
    case '4':
    case 'dataloss':
      return '5 Data Loss';
    default:
      return clean(payload.severityLabel) || clean(payload.severity) || '2 Normal';
  }
}

function compactFields(fields) {
  return fields
    .filter(field => field && field.name && field.value)
    .slice(0, 25)
    .map(field => ({
      name: truncate(field.name, 256),
      value: truncate(field.value, 1024),
      inline: true
    }));
}

function colorForSeverity(severity) {
  switch (String(severity).toLowerCase()) {
    case '3':
    case '4':
    case 'blocking':
    case 'dataloss':
      return 0xE05252;
    case '2':
    case 'high':
      return 0xF0A23A;
    case '0':
    case 'low':
      return 0x5FA7D8;
    default:
      return 0x25B99A;
  }
}

function clean(value) {
  return value === undefined || value === null ? '' : String(value).trim();
}

function truncate(value, max) {
  const text = clean(value);
  return text.length <= max ? text : text.substring(0, Math.max(0, max - 3)) + '...';
}
