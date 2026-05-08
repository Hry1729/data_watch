USE `device_watch`;

SET NAMES utf8mb4;

SELECT 'alarm_log_table_count' AS `check_name`, COUNT(*) AS `actual_value`
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN (
    'alarm_level',
    'alarm_rule',
    'alarm_record',
    'alarm_active',
    'operation_log',
    'system_log'
  );

SELECT TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
FROM information_schema.table_constraints
WHERE table_schema = DATABASE()
  AND table_name IN (
    'alarm_level',
    'alarm_rule',
    'alarm_record',
    'alarm_active',
    'operation_log',
    'system_log'
  )
ORDER BY TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME;

SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name IN (
    'alarm_level',
    'alarm_rule',
    'alarm_record',
    'alarm_active',
    'operation_log',
    'system_log'
  )
ORDER BY TABLE_NAME, INDEX_NAME;

SELECT `level_code`, `level_name`, `severity`, `enabled`
FROM `alarm_level`
WHERE `level_code` IN ('INFO', 'WARN', 'ERROR')
ORDER BY `severity`;

SELECT `rule_code`, `rule_name`, `alarm_type`, `enabled`
FROM `alarm_rule`
WHERE `rule_code` LIKE 'SEED_ALARM_RULE_%'
ORDER BY `rule_code`;

SELECT COUNT(*) AS `seed_alarm_record_total`
FROM `alarm_record`
WHERE `alarm_no` LIKE 'ALM_SEED_20260420_%';

SELECT COUNT(*) AS `seed_alarm_active_total`
FROM `alarm_active`
WHERE `record_id` IN (
  SELECT `id`
  FROM `alarm_record`
  WHERE `alarm_no` IN (
    'ALM_SEED_20260420_001',
    'ALM_SEED_20260420_002',
    'ALM_SEED_20260420_003'
  )
);

SELECT COUNT(*) AS `seed_operation_log_total`
FROM `operation_log`
WHERE `request_id` IN (
  'req-op-seed-001',
  'req-op-seed-002',
  'req-op-seed-003',
  'req-op-seed-004',
  'req-op-seed-005',
  'req-op-seed-006',
  'req-op-seed-007',
  'req-op-seed-008',
  'req-op-seed-009',
  'req-op-seed-010'
);

SELECT COUNT(*) AS `seed_system_log_total`
FROM `system_log`
WHERE `trace_id` IN (
  'trace-sys-seed-001',
  'trace-sys-seed-002',
  'trace-sys-seed-003',
  'trace-sys-seed-004',
  'trace-sys-seed-005',
  'trace-sys-seed-006',
  'trace-sys-seed-007',
  'trace-sys-seed-008',
  'trace-sys-seed-009',
  'trace-sys-seed-010',
  'trace-sys-seed-011',
  'trace-sys-seed-012',
  'trace-sys-seed-013',
  'trace-sys-seed-014',
  'trace-sys-seed-015',
  'trace-sys-seed-016',
  'trace-sys-seed-017',
  'trace-sys-seed-018',
  'trace-sys-seed-019',
  'trace-sys-seed-020'
);

SELECT `alarm_type`, COUNT(*) AS `record_count`
FROM `alarm_record`
WHERE `alarm_no` LIKE 'ALM_SEED_20260420_%'
GROUP BY `alarm_type`
ORDER BY `record_count` DESC, `alarm_type` ASC;

SELECT `level_code`, COUNT(*) AS `record_count`
FROM `alarm_record`
WHERE `alarm_no` LIKE 'ALM_SEED_20260420_%'
GROUP BY `level_code`
ORDER BY `record_count` DESC, `level_code` ASC;

SELECT
  'overview_active_alarm_ready' AS `check_name`,
  EXISTS (
    SELECT 1 FROM `alarm_active`
    WHERE `record_id` IN (
      SELECT `id`
      FROM `alarm_record`
      WHERE `alarm_no` IN (
        'ALM_SEED_20260420_001',
        'ALM_SEED_20260420_002',
        'ALM_SEED_20260420_003'
      )
    )
  ) AS `pass_flag`
UNION ALL
SELECT
  'recent_alarm_event_ready',
  EXISTS (
    SELECT 1 FROM `alarm_record`
    WHERE `alarm_no` LIKE 'ALM_SEED_20260420_%'
      AND `start_time` >= NOW() - INTERVAL 1 DAY
  )
UNION ALL
SELECT
  'alarm_distribution_ready',
  EXISTS (
    SELECT 1 FROM `alarm_record`
    WHERE `alarm_no` LIKE 'ALM_SEED_20260420_%'
      AND `alarm_type` IS NOT NULL
      AND `level_code` IS NOT NULL
  )
UNION ALL
SELECT
  'log_list_ready',
  (
    EXISTS (
      SELECT 1 FROM `operation_log`
      WHERE `request_id` = 'req-op-seed-001'
    )
    AND EXISTS (
      SELECT 1 FROM `system_log`
      WHERE `trace_id` = 'trace-sys-seed-001'
    )
  );
