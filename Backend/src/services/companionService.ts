import { v4 as uuid } from 'uuid';
import { db } from '../db';
import { config } from '../config';
import { AppError } from '../middleware/errorHandler';
import { ChatMessageData, MemoryChapterData, MemoryEntryData } from '../types';
import * as bedrockService from './bedrockService';
import { buildWorldContext } from './worldContextService';

export async function getChatHistory(
  accountId: string,
  worldId: string,
  sessionId?: string,
  limit = 50,
  offset = 0
): Promise<{ sessions: Array<{ id: string; title: string | null; hasUnread: boolean; updatedAt: string }>; messages: ChatMessageData[] }> {
  const sessions = await db.query(
    `SELECT id, title, has_unread, updated_at
     FROM chat_sessions
     WHERE world_id = $1 AND account_id = $2
     ORDER BY updated_at DESC`,
    [worldId, accountId]
  );

  let messages: ChatMessageData[] = [];
  if (sessionId) {
    const msgRows = await db.query(
      `SELECT id, speaker, text, emotion, expression, animation, portrait_id, ap_cost, created_at
       FROM chat_messages
       WHERE session_id = $1
       ORDER BY created_at ASC
       LIMIT $2 OFFSET $3`,
      [sessionId, limit, offset]
    );
    messages = msgRows.rows.map(formatChatMessage);
  } else if (sessions.rows.length > 0) {
    const latestSession = sessions.rows[0];
    const msgRows = await db.query(
      `SELECT id, speaker, text, emotion, expression, animation, portrait_id, ap_cost, created_at
       FROM chat_messages
       WHERE session_id = $1
       ORDER BY created_at ASC
       LIMIT $2 OFFSET $3`,
      [latestSession.id, limit, offset]
    );
    messages = msgRows.rows.map(formatChatMessage);
  }

  return {
    sessions: sessions.rows.map((s: { id: string; title: string | null; has_unread: boolean; updated_at: Date }) => ({
      id: s.id,
      title: s.title,
      hasUnread: s.has_unread,
      updatedAt: s.updated_at.toISOString(),
    })),
    messages,
  };
}

export async function sendChatMessage(
  accountId: string,
  worldId: string,
  text: string,
  sessionId?: string
): Promise<{ playerMessage: ChatMessageData; soulformMessage: ChatMessageData; brainResponse: bedrockService.BrainResponse }> {
  let chatSessionId = sessionId;

  if (!chatSessionId) {
    const sessionResult = await db.query(
      `INSERT INTO chat_sessions (world_id, account_id, title)
       VALUES ($1, $2, $3)
       RETURNING id`,
      [worldId, accountId, text.substring(0, 50)]
    );
    chatSessionId = sessionResult.rows[0].id;
  } else {
    const sessionCheck = await db.query(
      'SELECT id FROM chat_sessions WHERE id = $1 AND world_id = $2 AND account_id = $3',
      [chatSessionId, worldId, accountId]
    );
    if (sessionCheck.rows.length === 0) {
      throw new AppError(404, 'Chat session not found');
    }
  }

  const communicationCost = calculateChatCost(text);

  const balanceResult = await db.query(
    'SELECT balance FROM ap_balances WHERE world_id = $1 AND account_id = $2',
    [worldId, accountId]
  );

  const currentBalance = balanceResult.rows[0]?.balance || 0;
  if (currentBalance < communicationCost) {
    throw new AppError(400, `Insufficient AP for chat. Cost: ${communicationCost}, Balance: ${currentBalance}`);
  }

  const worldContext = await buildWorldContext(worldId);
  const brain = await bedrockService.queryBrain(worldContext, text, config.brain.maxRetries);

  const playerMsgId = uuid();
  await db.query(
    `INSERT INTO chat_messages (id, session_id, speaker, text, ap_cost)
     VALUES ($1, $2, 'Player', $3, $4)`,
    [playerMsgId, chatSessionId, text, communicationCost]
  );

  await db.query(
    'UPDATE ap_balances SET balance = balance - $1, updated_at = NOW() WHERE world_id = $2 AND account_id = $3',
    [communicationCost, worldId, accountId]
  );

  await recordAPTransaction(worldId, accountId, 'chat_spend', -communicationCost, 'Chat message');

  const soulformMsgId = uuid();
  await db.query(
    `INSERT INTO chat_messages (id, session_id, speaker, text, emotion, expression, animation, ap_cost, model_metadata, safety_status)
     VALUES ($1, $2, 'Soulform', $3, $4, $5, $6, 0, $7, $8)`,
    [
      soulformMsgId, chatSessionId, brain.response.currentThought,
      brain.response.mood, brain.response.expression,
      brain.response.animation, JSON.stringify({ model: brain.model, attempts: brain.attempts }),
      brain.safetyStatus,
    ]
  );

  await applyBrainResponseToWorld(worldId, brain.response);

  await db.query(
    `UPDATE chat_sessions SET updated_at = NOW(), title = COALESCE(title, $1) WHERE id = $2`,
    [text.substring(0, 50), chatSessionId]
  );

  await db.query(
    'UPDATE player_stats SET chat_messages_sent = chat_messages_sent + 1 WHERE account_id = $1',
    [accountId]
  );

  const playerMessage: ChatMessageData = {
    id: playerMsgId,
    speaker: 'Player',
    text,
    apCost: communicationCost,
    createdAt: new Date().toISOString(),
  };

  const soulformMessage: ChatMessageData = {
    id: soulformMsgId,
    speaker: 'Soulform',
    text: brain.response.currentThought,
    emotion: brain.response.mood,
    expression: brain.response.expression,
    animation: brain.response.animation,
    apCost: 0,
    createdAt: new Date().toISOString(),
  };

  return { playerMessage, soulformMessage, brainResponse: brain.response };
}

/**
 * Entry point for the Unity brain path (chat + tick).
 * Builds authoritative world context, queries Bedrock, updates world state.
 */
export async function querySoulformBrain(
  worldId: string,
  mode: 'chat' | 'tick',
  playerMessage?: string
): Promise<{ brainResponse: bedrockService.BrainResponse; attempts: number; safetyStatus: string }> {
  const worldContext = await buildWorldContext(worldId);
  const maxRetries = mode === 'tick' ? config.brain.tickMaxRetries : config.brain.maxRetries;
  const brain = await bedrockService.queryBrain(
    worldContext,
    mode === 'chat' ? (playerMessage || null) : null,
    maxRetries
  );

  if (mode === 'chat' && playerMessage) {
    await storeBrainChat(worldId, playerMessage, brain);
  }

  await applyBrainResponseToWorld(worldId, brain.response);

  return {
    brainResponse: brain.response,
    attempts: brain.attempts,
    safetyStatus: brain.safetyStatus,
  };
}

async function storeBrainChat(
  worldId: string,
  playerText: string,
  brain: bedrockService.BrainResult
): Promise<void> {
  const owner = await db.query(
    'SELECT owner_account_id FROM worlds WHERE id = $1',
    [worldId]
  );
  if (owner.rows.length === 0) return;
  const accountId = owner.rows[0].owner_account_id;

  const sessionResult = await db.query(
    `INSERT INTO chat_sessions (world_id, account_id, title)
     VALUES ($1, $2, $3)
     RETURNING id`,
    [worldId, accountId, playerText.substring(0, 50)]
  );
  const sessionId = sessionResult.rows[0].id;

  await db.query(
    `INSERT INTO chat_messages (session_id, speaker, text) VALUES ($1, 'Player', $2)`,
    [sessionId, playerText]
  );
  await db.query(
    `INSERT INTO chat_messages (session_id, speaker, text, emotion, expression, animation, model_metadata, safety_status)
     VALUES ($1, 'Soulform', $2, $3, $4, $5, $6, $7)`,
    [
      sessionId, brain.response.currentThought, brain.response.mood,
      brain.response.expression, brain.response.animation,
      JSON.stringify({ model: brain.model, attempts: brain.attempts }),
      brain.safetyStatus,
    ]
  );
  await db.query(
    'UPDATE chat_sessions SET updated_at = NOW() WHERE id = $1',
    [sessionId]
  );
}

async function applyBrainResponseToWorld(
  worldId: string,
  response: bedrockService.BrainResponse
): Promise<void> {
  await db.query(
    `UPDATE soulform_states
     SET mood = $1, current_activity = $2, current_thought = $3,
         current_expression = $4, current_animation = $5, updated_at = NOW()
     WHERE world_id = $6`,
    [response.mood, response.currentActivity, response.currentThought,
     response.expression, response.animation, worldId]
  );

  if (response.targetX !== null && response.targetZ !== null) {
    await db.query(
      `UPDATE soulform_states SET grid_x = $1, grid_y = $2, grid_z = $3 WHERE world_id = $4`,
      [response.targetX, response.targetY ?? 0, response.targetZ, worldId]
    );
  }

  if (response.action) {
    await db.query(
      `INSERT INTO action_journal_entries
       (world_id, account_id, action_type, action_data, validation_result, server_result)
       SELECT $1, owner_account_id, 'soulform_action', $2, 'accepted', $3
       FROM worlds WHERE id = $1`,
      [worldId, JSON.stringify({ action: response.action, actionTarget: response.actionTarget }),
       JSON.stringify({ source: 'brain', safetyStatus: 'ok' })]
    );
  }
}

export async function getMemoryChapters(
  accountId: string,
  worldId: string
): Promise<MemoryChapterData[]> {
  const chapters = await db.query(
    `SELECT id, chapter_index, summary, start_time, end_time,
            important_people, important_places, skills_learned, major_events
     FROM memory_chapters
     WHERE world_id = $1
     ORDER BY chapter_index ASC`,
    [worldId]
  );

  const result: MemoryChapterData[] = [];
  for (const chapter of chapters.rows) {
    const entries = await db.query(
      `SELECT id, description, category, keywords, created_at
       FROM memory_entries
       WHERE chapter_id = $1
       ORDER BY created_at ASC`,
      [chapter.id]
    );

    result.push({
      id: chapter.id,
      chapterIndex: chapter.chapter_index,
      summary: chapter.summary,
      startTime: chapter.start_time.toISOString(),
      endTime: chapter.end_time?.toISOString() || null,
      importantPeople: chapter.important_people || [],
      importantPlaces: chapter.important_places || [],
      skillsLearned: chapter.skills_learned || [],
      majorEvents: chapter.major_events || [],
      entries: entries.rows.map((e: { id: string; description: string; category: string; keywords: string[]; created_at: Date }) => ({
        id: e.id,
        description: e.description,
        category: e.category,
        keywords: e.keywords || [],
        createdAt: e.created_at.toISOString(),
      })),
    });
  }

  return result;
}

function calculateChatCost(text: string): number {
  const wordCount = text.split(/\s+/).length;
  if (wordCount <= 20) return 1;
  if (wordCount <= 50) return 2;
  return 3;
}

async function recordAPTransaction(
  worldId: string,
  accountId: string,
  entryType: string,
  amount: number,
  reason: string
): Promise<void> {
  const balance = await db.query(
    'SELECT balance FROM ap_balances WHERE world_id = $1 AND account_id = $2',
    [worldId, accountId]
  );

  const currentBalance = balance.rows[0]?.balance || 0;
  const newBalance = currentBalance + amount;

  await db.query(
    `INSERT INTO architect_point_ledger (world_id, account_id, entry_type, amount, balance_after, reason)
     VALUES ($1, $2, $3, $4, $5, $6)`,
    [worldId, accountId, entryType, amount, newBalance, reason]
  );
}

function formatChatMessage(row: {
  id: string;
  speaker: string;
  text: string;
  emotion: string | null;
  expression: string | null;
  animation: string | null;
  portrait_id: string | null;
  ap_cost: number | null;
  created_at: Date;
}): ChatMessageData {
  return {
    id: row.id,
    speaker: row.speaker as 'Player' | 'Soulform',
    text: row.text,
    emotion: row.emotion || undefined,
    expression: row.expression || undefined,
    animation: row.animation || undefined,
    portraitId: row.portrait_id || undefined,
    apCost: row.ap_cost || undefined,
    createdAt: row.created_at.toISOString(),
  };
}
