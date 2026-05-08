"""自定义异常类"""


class CollectorError(Exception):
    """采集平台基础异常"""
    pass


class DriverError(CollectorError):
    """驱动层异常"""
    pass


class DeviceConnectionError(CollectorError):
    """设备连接异常

    注意: 命名为 DeviceConnectionError 而非 ConnectionError，
    避免与 Python 内置的 ConnectionError 异常冲突。
    """
    pass


class ReadError(CollectorError):
    """数据读取异常"""
    pass


class StorageError(CollectorError):
    """数据存储异常"""
    pass


class ConfigError(CollectorError):
    """配置异常"""
    pass


class AggregatorError(CollectorError):
    """数据聚合异常"""
    pass
