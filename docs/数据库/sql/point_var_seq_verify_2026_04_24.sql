USE `device_watch`;

SET NAMES utf8mb4;
SET @schema_name = DATABASE();

SELECT 'device_variable.var_code removed' AS `check_name`,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS `status`,
       COUNT(*) AS `matched`
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND table_name = 'device_variable'
  AND column_name = 'var_code'
UNION ALL
SELECT 'template_variable.var_code removed',
       IF(COUNT(*) = 0, 'PASS', 'FAIL'),
       COUNT(*)
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND table_name = 'template_variable'
  AND column_name = 'var_code'
UNION ALL
SELECT 'device_variable.var_seq not null',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       COUNT(*)
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND table_name = 'device_variable'
  AND column_name = 'var_seq'
  AND is_nullable = 'NO'
UNION ALL
SELECT 'template_variable.var_seq not null',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       COUNT(*)
FROM information_schema.columns
WHERE table_schema = @schema_name
  AND table_name = 'template_variable'
  AND column_name = 'var_seq'
  AND is_nullable = 'NO'
UNION ALL
SELECT 'uq_device_variable_device_var_seq exists',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       COUNT(*)
FROM information_schema.table_constraints
WHERE table_schema = @schema_name
  AND table_name = 'device_variable'
  AND constraint_name = 'uq_device_variable_device_var_seq'
  AND constraint_type = 'UNIQUE'
UNION ALL
SELECT 'uq_template_variable_template_var_seq exists',
       IF(COUNT(*) = 1, 'PASS', 'FAIL'),
       COUNT(*)
FROM information_schema.table_constraints
WHERE table_schema = @schema_name
  AND table_name = 'template_variable'
  AND constraint_name = 'uq_template_variable_template_var_seq'
  AND constraint_type = 'UNIQUE'
UNION ALL
SELECT 'qc100 default points seeded',
       IF(COUNT(*) = 3, 'PASS', 'FAIL'),
       COUNT(*)
FROM device_variable AS dv
JOIN device AS d
  ON d.id = dv.device_id
WHERE d.device_code = 'qc100'
  AND dv.var_seq IN (30, 11894, 11900);

SELECT
    c.table_name,
    c.column_name,
    c.column_type,
    c.is_nullable,
    c.column_comment
FROM information_schema.columns AS c
WHERE c.table_schema = @schema_name
  AND (
    (c.table_name = 'device_variable' AND c.column_name = 'var_seq')
    OR (c.table_name = 'template_variable' AND c.column_name = 'var_seq')
  )
ORDER BY c.table_name, c.ordinal_position;

SELECT
    tc.table_name,
    tc.constraint_name,
    tc.constraint_type
FROM information_schema.table_constraints AS tc
WHERE tc.table_schema = @schema_name
  AND tc.table_name IN ('device_variable', 'template_variable')
  AND tc.constraint_name IN ('uq_device_variable_device_var_seq', 'uq_template_variable_template_var_seq')
ORDER BY tc.table_name, tc.constraint_name;

SELECT
    dv.id,
    d.device_code,
    dv.var_seq,
    CONCAT(d.device_code, '_', dv.var_seq) AS td_subtable_name,
    dv.var_name,
    dv.io_type,
    dv.var_type,
    dv.var_address,
    dv.description,
    dv.scan_rate,
    dv.is_alarm,
    dv.is_storage
FROM device_variable AS dv
JOIN device AS d
  ON d.id = dv.device_id
WHERE d.device_code = 'qc100'
  AND dv.var_seq IN (30, 11894, 11900)
ORDER BY dv.var_seq;
