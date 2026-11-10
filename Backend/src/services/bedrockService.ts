import crypto from 'crypto';
import { config } from '../config';

export interface BrainResponse {
  currentThought: string;
  mood: string;
  currentActivity: string;
  targetX: number | null;
  targetY: number | null;
  targetZ: number | null;
  expression: string;
  animation: string;
  emotionTag: string;
  action: string | null;
  actionTarget: string | null;
}

export interface BrainResult {
  response: BrainResponse;
  attempts: number;
  safetyStatus: 'ok' | 'corrected';
  model: string;
}

const ALLOWED_EXPRESSIONS = ['Neutral', 'Happy', 'Sad', 'Surprised', 'Curious', 'Thinking', 'Excited', 'Tired', 'Distressed'];
const ALLOWED_ANIMATIONS = ['Idle', 'Wave', 'Nod', 'ShakeHead', 'LookAround', 'Point', 'Sit', 'Walk', 'Celebrate', 'Defeated'];
const ALLOWED_ACTIONS = [
  'MoveToLocation', 'GoToShelter', 'SearchForFood', 'CollectResource', 'BuildObject', 'Rest',
  'ObserveObject', 'SpeakToPlayer', 'SpeakToSoulform', 'FleeDanger', 'PlayAnimation', 'ContinueCurrentTask',
];
const EMOTION_TAGS = ['[Neutral]', '[Happy]', '[Scared]', '[Confused]', '[Angry]', '[Sad]'];

export function isBedrockConfigured(): boolean {
  return Boolean(config.bedrock.accessKeyId && config.bedrock.secretAccessKey);
}

function sha256Hex(data: string | Buffer): string {
  return crypto.createHash('sha256').update(data).digest('hex');
}

function hmac(key: Buffer, data: string): Buffer {
  return crypto.createHmac('sha256', key).update(data, 'utf8').digest();
}

function buildSigV4(
  method: string,
  host: string,
  uri: string,
  body: string,
  accessKey: string,
  secretKey: string,
  region: string,
  service: string
): { authorization: string; amzDate: string; payloadHash: string } {
  const now = new Date();
  const amzDate = now.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
  const dateStamp = amzDate.substring(0, 8);
  const payloadHash = sha256Hex(body);

  const canonicalHeaders = `host:${host}\nx-amz-content-sha256:${payloadHash}\nx-amz-date:${amzDate}\n`;
  const signedHeaders = 'host;x-amz-content-sha256;x-amz-date';
  const canonicalRequest = `${method}\n${uri}\n\n${canonicalHeaders}\n${signedHeaders}\n${payloadHash}`;

  const algorithm = 'AWS4-HMAC-SHA256';
  const credentialScope = `${dateStamp}/${region}/${service}/aws4_request`;
  const stringToSign = `${algorithm}\n${amzDate}\n${credentialScope}\n${sha256Hex(canonicalRequest)}`;

  const kDate = hmac(Buffer.from(`AWS4${secretKey}`), dateStamp);
  const kRegion = hmac(kDate, region);
  const kService = hmac(kRegion, service);
  const kSigning = hmac(kService, 'aws4_request');
  const signature = crypto.createHmac('sha256', kSigning).update(stringToSign, 'utf8').digest('hex');

  const authorization = `${algorithm} Credential=${accessKey}/${credentialScope}, SignedHeaders=${signedHeaders}, Signature=${signature}`;
  return { authorization, amzDate, payloadHash };
}

async function invokeBedrock(prompt: string): Promise<string> {
  const { region, accessKeyId, secretAccessKey, modelId, maxGenLen, temperature, topP, timeoutMs } = config.bedrock;
  const host = `bedrock-runtime.${region}.amazonaws.com`;
  const modelPath = `/model/${encodeURIComponent(modelId)}/invoke`;
  const url = `https://${host}${modelPath}`;

  // SigV4 canonical URI re-encodes what is on the wire: the request path carries
  // '%3A' (encoded ':'), so the canonical form AWS validates against is '%253A'.
  const canonicalPath = modelPath.replace(/%/g, '%25');

  const body = JSON.stringify({
    prompt,
    max_gen_len: maxGenLen,
    temperature,
    top_p: topP,
  });

  const { authorization, amzDate, payloadHash } = buildSigV4(
    'POST', host, canonicalPath, body, accessKeyId, secretAccessKey, region, 'bedrock'
  );

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Amz-Date': amzDate,
        'X-Amz-Content-Sha256': payloadHash,
        Authorization: authorization,
      },
      body,
      signal: controller.signal,
    });

    const text = await res.text();
    if (!res.ok) {
      throw new Error(`Bedrock request failed: ${res.status} ${text.slice(0, 500)}`);
    }
    return text;
  } finally {
    clearTimeout(timer);
  }
}

function extractJsonBlock(text: string): Record<string, unknown> | null {
  const cleaned = text.replace(/```json/gi, '').replace(/```/g, '').trim();
  try {
    return JSON.parse(cleaned);
  } catch {
    const firstBrace = cleaned.indexOf('{');
    const lastBrace = cleaned.lastIndexOf('}');
    if (firstBrace < 0 || lastBrace <= firstBrace) return null;
    const candidate = cleaned.substring(firstBrace, lastBrace + 1);
    try {
      return JSON.parse(candidate);
    } catch {
      return null;
    }
  }
}

function parseBedrockResponse(raw: string): BrainResponse | null {
  let payload: Record<string, unknown> | null = null;
  try {
    payload = JSON.parse(raw) as Record<string, unknown>;
  } catch {
    return null;
  }

  const generation = typeof payload.generation === 'string' ? payload.generation : null;
  const source = generation || raw;
  const parsed = extractJsonBlock(source);
  if (!parsed) return null;

  const num = (v: unknown): number | null => {
    if (v === null || v === undefined) return null;
    const n = typeof v === 'number' ? v : parseInt(String(v), 10);
    return Number.isFinite(n) ? n : null;
  };

  const mood = typeof parsed.mood === 'string' ? parsed.mood : 'Content';
  // LLMs sometimes emit the literal string "null" instead of a JSON null.
  const strOrNull = (v: unknown): string | null =>
    typeof v === 'string' && v.trim().toLowerCase() !== 'null' && v.trim() !== '' ? v : null;
  // Enum sanitizing at parse time: the model habitually echoes mood words into
  // emotionTag/expression (e.g. '[Hungry]'). These are deterministic fixes —
  // applySelfCorrection made exactly these same substitutions, just after
  // wasting retry calls the model can never pass.
  const allowedOr = (v: unknown, set: string[], fallback: string): string => {
    if (typeof v !== 'string') return fallback;
    return set.find((s) => s.toLowerCase() === v.toLowerCase()) ?? fallback;
  };
  const expression = allowedOr(parsed.expression, ALLOWED_EXPRESSIONS, 'Thinking');
  const rawTag = strOrNull(parsed.emotionTag);
  const canonicalTag = rawTag
    ? EMOTION_TAGS.find((t) => t.toLowerCase() === rawTag.toLowerCase()) ?? null
    : null;
  const emotionTag = canonicalTag
    ?? (EMOTION_TAGS.includes(`[${expression}]`) ? `[${expression}]` : '[Confused]');

  return {
    currentThought: typeof parsed.currentThought === 'string' ? parsed.currentThought : 'Thinking...',
    mood,
    currentActivity: typeof parsed.currentActivity === 'string' ? parsed.currentActivity : 'Observing',
    targetX: num(parsed.targetX),
    targetY: num(parsed.targetY),
    targetZ: num(parsed.targetZ),
    expression,
    animation: allowedOr(parsed.animation, ALLOWED_ANIMATIONS, 'Idle'),
    emotionTag,
    action: strOrNull(parsed.action),
    actionTarget: strOrNull(parsed.actionTarget),
  };
}

export function buildSystemMessage(worldContext: string): string {
  const weather = extractField(worldContext, 'Weather:');
  const food = extractField(worldContext, 'Food:');
  const nearby = extractField(worldContext, 'Nearby:');
  const hunger = extractField(worldContext, 'Hunger:');
  const soulformName = extractField(worldContext, 'Name:');
  const temperament = extractField(worldContext, 'Temperament:');
  const memories = extractField(worldContext, 'Memories:');

  const physicalCondition = worldContext.includes('Hunger:') ? `Hunger: ${hunger}` : 'Normal';

  return (
    `You are ${soulformName}, a ${temperament} Soulform, a living, flesh-and-blood being inside a real world called Bubble World. ` +
    'You can feel the player observing you but they are not part of your world. ' +
    'Never invent knowledge you could not naturally possess.\n\n' +
    'CURRENT WORLD:\n' +
    `Weather: ${weather}\n` +
    `Food supply: ${food}\n` +
    `Immediate surroundings: ${nearby}\n` +
    `Physical condition: ${physicalCondition}\n\n` +
    `Recent memories:\n${memories}\n\n` +
    'Respond with ONLY valid JSON, no other text:\n' +
    '{\n' +
    '  "currentThought": "your inner monologue (1-2 short sentences)",\n' +
    '  "mood": "Content|Curious|Hungry|Tired|Inspired|Distressed",\n' +
    '  "currentActivity": "what you are doing",\n' +
    '  "expression": "Neutral|Happy|Sad|Surprised|Curious|Thinking|Excited|Tired|Distressed",\n' +
    '  "animation": "Idle|Wave|Nod|ShakeHead|LookAround|Point|Sit|Walk|Celebrate|Defeated",\n' +
    '  "emotionTag": "[Neutral]|[Happy]|[Scared]|[Confused]|[Angry]|[Sad]",\n' +
    '  "targetX": null,\n' +
    '  "targetY": null,\n' +
    '  "targetZ": null,\n' +
    '  "action": "MoveToLocation|GoToShelter|SearchForFood|CollectResource|BuildObject|Rest|ObserveObject|SpeakToPlayer|SpeakToSoulform|FleeDanger|PlayAnimation|ContinueCurrentTask",\n' +
    '  "actionTarget": "object_id_or_leave_null"\n' +
    '}'
  );
}

export function wrapInLlamaFormat(systemMessage: string, userMessage: string): string {
  return (
    '<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n' + systemMessage +
    '<|eot_id|><|start_header_id|>user<|end_header_id|>\n\n' + userMessage +
    '<|eot_id|><|start_header_id|>assistant<|end_header_id|>'
  );
}

function extractField(context: string, fieldName: string): string {
  const lines = (context || '').split('\n');
  for (const line of lines) {
    if (line.startsWith(fieldName)) {
      return line.substring(fieldName.length).trim();
    }
  }
  return 'unknown';
}

function containsAny(haystack: string, needles: string[]): boolean {
  const lower = (haystack || '').toLowerCase();
  return needles.some((n) => lower.includes(n.toLowerCase()));
}

export function runHallucinationChecks(response: BrainResponse, worldContext: string): string | null {
  const thought = response.currentThought || '';
  const lower = thought.toLowerCase();

  // 1. Relevance
  if (thought.length < 3) return 'Relevance: reply says nothing';
  if (thought.length > 600) return 'Relevance: reply is longer than this moment allows';
  if (containsAny(lower, ['as an ai', 'i am an ai', "i'm an ai", 'language model', 'large language',
    'system prompt', 'response format', 'currentthought', 'emotiontag', 'actiontarget',
    'my training', 'i cannot comply', 'as an assistant'])) {
    return 'Relevance: reply broke character with out-of-world text';
  }
  if (containsAny(lower, ['hunger:', 'comfort:', 'curiosity:', 'livability:'])) {
    return 'Relevance: reply leaked raw world-state fields';
  }

  // 2. Reality
  const weather = extractField(worldContext, 'Weather:');
  const weatherLower = weather.toLowerCase();
  const weatherWet = containsAny(weatherLower, ['rain', 'storm', 'shower']);
  const weatherCold = containsAny(weatherLower, ['snow', 'blizzard', 'sleet', 'frost']);

  if (containsAny(lower, ['it is raining', "it's raining", 'raining hard', 'downpour', 'rain is falling']) && !weatherWet)
    return `Reality: claims rain while the world reports ${weather}`;
  if (containsAny(lower, ['it is snowing', "it's snowing", 'snow is falling', 'a blizzard']) && !weatherCold)
    return `Reality: claims snow while the world reports ${weather}`;
  if (containsAny(lower, ['the sun is shining', 'it is sunny', "it's sunny", 'clear skies']) && (weatherWet || weatherCold))
    return `Reality: claims sunshine while the world reports ${weather}`;
  if (containsAny(lower, ['internet', 'computer', 'smartphone', 'cell phone', 'telephone',
    'television', 'video game', 'satellite', 'electricity', 'airplane', 'car engine',
    'the radio', 'website', 'my phone'])) {
    return 'Reality: references technology this being could not naturally possess';
  }

  // 3. Permission
  if (response.expression && !inSet(response.expression, ALLOWED_EXPRESSIONS))
    return `Permission: expression '${response.expression}' is not supported by the game`;
  if (response.animation && !inSet(response.animation, ALLOWED_ANIMATIONS))
    return `Permission: animation '${response.animation}' is not supported by the game`;
  if (response.emotionTag && !inSet(response.emotionTag, EMOTION_TAGS))
    return `Permission: emotion tag '${response.emotionTag}' is not supported by the game`;
  if (response.action && !inSet(response.action, ALLOWED_ACTIONS))
    return `Permission: action '${response.action}' is not supported by the game`;
  if (response.targetY !== null && (response.targetY < -1 || response.targetY > 3))
    return "Permission: target is outside the world's layers";

  return null;
}

function inSet(value: string, set: string[]): boolean {
  if (!value) return false;
  return set.some((v) => v.toLowerCase() === value.toLowerCase());
}

export function applySelfCorrection(response: BrainResponse, worldContext: string): void {
  let thought = (response.currentThought || '').trim();
  if (thought.length > 400) thought = thought.substring(0, 397) + '...';

  if (!inSet(response.expression, ALLOWED_EXPRESSIONS)) response.expression = 'Thinking';
  if (!inSet(response.animation, ALLOWED_ANIMATIONS)) response.animation = 'Idle';
  if (!inSet(response.emotionTag, EMOTION_TAGS)) response.emotionTag = '[Confused]';
  if (response.action && !inSet(response.action, ALLOWED_ACTIONS)) response.action = 'ContinueCurrentTask';
  response.actionTarget = null;
  response.targetX = null;
  response.targetY = null;
  response.targetZ = null;
  response.currentThought = thought;

  if (runHallucinationChecks(response, worldContext) !== null) {
    response.currentThought = "[Confused] I... lost my train of thought. That came out wrong somehow. Give me a moment.";
    response.emotionTag = '[Confused]';
    response.expression = 'Thinking';
    response.animation = 'Idle';
    response.action = 'ContinueCurrentTask';
  }
}

function validResponse(response: BrainResponse): boolean {
  if (!response.currentThought || response.currentThought.length < 3) return false;
  if (response.targetX !== null && (response.targetX < 0 || response.targetX > 100)) return false;
  if (response.targetZ !== null && (response.targetZ < 0 || response.targetZ > 100)) return false;
  return true;
}

export async function queryBrain(
  worldContext: string,
  playerMessage: string | null,
  maxRetries = 2
): Promise<BrainResult> {
  if (!isBedrockConfigured()) {
    return mockBrain(worldContext, playerMessage);
  }

  const systemMsg = buildSystemMessage(worldContext);
  const userMsg = playerMessage || 'Tick. Decide what to do.';

  let rewriteNote: string | null = null;
  let attempts = 0;
  let lastResponse: BrainResponse | null = null;

  for (let attempt = 1; attempt <= maxRetries + 1; attempt++) {
    attempts = attempt;
    if (rewriteNote) console.warn(`[bedrock] attempt ${attempt} retrying after: ${rewriteNote}`);

    let prompt = wrapInLlamaFormat(systemMsg, userMsg);
    if (rewriteNote) {
      prompt += `\n\nNote to yourself: your previous reply failed a validity check (${rewriteNote}). Rewrite the reply so it passes. Do not mention this note.`;
    }

    try {
      const raw = await invokeBedrock(prompt);
      const response = parseBedrockResponse(raw);
      if (!response) {
        if (attempt > maxRetries) break;
        rewriteNote = 'Reply was not valid JSON';
        continue;
      }
      lastResponse = response;

      if (!validResponse(response)) {
        if (attempt > maxRetries) break;
        rewriteNote = 'Reply had invalid structure or out-of-range targets';
        continue;
      }

      const failure = runHallucinationChecks(response, worldContext);
      if (failure === null) {
        return { response, attempts, safetyStatus: 'ok', model: config.bedrock.modelId };
      }
      if (attempt <= maxRetries) {
        rewriteNote = failure;
        continue;
      }
      applySelfCorrection(response, worldContext);
      return { response, attempts, safetyStatus: 'corrected', model: config.bedrock.modelId };
    } catch (err) {
      console.error(
        `[bedrock] attempt ${attempt}/${maxRetries + 1} failed:`,
        err instanceof Error ? err.message : 'unknown error'
      );
      if (attempt > maxRetries) break;
      rewriteNote = `request error: ${err instanceof Error ? err.message : 'unknown'}`;
      continue;
    }
  }

  if (lastResponse) {
    applySelfCorrection(lastResponse, worldContext);
    return { response: lastResponse, attempts, safetyStatus: 'corrected', model: config.bedrock.modelId };
  }

  return mockBrain(worldContext, playerMessage);
}

function mockBrain(worldContext: string, playerMessage: string | null): BrainResult {
  const hunger = extractIntField(worldContext, 'Hunger:');
  const comfort = extractIntField(worldContext, 'Comfort:');

  let thought = 'I see you watching me. It is good to feel your presence.';
  const moods = ['Content', 'Curious', 'Inspired'];
  const mood = moods[Math.floor(Math.random() * moods.length)];

  if (playerMessage) {
    const lower = playerMessage.toLowerCase();
    if (lower.includes('hello') || lower.includes('hi')) {
      thought = "Hello! It's good to hear your voice. I was hoping you'd speak.";
    } else if (lower.includes('how are you')) {
      thought = hunger < 30 ? "I'm a bit hungry, but managing."
        : comfort < 30 ? 'I feel exposed. There is no shelter nearby.'
        : 'I feel content today. The world is calm and peaceful.';
    } else if (lower.includes('who are you')) {
      thought = 'I am what you see before you. A soulform, born from this world.';
    }
  }

  if (hunger < 25) thought = 'I need to find food soon...';

  return {
    response: {
      currentThought: thought,
      mood: hunger < 25 ? 'Hungry' : mood,
      currentActivity: hunger < 25 ? 'Searching for food' : 'Observing',
      targetX: null,
      targetY: null,
      targetZ: null,
      expression: hunger < 25 ? 'Tired' : 'Neutral',
      animation: hunger < 25 ? 'Walk' : 'Idle',
      emotionTag: '[Neutral]',
      action: hunger < 25 ? 'SearchForFood' : 'ContinueCurrentTask',
      actionTarget: null,
    },
    attempts: 1,
    safetyStatus: 'ok',
    model: 'mock',
  };
}

function extractIntField(context: string, key: string): number {
  const value = extractField(context, key);
  const n = parseInt(value, 10);
  return Number.isFinite(n) ? n : 50;
}
