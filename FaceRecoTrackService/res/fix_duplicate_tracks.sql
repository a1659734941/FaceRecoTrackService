-- ============================================
-- 轨迹记录去重修复脚本
-- 执行前请先备份数据库！
-- ============================================

-- 步骤1: 查看重复记录数量
SELECT 
    person_id,
    snap_camera_ip,
    COUNT(*) as duplicate_count
FROM track_records
GROUP BY person_id, snap_camera_ip
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC;

-- 步骤2: 删除重复记录（保留最早的一条）
-- 使用CTE标记要删除的记录
DELETE FROM track_records
WHERE id IN (
    SELECT id FROM (
        SELECT 
            id,
            person_id,
            snap_camera_ip,
            snap_time,
            ROW_NUMBER() OVER (
                PARTITION BY person_id, snap_camera_ip 
                ORDER BY snap_time ASC
            ) as rn
        FROM track_records
    ) ranked
    WHERE rn > 1
);

-- 步骤3: 添加唯一约束（防止同一人在5秒内同一摄像头重复录入）
-- 注意：这个约束使用部分索引，只对精确时间点生效
-- 如需更严格的约束，请使用触发器

-- 方案A: 精确时间唯一约束
ALTER TABLE track_records 
ADD CONSTRAINT uq_track_records_person_time_camera 
UNIQUE (person_id, snap_time, snap_camera_ip);

-- 步骤4: 创建用于时间窗口去重的函数（可选）
CREATE OR REPLACE FUNCTION check_duplicate_track()
RETURNS TRIGGER AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM track_records 
        WHERE person_id = NEW.person_id 
        AND snap_camera_ip = NEW.snap_camera_ip
        AND snap_time >= NEW.snap_time - INTERVAL '5 seconds'
        AND snap_time <= NEW.snap_time + INTERVAL '5 seconds'
        AND id != NEW.id
    ) THEN
        RAISE EXCEPTION 'Duplicate track record detected for person % at camera % within 5 seconds', 
            NEW.person_id, NEW.snap_camera_ip;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 步骤5: 创建触发器（可选，如果需要更严格的数据库层保护）
-- DROP TRIGGER IF EXISTS trg_check_duplicate_track ON track_records;
-- CREATE TRIGGER trg_check_duplicate_track
--     BEFORE INSERT ON track_records
--     FOR EACH ROW
--     EXECUTE FUNCTION check_duplicate_track();

-- 步骤6: 验证清理结果
SELECT 
    COUNT(*) as total_records,
    COUNT(DISTINCT person_id) as unique_persons,
    COUNT(DISTINCT snap_camera_ip) as unique_cameras
FROM track_records;
