"""数据质量测试"""

import pytest
from datetime import datetime, timedelta

from core.data_quality import DataQualityManager, DataQuality, QualityRule, QualityCheck
from core.exceptions import CollectorError


class TestDataQualityManager:
    """数据质量管理器测试"""

    def setup_method(self):
        """每个测试方法前的设置"""
        self.quality_manager = DataQualityManager()

    def test_value_range_rule(self):
        """测试值范围检查规则"""
        # 配置规则
        self.quality_manager.configure_rules({
            "temperature": {
                "min_value": -10,
                "max_value": 50
            }
        })

        # 测试正常值
        result = self.quality_manager.validate_and_store(
            "temperature", 25.0, datetime.now()
        )
        assert result.quality == DataQuality.GOOD

        # 测试低于最小值
        result = self.quality_manager.validate_and_store(
            "temperature", -15.0, datetime.now()
        )
        assert result.quality == DataQuality.OUT_OF_RANGE
        assert "minimum" in result.message

        # 测试超过最大值
        result = self.quality_manager.validate_and_store(
            "temperature", 60.0, datetime.now()
        )
        assert result.quality == DataQuality.OUT_OF_RANGE
        assert "maximum" in result.message

    def test_rate_of_change_rule(self):
        """测试变化率检查规则"""
        # 配置规则
        self.quality_manager.configure_rules({
            "pressure": {
                "max_change_rate": 10.0,  # 10 units per second
                "change_time_window": 60
            }
        })

        # 添加正常数据
        now = datetime.now()
        self.quality_manager.validate_and_store("pressure", 100.0, now - timedelta(seconds=5))
        self.quality_manager.validate_and_store("pressure", 105.0, now - timedelta(seconds=1))

        # 添加异常变化的数据
        result = self.quality_manager.validate_and_store("pressure", 116.0, now)

        # 检查是否标记为可疑
        assert result.quality == DataQuality.SUSPECTED
        assert "change_rate" in result.details

    def test_missing_data(self):
        """测试缺失数据处理"""
        # 测试None值
        result = self.quality_manager.validate_and_store(
            "unknown", None, datetime.now()
        )
        assert result.quality == DataQuality.MISSING

    def test_quality_stats(self):
        """测试质量统计"""
        # 添加测试数据
        now = datetime.now()
        for i in range(10):
            quality = DataQuality.GOOD if i < 8 else DataQuality.BAD
            result = self.quality_manager.validate_and_store(
                "test_var", 100.0 + i, now - timedelta(minutes=i)
            )
            result.quality = quality

        # 获取统计
        stats = self.quality_manager.get_quality_stats("test_var")

        # 验证统计结果
        assert stats["total_points"] == 10
        assert stats["quality_distribution"]["good"] == 80.0
        assert stats["quality_distribution"]["bad"] == 20.0

    def test_interpolation_recovery(self):
        """测试插值恢复"""
        # 添加历史数据
        now = datetime.now()
        self.quality_manager.recovery.add_data_point("temp", 20.0, now - timedelta(minutes=5))
        self.quality_manager.recovery.add_data_point("temp", 30.0, now + timedelta(minutes=5))

        # 恢复缺失的数据
        recovered = self.quality_manager.recovery.recover_missing_data("temp", now)

        # 验证插值结果
        assert recovered == 25.0  # (20 + 30) / 2

    def test_last_known_recovery(self):
        """测试使用上次已知值恢复"""
        # 添加历史数据
        now = datetime.now()
        self.quality_manager.recovery.add_data_point("temp", 20.0, now - timedelta(minutes=5))

        # 恢复缺失的数据
        recovered = self.quality_manager.recovery.recover_missing_data("temp", now, "last_known")

        # 验证结果
        assert recovered == 20.0

    def test_custom_rule(self):
        """测试自定义规则"""
        # 添加自定义规则
        self.quality_manager.validator.add_rule("custom_var", QualityCheck(
            rule_type=QualityRule.CUSTOM,
            parameter={"custom_param": "value"},
            enabled=True
        ))

        # 测试自定义规则（应该总是返回良好）
        result = self.quality_manager.validate_and_store(
            "custom_var", 100.0, datetime.now()
        )
        assert result.quality == DataQuality.GOOD

    def test_extreme_rate_of_change_is_bad(self):
        """极端变化率（>5x 阈值且无历史相似变化）应判定为 BAD，不应再被 AttributeError 静默放行"""
        self.quality_manager.configure_rules({
            "pressure_ext": {
                "max_change_rate": 10.0,
                "change_time_window": 60
            }
        })

        now = datetime.now()
        self.quality_manager.validate_and_store("pressure_ext", 0.0, now - timedelta(seconds=1))
        # 变化率 200 units/sec，远超 5*10 = 50 的极端门槛
        result = self.quality_manager.validate_and_store("pressure_ext", 200.0, now)

        assert result.quality == DataQuality.BAD
        assert "Extreme change rate" in result.message
