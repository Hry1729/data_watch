"""数据质量管理"""

from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import Enum
from typing import Any, Dict, List, Optional, Tuple
from collections import defaultdict
import statistics

from core.logger import get_logger
from core.exceptions import CollectorError

logger = get_logger("data_quality")


class DataQuality(Enum):
    """数据质量标记"""
    GOOD = "good"
    SUSPECTED = "suspected"
    BAD = "bad"
    MISSING = "missing"
    OUT_OF_RANGE = "out_of_range"
    STALE = "stale"


class QualityRule(Enum):
    """质量规则类型"""
    VALUE_RANGE = "value_range"
    RATE_OF_CHANGE = "rate_of_change"
    TREND = "trend"
    COMPARE_WITH_NEIGHBOR = "compare_with_neighbor"
    CUSTOM = "custom"


@dataclass
class QualityCheck:
    """质量检查规则"""
    rule_type: QualityRule
    parameter: Any
    enabled: bool = True
    severity: DataQuality = DataQuality.BAD


@dataclass
class DataQualityResult:
    """数据质量检查结果"""
    variable_id: str
    timestamp: datetime
    value: Any
    quality: DataQuality
    message: str = ""
    details: Dict[str, Any] = None


class DataValidator:
    """数据验证器"""

    def __init__(self):
        self.rules: Dict[str, List[QualityCheck]] = defaultdict(list)
        self._last_values: Dict[str, Tuple[Any, datetime]] = {}
        self._neighbor_value_provider: Optional[callable] = None
        self._historical_data_provider: Optional[callable] = None

    def set_neighbor_value_provider(self, provider: callable) -> None:
        """
        设置相邻变量值提供器

        Args:
            provider: 一个接受 variable_id 参数并返回当前值的可调用对象
        """
        self._neighbor_value_provider = provider

    def set_historical_data_provider(self, provider: callable) -> None:
        """
        设置历史数据提供器

        Args:
            provider: 一个接受 (variable_id, time_window) 参数并返回历史变化率列表的可调用对象
        """
        self._historical_data_provider = provider

    def add_rule(self, variable_id: str, rule: QualityCheck):
        """添加质量检查规则"""
        self.rules[variable_id].append(rule)
        logger.debug(f"Added quality rule for {variable_id}: {rule.rule_type.value}")

    def validate_value(self, variable_id: str, value: Any, timestamp: datetime) -> DataQualityResult:
        """验证单个值的质量"""
        # 默认为良好
        quality = DataQuality.GOOD
        message = ""
        details = {}

        # 检查基本规则
        if value is None:
            return DataQualityResult(
                variable_id=variable_id,
                timestamp=timestamp,
                value=value,
                quality=DataQuality.MISSING,
                message="Value is None"
            )

        # 获取该变量的所有规则
        rules = self.rules.get(variable_id, [])

        for rule in rules:
            if not rule.enabled:
                continue

            try:
                result = self._check_rule(rule, variable_id, value, timestamp)
                if result.quality.value != "good":
                    # 如果有多个规则，选择最严重的质量
                    if result.quality.value in ["bad", "missing", "out_of_range"]:
                        quality = result.quality
                        message = result.message
                        details = result.details
                    elif quality == DataQuality.GOOD and result.quality == DataQuality.SUSPECTED:
                        quality = result.quality
                        message = result.message
                        details = result.details
            except Exception as e:
                logger.error(f"Error checking rule {rule.rule_type} for {variable_id}: {e}")

        # 更新上次值
        self._last_values[variable_id] = (value, timestamp)

        return DataQualityResult(
            variable_id=variable_id,
            timestamp=timestamp,
            value=value,
            quality=quality,
            message=message,
            details=details
        )

    def _check_rule(self, rule: QualityCheck, variable_id: str, value: Any, timestamp: datetime) -> DataQualityResult:
        """检查单个规则"""
        if rule.rule_type == QualityRule.VALUE_RANGE:
            return self._check_value_range(variable_id, value, rule.parameter)
        elif rule.rule_type == QualityRule.RATE_OF_CHANGE:
            return self._check_rate_of_change(variable_id, value, timestamp, rule.parameter)
        elif rule.rule_type == QualityRule.COMPARE_WITH_NEIGHBOR:
            return self._check_with_neighbor(variable_id, value, rule.parameter)
        else:
            return DataQualityResult(
                variable_id=variable_id,
                timestamp=timestamp,
                value=value,
                quality=DataQuality.GOOD
            )

    def _check_value_range(self, variable_id: str, value: Any, parameter: Dict) -> DataQualityResult:
        """检查值范围"""
        min_val = parameter.get("min")
        max_val = parameter.get("max")

        if min_val is not None and value < min_val:
            return DataQualityResult(
                variable_id=variable_id,
                timestamp=datetime.now(),
                value=value,
                quality=DataQuality.OUT_OF_RANGE,
                message=f"Value {value} < minimum {min_val}",
                details={"rule": "value_range", "min": min_val, "actual": value}
            )

        if max_val is not None and value > max_val:
            return DataQualityResult(
                variable_id=variable_id,
                timestamp=datetime.now(),
                value=value,
                quality=DataQuality.OUT_OF_RANGE,
                message=f"Value {value} > maximum {max_val}",
                details={"rule": "value_range", "max": max_val, "actual": value}
            )

        return DataQualityResult(
            variable_id=variable_id,
            timestamp=datetime.now(),
            value=value,
            quality=DataQuality.GOOD
        )

    def _check_rate_of_change(self, variable_id: str, value: Any, timestamp: datetime, parameter: Dict) -> DataQualityResult:
        """检查变化率"""
        max_change = parameter.get("max_change", float('inf'))
        time_window = parameter.get("time_window", 60)  # 秒

        last_value, last_time = self._last_values.get(variable_id, (None, None))

        if last_value is None or (timestamp - last_time).total_seconds() > time_window:
            return DataQualityResult(
                variable_id=variable_id,
                timestamp=timestamp,
                value=value,
                quality=DataQuality.GOOD
            )

        # 计算变化率
        time_diff = (timestamp - last_time).total_seconds()
        if time_diff > 0:
            change_rate = abs(value - last_value) / time_diff

            if change_rate > max_change:
                # 检查是否是阶跃变化（可能是真实的物理变化）
                if change_rate > max_change * 5:  # 5倍于最大允许变化率
                    # 检查历史数据是否有类似变化
                    historical_changes = self._get_historical_changes(variable_id, time_window)
                    if historical_changes and max(historical_changes) > max_change * 3:
                        # 历史上有类似变化，可能是真实的
                        return DataQualityResult(
                            variable_id=variable_id,
                            timestamp=timestamp,
                            value=value,
                            quality=DataQuality.GOOD
                        )
                    else:
                        return DataQualityResult(
                            variable_id=variable_id,
                            timestamp=timestamp,
                            value=value,
                            quality=DataQuality.BAD,
                            message=f"Extreme change rate: {change_rate:.2f} units/sec",
                            details={
                                "rule": "rate_of_change",
                                "change_rate": change_rate,
                                "max_allowed": max_change,
                                "previous_value": last_value,
                                "severity": "extreme"
                            }
                        )
                else:
                    return DataQualityResult(
                        variable_id=variable_id,
                        timestamp=timestamp,
                        value=value,
                        quality=DataQuality.SUSPECTED,
                        message=f"High change rate: {change_rate:.2f} units/sec",
                        details={
                            "rule": "rate_of_change",
                            "change_rate": change_rate,
                            "max_allowed": max_change,
                            "previous_value": last_value
                        }
                    )

        return DataQualityResult(
            variable_id=variable_id,
            timestamp=timestamp,
            value=value,
            quality=DataQuality.GOOD
        )

    def _get_historical_changes(self, variable_id: str, time_window: int) -> List[float]:
        """获取历史变化率"""
        if self._historical_data_provider:
            try:
                return self._historical_data_provider(variable_id, time_window)
            except Exception as e:
                logger.warning("Failed to get historical changes for %s: %s", variable_id, e)
        return []

    def _check_with_neighbor(self, variable_id: str, value: Any, parameter: Dict) -> DataQualityResult:
        """
        与相邻变量比较

        注意：此功能需要外部提供相邻变量的实时值。
        可通过 set_neighbor_value_provider() 设置值提供器来启用此检查。
        """
        neighbor_var = parameter.get("neighbor_variable")
        threshold = parameter.get("threshold", 0.1)  # 10%差异

        # 如果设置了相邻变量值提供器，则进行比较
        if neighbor_var and self._neighbor_value_provider:
            try:
                neighbor_value = self._neighbor_value_provider(neighbor_var)
                if neighbor_value is not None and value is not None:
                    # 计算相对差异
                    try:
                        val_float = float(value)
                        neighbor_float = float(neighbor_value)
                        if neighbor_float != 0:
                            relative_diff = abs(val_float - neighbor_float) / abs(neighbor_float)
                            if relative_diff > threshold:
                                return DataQualityResult(
                                    variable_id=variable_id,
                                    timestamp=datetime.now(),
                                    value=value,
                                    quality=DataQuality.SUSPECTED,
                                    message=f"Value differs from neighbor {neighbor_var} by {relative_diff:.1%}",
                                    details={
                                        "rule": "compare_with_neighbor",
                                        "neighbor_variable": neighbor_var,
                                        "neighbor_value": neighbor_value,
                                        "relative_diff": relative_diff,
                                        "threshold": threshold
                                    }
                                )
                    except (ValueError, TypeError):
                        pass  # 非数值类型，跳过比较
            except Exception as e:
                logger.warning("Failed to get neighbor value for %s: %s", neighbor_var, e)

        # 未配置相邻变量或无可比较数据，返回通过
        return DataQualityResult(
            variable_id=variable_id,
            timestamp=datetime.now(),
            value=value,
            quality=DataQuality.GOOD
        )


class DataRecovery:
    """数据恢复服务"""

    def __init__(self, buffer_size: int = 10000):
        self.buffer_size = buffer_size
        self.data_buffer: Dict[str, List[Tuple[Any, datetime]]] = defaultdict(list)
        self.recovery_strategies = {
            "interpolation": self._interpolate_missing,
            "last_known": self._use_last_known,
            "default_value": self._use_default
        }

    def add_data_point(self, variable_id: str, value: Any, timestamp: datetime):
        """添加数据点到缓冲区"""
        self.data_buffer[variable_id].append((value, timestamp))

        # 限制缓冲区大小
        if len(self.data_buffer[variable_id]) > self.buffer_size:
            self.data_buffer[variable_id] = self.data_buffer[variable_id][-self.buffer_size:]

    def recover_missing_data(self, variable_id: str, timestamp: datetime, strategy: str = "interpolation") -> Any:
        """恢复缺失的数据"""
        data_points = self.data_buffer.get(variable_id, [])

        if not data_points:
            return None

        # 查找时间点前后的数据
        before = None
        after = None

        for i, (value, ts) in enumerate(data_points):
            if ts <= timestamp:
                before = (value, ts)
            else:
                after = (value, ts)
                break

        # 根据策略恢复
        if strategy in self.recovery_strategies:
            return self.recovery_strategies[strategy](before, after, timestamp)
        else:
            # 默认使用插值
            return self._interpolate_missing(before, after, timestamp)

    def _get_historical_changes(self, variable_id: str, time_window: int) -> List[float]:
        """获取历史变化率"""
        if self._historical_data_provider:
            try:
                return self._historical_data_provider(variable_id, time_window)
            except Exception as e:
                logger.warning("Failed to get historical changes for %s: %s", variable_id, e)
        return []

    def _interpolate_missing(self, before: Optional[Tuple[Any, datetime]], after: Optional[Tuple[Any, datetime]], timestamp: datetime) -> Any:
        """插值恢复"""
        if before and after:
            # 线性插值
            val1, ts1 = before
            val2, ts2 = after

            if ts1 == ts2:
                return val1

            # 计算时间权重
            total_diff = (ts2 - ts1).total_seconds()
            current_diff = (timestamp - ts1).total_seconds()
            weight = current_diff / total_diff

            # 尝试数值插值
            try:
                val1_float = float(val1)
                val2_float = float(val2)
                return val1_float + (val2_float - val1_float) * weight
            except (ValueError, TypeError):
                # 插值失败，返回之前的值
                return val1

        elif before:
            return before[0]
        elif after:
            return after[0]
        else:
            return None

    def _use_last_known(self, before: Optional[Tuple[Any, datetime]], after: Optional[Tuple[Any, datetime]], timestamp: datetime) -> Any:
        """使用上次已知值"""
        if before:
            return before[0]
        elif after:
            return after[0]
        else:
            return None

    def _use_default(self, before: Optional[Tuple[Any, datetime]], after: Optional[Tuple[Any, datetime]], timestamp: datetime) -> Any:
        """使用默认值"""
        return 0.0  # 可以根据变量类型设置不同的默认值


class DataQualityManager:
    """数据质量管理器"""

    def __init__(self):
        self.validator = DataValidator()
        self.recovery = DataRecovery()
        self.quality_history: Dict[str, List[DataQualityResult]] = defaultdict(list)

    def configure_rules(self, variable_configs: Dict[str, Dict]):
        """配置质量检查规则"""
        for var_id, config in variable_configs.items():
            # 添加值范围检查
            if "min_value" in config or "max_value" in config:
                self.validator.add_rule(var_id, QualityCheck(
                    rule_type=QualityRule.VALUE_RANGE,
                    parameter={
                        "min": config.get("min_value"),
                        "max": config.get("max_value")
                    }
                ))

            # 添加变化率检查
            if "max_change_rate" in config:
                self.validator.add_rule(var_id, QualityCheck(
                    rule_type=QualityRule.RATE_OF_CHANGE,
                    parameter={
                        "max_change": config["max_change_rate"],
                        "time_window": config.get("change_time_window", 60)
                    }
                ))

    def validate_and_store(self, variable_id: str, value: Any, timestamp: datetime) -> DataQualityResult:
        """验证并存储数据点"""
        # 验证数据质量
        result = self.validator.validate_value(variable_id, value, timestamp)

        # 存储到历史
        self.quality_history[variable_id].append(result)

        # 添加到恢复缓冲区
        self.recovery.add_data_point(variable_id, value, timestamp)

        # 记录质量问题
        if result.quality != DataQuality.GOOD:
            logger.warning(f"Data quality alert - {variable_id}: {result.message}")

        return result

    def get_quality_stats(self, variable_id: str, time_range: timedelta = timedelta(hours=24)) -> Dict[str, Any]:
        """获取质量统计"""
        end_time = datetime.now()
        start_time = end_time - time_range

        history = [
            r for r in self.quality_history.get(variable_id, [])
            if start_time <= r.timestamp <= end_time
        ]

        if not history:
            return {}

        # 统计各质量级别的数量
        quality_counts = defaultdict(int)
        for result in history:
            quality_counts[result.quality.value] += 1

        # 计算百分比
        total = len(history)
        quality_percentages = {
            q: count / total * 100
            for q, count in quality_counts.items()
        }

        return {
            "total_points": total,
            "quality_distribution": quality_percentages,
            "bad_points": quality_counts.get("bad", 0),
            "suspected_points": quality_counts.get("suspected", 0),
            "last_check": history[-1].timestamp if history else None
        }