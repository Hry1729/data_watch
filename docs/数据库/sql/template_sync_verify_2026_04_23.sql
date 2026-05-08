USE `device_watch`;

SET @schema_name = DATABASE();

SELECT TABLE_NAME
FROM information_schema.tables
WHERE table_schema = @schema_name
  AND table_name IN ('unit_dict', 'variable_template', 'template_variable', 'cross_db_task')
ORDER BY TABLE_NAME;

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND (
    (table_name = 'device' AND column_name IN ('template_id', 'template_last_sync_version', 'template_sync_time'))
    OR (table_name = 'device_variable' AND column_name IN ('var_seq', 'unit_id'))
    OR (table_name = 'template_variable' AND column_name IN (
      'template_id', 'var_seq', 'var_name', 'io_type', 'var_type', 'var_address',
      'var_offset', 'var_bit', 'unit_id', 'description', 'scan_rate', 'is_alarm', 'is_storage',
      'create_user', 'create_time', 'update_user', 'update_time'
    ))
  )
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
FROM information_schema.table_constraints
WHERE table_schema = @schema_name
  AND table_name IN ('unit_dict', 'variable_template', 'template_variable', 'cross_db_task', 'device', 'device_variable')
ORDER BY TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME;

SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE
FROM information_schema.statistics
WHERE table_schema = @schema_name
  AND table_name IN ('variable_template', 'template_variable', 'cross_db_task', 'device', 'device_variable')
  AND INDEX_NAME IN (
    'idx_variable_template_device_type_id',
    'idx_variable_template_enabled',
    'idx_template_variable_template_id',
    'idx_template_variable_scan_rate',
    'idx_template_variable_is_alarm',
    'idx_template_variable_unit_id',
    'uq_template_variable_template_var_seq',
    'idx_cross_db_task_biz',
    'idx_cross_db_task_status_retry',
    'idx_device_template_id',
    'idx_device_variable_var_seq',
    'idx_device_variable_unit_id'
  )
ORDER BY TABLE_NAME, INDEX_NAME;

SELECT TRIGGER_NAME, EVENT_MANIPULATION, EVENT_OBJECT_TABLE
FROM information_schema.triggers
WHERE trigger_schema = @schema_name
  AND trigger_name = 'trg_device_code_no_update';

SELECT `unit_code`, `unit_symbol`, `unit_name`
FROM `unit_dict`
ORDER BY `unit_code`;
