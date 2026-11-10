import { db } from './index';

const migrations: { name: string; sql: string }[] = [
  {
    name: '001_create_accounts',
    sql: `
      CREATE TABLE IF NOT EXISTS accounts (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        email VARCHAR(255) UNIQUE,
        password_hash VARCHAR(255),
        display_name VARCHAR(100),
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE TABLE IF NOT EXISTS platform_identities (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        platform VARCHAR(50) NOT NULL,
        platform_user_id VARCHAR(255) NOT NULL,
        platform_metadata JSONB DEFAULT '{}',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(platform, platform_user_id)
      );

      CREATE INDEX idx_platform_identities_account ON platform_identities(account_id);
    `,
  },
  {
    name: '002_create_player_profiles',
    sql: `
      CREATE TABLE IF NOT EXISTS player_profiles (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL UNIQUE REFERENCES accounts(id) ON DELETE CASCADE,
        display_name VARCHAR(100) NOT NULL,
        region VARCHAR(10) DEFAULT 'us',
        preferences JSONB DEFAULT '{}',
        consent_flags JSONB DEFAULT '{}',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );
    `,
  },
  {
    name: '003_create_devices',
    sql: `
      CREATE TABLE IF NOT EXISTS devices (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        device_type VARCHAR(50) NOT NULL,
        device_id VARCHAR(255) NOT NULL,
        device_name VARCHAR(255),
        is_trusted BOOLEAN DEFAULT FALSE,
        last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(account_id, device_id)
      );

      CREATE INDEX idx_devices_account ON devices(account_id);
    `,
  },
  {
    name: '004_create_sessions',
    sql: `
      CREATE TABLE IF NOT EXISTS sessions (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        refresh_token_hash VARCHAR(255) NOT NULL,
        device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
        ip_address VARCHAR(45),
        expires_at TIMESTAMPTZ NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        revoked_at TIMESTAMPTZ
      );

      CREATE INDEX idx_sessions_account ON sessions(account_id);
      CREATE INDEX idx_sessions_refresh ON sessions(refresh_token_hash);
    `,
  },
  {
    name: '005_create_worlds',
    sql: `
      CREATE TABLE IF NOT EXISTS worlds (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        owner_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        name VARCHAR(255) NOT NULL,
        environment_type VARCHAR(50) NOT NULL DEFAULT 'MeadowGrassland',
        grid_width INT NOT NULL DEFAULT 60,
        grid_height INT NOT NULL DEFAULT 60,
        grid_depth INT NOT NULL DEFAULT 8,
        current_version INT NOT NULL DEFAULT 0,
        current_checkpoint UUID,
        day_count INT NOT NULL DEFAULT 1,
        current_season INT NOT NULL DEFAULT 0,
        weather_condition VARCHAR(50) DEFAULT 'Clear',
        livability_percent INT DEFAULT 0,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_worlds_owner ON worlds(owner_account_id);
    `,
  },
  {
    name: '006_create_world_members',
    sql: `
      CREATE TABLE IF NOT EXISTS world_members (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        role VARCHAR(50) NOT NULL DEFAULT 'viewer',
        joined_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, account_id)
      );

      CREATE INDEX idx_world_members_world ON world_members(world_id);
      CREATE INDEX idx_world_members_account ON world_members(account_id);
    `,
  },
  {
    name: '007_create_world_snapshots',
    sql: `
      CREATE TABLE IF NOT EXISTS world_state_snapshots (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        version INT NOT NULL,
        compressed_state BYTEA NOT NULL,
        checkpoint UUID NOT NULL DEFAULT gen_random_uuid(),
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, version)
      );

      CREATE INDEX idx_snapshots_world ON world_state_snapshots(world_id);
      CREATE INDEX idx_snapshots_checkpoint ON world_state_snapshots(checkpoint);
    `,
  },
  {
    name: '008_create_world_state_diffs',
    sql: `
      CREATE TABLE IF NOT EXISTS world_state_diffs (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        from_version INT NOT NULL,
        to_version INT NOT NULL,
        compressed_diff BYTEA NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, from_version, to_version)
      );

      CREATE INDEX idx_diffs_world ON world_state_diffs(world_id);
    `,
  },
  {
    name: '009_create_action_journal',
    sql: `
      CREATE TABLE IF NOT EXISTS action_journal_entries (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        action_type VARCHAR(100) NOT NULL,
        action_data JSONB NOT NULL,
        validation_result VARCHAR(50),
        server_result JSONB,
        client_timestamp TIMESTAMPTZ,
        server_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        batch_id UUID,
        sequence_index INT DEFAULT 0
      );

      CREATE INDEX idx_action_journal_world ON action_journal_entries(world_id);
      CREATE INDEX idx_action_journal_account ON action_journal_entries(account_id);
      CREATE INDEX idx_action_journal_batch ON action_journal_entries(batch_id);
    `,
  },
  {
    name: '010_create_player_stats',
    sql: `
      CREATE TABLE IF NOT EXISTS player_stats (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL UNIQUE REFERENCES accounts(id) ON DELETE CASCADE,
        total_play_time_seconds BIGINT DEFAULT 0,
        total_days_played INT DEFAULT 0,
        worlds_created INT DEFAULT 0,
        total_population INT DEFAULT 0,
        skills_unlocked INT DEFAULT 0,
        skills_mastered INT DEFAULT 0,
        interventions_used INT DEFAULT 0,
        chat_messages_sent INT DEFAULT 0,
        offline_syncs INT DEFAULT 0,
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );
    `,
  },
  {
    name: '011_create_soulform_state',
    sql: `
      CREATE TABLE IF NOT EXISTS soulform_states (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL UNIQUE REFERENCES worlds(id) ON DELETE CASCADE,
        name VARCHAR(100) NOT NULL DEFAULT 'Soulform',
        meaning VARCHAR(255) DEFAULT '',
        nature VARCHAR(100) DEFAULT 'Explorer',
        temperament VARCHAR(50) DEFAULT 'Inquisitive',
        mood VARCHAR(50) DEFAULT 'Content',
        grid_x INT DEFAULT 12,
        grid_y INT DEFAULT 0,
        grid_z INT DEFAULT 12,
        comfort INT DEFAULT 50,
        hunger INT DEFAULT 50,
        curiosity INT DEFAULT 50,
        health REAL DEFAULT 100.0,
        current_activity VARCHAR(255) DEFAULT 'Observing',
        current_thought TEXT DEFAULT 'Taking in the surroundings.',
        current_expression VARCHAR(100) DEFAULT 'Neutral',
        current_animation VARCHAR(100) DEFAULT 'Idle',
        appearance JSONB DEFAULT '{}',
        voice JSONB DEFAULT '{}',
        outfit JSONB DEFAULT '{}',
        bond_traits JSONB DEFAULT '[]',
        skill_data JSONB DEFAULT '{}',
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_soulform_world ON soulform_states(world_id);
    `,
  },
  {
    name: '012_create_memory_bank',
    sql: `
      CREATE TABLE IF NOT EXISTS memory_chapters (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        chapter_index INT NOT NULL,
        summary TEXT,
        start_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        end_time TIMESTAMPTZ,
        important_people JSONB DEFAULT '[]',
        important_places JSONB DEFAULT '[]',
        skills_learned JSONB DEFAULT '[]',
        major_events JSONB DEFAULT '[]',
        UNIQUE(world_id, chapter_index)
      );

      CREATE TABLE IF NOT EXISTS memory_entries (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        chapter_id UUID NOT NULL REFERENCES memory_chapters(id) ON DELETE CASCADE,
        description TEXT NOT NULL,
        category VARCHAR(50) NOT NULL,
        keywords JSONB DEFAULT '[]',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_memory_chapters_world ON memory_chapters(world_id);
      CREATE INDEX idx_memory_entries_chapter ON memory_entries(chapter_id);
    `,
  },
  {
    name: '013_create_ap_ledger',
    sql: `
      CREATE TABLE IF NOT EXISTS architect_point_ledger (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        entry_type VARCHAR(50) NOT NULL,
        amount INT NOT NULL,
        balance_after INT NOT NULL,
        reason VARCHAR(255),
        reference_id UUID,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_ap_ledger_world ON architect_point_ledger(world_id);
      CREATE INDEX idx_ap_ledger_account ON architect_point_ledger(account_id);

      CREATE TABLE IF NOT EXISTS ap_balances (
        world_id UUID NOT NULL UNIQUE REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        balance INT NOT NULL DEFAULT 100,
        last_passive_ap_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        PRIMARY KEY (world_id, account_id)
      );
    `,
  },
  {
    name: '014_create_chat',
    sql: `
      CREATE TABLE IF NOT EXISTS chat_sessions (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        title VARCHAR(255),
        is_pinned BOOLEAN DEFAULT FALSE,
        has_unread BOOLEAN DEFAULT FALSE,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE TABLE IF NOT EXISTS chat_messages (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        session_id UUID NOT NULL REFERENCES chat_sessions(id) ON DELETE CASCADE,
        speaker VARCHAR(20) NOT NULL,
        text TEXT NOT NULL,
        emotion VARCHAR(50),
        expression VARCHAR(100),
        animation VARCHAR(100),
        portrait_id VARCHAR(100),
        ap_cost INT DEFAULT 0,
        model_metadata JSONB,
        safety_status VARCHAR(50) DEFAULT 'ok',
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_chat_sessions_world ON chat_sessions(world_id);
      CREATE INDEX idx_chat_sessions_account ON chat_sessions(account_id);
      CREATE INDEX idx_chat_messages_session ON chat_messages(session_id);
    `,
  },
  {
    name: '015_create_pairing_sessions',
    sql: `
      CREATE TABLE IF NOT EXISTS pairing_sessions (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
        token_hash VARCHAR(255) NOT NULL,
        short_code VARCHAR(10) NOT NULL,
        status VARCHAR(20) NOT NULL DEFAULT 'pending',
        claimed_by_account_id UUID REFERENCES accounts(id) ON DELETE SET NULL,
        claimed_by_device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
        expires_at TIMESTAMPTZ NOT NULL,
        claimed_at TIMESTAMPTZ,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_pairing_account ON pairing_sessions(account_id);
      CREATE INDEX idx_pairing_code ON pairing_sessions(short_code);
      CREATE INDEX idx_pairing_expires ON pairing_sessions(expires_at);
    `,
  },
  {
    name: '016_create_sync_checkpoints',
    sql: `
      CREATE TABLE IF NOT EXISTS sync_checkpoints (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
        checkpoint UUID NOT NULL,
        version INT NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, account_id, device_id)
      );

      CREATE INDEX idx_sync_checkpoints_world ON sync_checkpoints(world_id);
      CREATE INDEX idx_sync_checkpoints_account ON sync_checkpoints(account_id);
    `,
  },
  {
    name: '017_create_event_log',
    sql: `
      CREATE TABLE IF NOT EXISTS world_event_log (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        event_type VARCHAR(100) NOT NULL,
        description TEXT NOT NULL,
        source VARCHAR(100),
        event_data JSONB DEFAULT '{}',
        server_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_event_log_world ON world_event_log(world_id);
      CREATE INDEX idx_event_log_type ON world_event_log(event_type);
    `,
  },
  {
    name: '018_create_cooldowns',
    sql: `
      CREATE TABLE IF NOT EXISTS world_cooldowns (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
        intervention_type VARCHAR(100) NOT NULL,
        expires_at TIMESTAMPTZ NOT NULL,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, account_id, intervention_type)
      );

      CREATE INDEX idx_cooldowns_world ON world_cooldowns(world_id);
    `,
  },
  {
    name: '019_create_entities',
    sql: `
      CREATE TABLE IF NOT EXISTS entities (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        entity_type VARCHAR(20) NOT NULL,
        grid_x INT NOT NULL DEFAULT 0,
        grid_y INT NOT NULL DEFAULT 0,
        grid_z INT NOT NULL DEFAULT 0,
        facing VARCHAR(10) NOT NULL DEFAULT 'South',
        health REAL NOT NULL DEFAULT 100.0,
        hunger REAL NOT NULL DEFAULT 50.0,
        pending_movement VARCHAR(20) NOT NULL DEFAULT 'None',
        target_x INT,
        target_y INT,
        target_z INT,
        target_entity_id UUID,
        is_dead BOOLEAN NOT NULL DEFAULT FALSE,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_entities_world ON entities(world_id);
      CREATE INDEX idx_entities_type ON entities(world_id, entity_type);

      CREATE TABLE IF NOT EXISTS npc_states (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        entity_id UUID NOT NULL UNIQUE REFERENCES entities(id) ON DELETE CASCADE,
        baseline_state VARCHAR(50) NOT NULL DEFAULT 'Busy',
        current_state VARCHAR(50) NOT NULL DEFAULT 'SafeHungry',
        current_action VARCHAR(50) NOT NULL DEFAULT 'None',
        is_disgruntled BOOLEAN NOT NULL DEFAULT FALSE,
        disgruntled_days INT NOT NULL DEFAULT 0,
        consecutive_resource_shortage_days INT NOT NULL DEFAULT 0,
        has_adequate_resources BOOLEAN NOT NULL DEFAULT FALSE,
        has_shelter BOOLEAN NOT NULL DEFAULT FALSE,
        consecutive_food_days INT NOT NULL DEFAULT 0,
        consecutive_water_days INT NOT NULL DEFAULT 0,
        consecutive_shelter_days INT NOT NULL DEFAULT 0,
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_npc_states_entity ON npc_states(entity_id);

      CREATE TABLE IF NOT EXISTS animal_states (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        entity_id UUID NOT NULL UNIQUE REFERENCES entities(id) ON DELETE CASCADE,
        species VARCHAR(50) NOT NULL DEFAULT 'Generic',
        diet VARCHAR(20) NOT NULL DEFAULT 'Omnivore',
        hunger_level REAL NOT NULL DEFAULT 80.0,
        max_hunger REAL NOT NULL DEFAULT 100.0,
        hunger_rate REAL NOT NULL DEFAULT 2.0,
        current_state VARCHAR(50) NOT NULL DEFAULT 'Docile',
        is_attacked BOOLEAN NOT NULL DEFAULT FALSE,
        social_cooldown REAL NOT NULL DEFAULT 0.0,
        current_food_target VARCHAR(255),
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_animal_states_entity ON animal_states(entity_id);

      CREATE TABLE IF NOT EXISTS soulform_entity_states (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        entity_id UUID NOT NULL UNIQUE REFERENCES entities(id) ON DELETE CASCADE,
        current_skill_attempt VARCHAR(255),
        current_activity VARCHAR(255) NOT NULL DEFAULT 'Observing',
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );

      CREATE INDEX idx_soulform_entity_states_entity ON soulform_entity_states(entity_id);
    `,
  },
  {
    name: '020_create_bubbles',
    sql: `
      CREATE TABLE IF NOT EXISTS bubbles (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        bubble_id VARCHAR(255) NOT NULL,
        environment_type VARCHAR(50) NOT NULL DEFAULT 'MeadowGrassland',
        grid_width INT NOT NULL DEFAULT 60,
        grid_height INT NOT NULL DEFAULT 60,
        grid_depth INT NOT NULL DEFAULT 8,
        season_offset INT NOT NULL DEFAULT 0,
        is_unlocked BOOLEAN NOT NULL DEFAULT FALSE,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, bubble_id)
      );

      CREATE INDEX idx_bubbles_world ON bubbles(world_id);

      CREATE TABLE IF NOT EXISTS bubble_connections (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        from_bubble_id VARCHAR(255) NOT NULL,
        to_bubble_id VARCHAR(255) NOT NULL,
        direction VARCHAR(50) NOT NULL,
        is_open BOOLEAN NOT NULL DEFAULT FALSE,
        created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        UNIQUE(world_id, from_bubble_id, to_bubble_id)
      );

      CREATE INDEX idx_bubble_connections_world ON bubble_connections(world_id);
    `,
  },
  {
    name: '021_create_tile_data',
    sql: `
      CREATE TABLE IF NOT EXISTS tile_cells (
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        bubble_id VARCHAR(255) NOT NULL DEFAULT 'main',
        x INT NOT NULL,
        y INT NOT NULL,
        z INT NOT NULL,
        item_id VARCHAR(255) NOT NULL DEFAULT '',
        layer INT NOT NULL DEFAULT 0,
        is_object BOOLEAN NOT NULL DEFAULT FALSE,
        rotation INT NOT NULL DEFAULT 0,
        auto_tile_variant INT NOT NULL DEFAULT 0,
        from_intervention BOOLEAN NOT NULL DEFAULT FALSE,
        ground_item_id VARCHAR(255) NOT NULL DEFAULT '',
        tile_category VARCHAR(100) NOT NULL DEFAULT '',
        tile_height VARCHAR(20) NOT NULL DEFAULT 'Full',
        PRIMARY KEY (world_id, bubble_id, x, y, z)
      );

      CREATE INDEX idx_tile_cells_world ON tile_cells(world_id);

      CREATE TABLE IF NOT EXISTS tile_compositions (
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        bubble_id VARCHAR(255) NOT NULL DEFAULT 'main',
        x INT NOT NULL,
        y INT NOT NULL,
        z INT NOT NULL,
        base_material VARCHAR(50) NOT NULL DEFAULT 'None',
        surface_cover VARCHAR(50) NOT NULL DEFAULT 'None',
        fluid_type VARCHAR(50) NOT NULL DEFAULT 'None',
        fluid_data JSONB NOT NULL DEFAULT '{}',
        is_half_tile BOOLEAN NOT NULL DEFAULT FALSE,
        PRIMARY KEY (world_id, bubble_id, x, y, z)
      );

      CREATE TABLE IF NOT EXISTS tile_states (
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        bubble_id VARCHAR(255) NOT NULL DEFAULT 'main',
        x INT NOT NULL,
        y INT NOT NULL,
        z INT NOT NULL,
        current_moisture REAL NOT NULL DEFAULT 0,
        surface_water_amount REAL NOT NULL DEFAULT 0,
        current_temperature REAL NOT NULL DEFAULT 20,
        frozen_water_amount REAL NOT NULL DEFAULT 0,
        current_fertility REAL NOT NULL DEFAULT 0,
        erosion_damage REAL NOT NULL DEFAULT 0,
        contamination_level REAL NOT NULL DEFAULT 0,
        fire_state VARCHAR(50) NOT NULL DEFAULT 'None',
        resource_remaining INT NOT NULL DEFAULT 0,
        occupant_id VARCHAR(255) NOT NULL DEFAULT '',
        water_accumulation INT NOT NULL DEFAULT 0,
        sun_accumulation INT NOT NULL DEFAULT 0,
        earth_accumulation INT NOT NULL DEFAULT 0,
        consecutive_excess_days INT NOT NULL DEFAULT 0,
        days_since_last_check INT NOT NULL DEFAULT 0,
        growth_potential REAL NOT NULL DEFAULT 0,
        PRIMARY KEY (world_id, bubble_id, x, y, z)
      );
    `,
  },
  {
    name: '022_create_weather_state',
    sql: `
      ALTER TABLE worlds ADD COLUMN IF NOT EXISTS weather_data JSONB DEFAULT '{}';

      CREATE TABLE IF NOT EXISTS weather_history (
        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        world_id UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
        weather_type VARCHAR(50) NOT NULL,
        season INT NOT NULL,
        day_count INT NOT NULL,
        started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        ended_at TIMESTAMPTZ
      );

      CREATE INDEX idx_weather_history_world ON weather_history(world_id);
    `,
  },
  {
    name: '023_create_client_world_states',
    sql: `
      CREATE TABLE IF NOT EXISTS client_world_states (
        world_key VARCHAR(64) PRIMARY KEY,
        version INT NOT NULL DEFAULT 0,
        state TEXT,
        updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
      );
    `,
  },
  {
    // Device-side pairing sessions: the Unity client has no login (shared
    // x-api-key + worldKey instead of JWT), so its sessions carry a world_key
    // and no account/world UUID. Claiming one links the (mobile) account to
    // the world_key.
    name: '024_pairing_device_sessions',
    sql: `
      ALTER TABLE pairing_sessions ALTER COLUMN account_id DROP NOT NULL;
      ALTER TABLE pairing_sessions ALTER COLUMN world_id DROP NOT NULL;
      ALTER TABLE pairing_sessions ADD COLUMN world_key VARCHAR(64);

      CREATE INDEX idx_pairing_world_key ON pairing_sessions(world_key);
    `,
  },
];

export async function runMigrations(): Promise<void> {
  console.log('Running database migrations...');

  await db.query(`
    CREATE TABLE IF NOT EXISTS _migrations (
      name VARCHAR(255) PRIMARY KEY,
      applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );
  `);

  for (const migration of migrations) {
    const result = await db.query(
      'SELECT 1 FROM _migrations WHERE name = $1',
      [migration.name]
    );

    if (result.rows.length === 0) {
      console.log(`  Applying: ${migration.name}`);
      await db.query(migration.sql);
      await db.query(
        'INSERT INTO _migrations (name) VALUES ($1)',
        [migration.name]
      );
    } else {
      console.log(`  Skipped (already applied): ${migration.name}`);
    }
  }

  console.log('Migrations complete.');
}

if (require.main === module) {
  runMigrations()
    .then(() => process.exit(0))
    .catch((err) => {
      console.error('Migration failed:', err);
      process.exit(1);
    });
}
